using Crawler.Worker.Fetching;
using Crawler.Worker.Processing;
using MassTransit;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crawler.Worker;

/// <summary>Worker composition, shared by Program and the integration tests.</summary>
internal static class WorkerRegistration
{
    public static IServiceCollection AddCrawlerWorker(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CrawlerWorkerOptions>()
            .Bind(configuration.GetSection(CrawlerWorkerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddPageFetching();
        services.AddScoped<CrawlPageProcessor>();
        services.AddScoped<CrawlResultWriter>();
        return services;
    }

    public static IBusRegistrationConfigurator AddCrawlerWorkerConsumers(this IBusRegistrationConfigurator bus)
    {
        bus.AddConsumer<CrawlPageConsumer, CrawlPageConsumerDefinition>();
        bus.AddConsumer<CrawlPageFaultConsumer, CrawlPageFaultConsumerDefinition>();
        return bus;
    }
}
