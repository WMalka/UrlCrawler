using Crawler.Contracts;
using Crawler.Domain.Jobs;
using Crawler.Domain.Pages;
using Crawler.Infrastructure;
using Crawler.Infrastructure.Messaging;
using Crawler.Infrastructure.Persistence;
using Crawler.Worker;
using Crawler.Worker.Fetching;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.IntegrationTests.Infrastructure;

/// <summary>
/// The real worker pipeline (consumers, outbox, retry, persistence) on SQL Server, with the
/// MassTransit in-memory test harness instead of RabbitMQ and fixture HTML instead of the network.
/// </summary>
internal sealed class WorkerTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private WorkerTestHost(ServiceProvider provider, FixtureSiteHandler site)
    {
        _provider = provider;
        Site = site;
        Harness = provider.GetRequiredService<ITestHarness>();
    }

    public FixtureSiteHandler Site { get; }
    public ITestHarness Harness { get; }

    public static async Task<WorkerTestHost> StartAsync(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Crawler"] = connectionString,
                ["Crawler:MaxRetryAttempts"] = "0",
                ["Crawler:AttemptTimeoutSeconds"] = "5",
                ["Crawler:TotalTimeoutSeconds"] = "10"
            })
            .Build();

        var site = new FixtureSiteHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddCrawlerPersistence();
        services.AddCrawlerWorker(configuration);
        services.AddHttpClient<HtmlPageFetcher>()
            .ConfigurePrimaryHttpMessageHandler(() => site)
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
        services.AddMassTransitTestHarness(bus =>
        {
            bus.AddCrawlerOutbox();
            bus.AddCrawlerWorkerConsumers();
        });
        services.Configure<TestHarnessOptions>(options => options.TestTimeout = TimeSpan.FromMinutes(2));

        var provider = services.BuildServiceProvider(validateScopes: true);
        await provider.MigrateCrawlerDatabaseAsync();

        var host = new WorkerTestHost(provider, site);
        await host.Harness.Start();
        return host;
    }

    public async Task<(Guid JobId, long RootPageId)> SeedJobAsync(string path = "/", byte maxDepth = 2, int maxPages = 200)
    {
        var url = new Uri(FixtureSiteHandler.BaseUri, path);
        var job = CrawlJob.Create(url, maxDepth, maxPages, DateTime.UtcNow);

        return await WithDbAsync(async db =>
        {
            db.Jobs.Add(job);
            var root = CrawlPage.CreateRoot(job.Id, url);
            db.Pages.Add(root);
            await db.SaveChangesAsync();
            return (job.Id, root.Id);
        });
    }

    public Task PublishAsync(Guid jobId, long pageId) =>
        Harness.Bus.Publish(new CrawlPageRequested(jobId, pageId));

    public async Task<CrawlJob> WaitForJobToFinishAsync(Guid jobId, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(60));
        while (true)
        {
            var job = await WithDbAsync(db => db.Jobs.AsNoTracking().SingleAsync(j => j.Id == jobId));
            if (job.Status.IsTerminal())
                return job;
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Job {jobId} still {job.Status} after timeout.");
            await Task.Delay(200);
        }
    }

    public async Task<T> WithDbAsync<T>(Func<CrawlerDbContext, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<CrawlerDbContext>());
    }

    public async ValueTask DisposeAsync()
    {
        await Harness.Stop();
        await _provider.DisposeAsync();
    }
}
