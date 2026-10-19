using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Crawler.Api.Controllers;
using Crawler.Api.Jobs;
using Crawler.Contracts;
using Crawler.Domain.Jobs;
using Crawler.Infrastructure.Messaging;
using Crawler.IntegrationTests.Infrastructure;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.IntegrationTests;

/// <summary>The real API on SQL Server, with the in-memory bus instead of RabbitMQ.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class ApiTests(SqlServerFixture sql) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<JobsController> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        var connectionString = sql.CreateDatabaseConnectionString();
        _factory = new WebApplicationFactory<JobsController>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Crawler"] = connectionString,
                ["Database:MigrateOnStartup"] = "true",
                ["RabbitMq:Username"] = "unused",
                ["RabbitMq:Password"] = "unused"
            }));
            builder.ConfigureTestServices(services =>
            {
                services.AddMassTransitTestHarness(bus =>
                {
                    bus.AddCrawlerOutbox();
                    bus.AddHandler<CrawlPageRequested>((ConsumeContext<CrawlPageRequested> _) => Task.CompletedTask); // stands in for the worker
                });
                // The bus outbox delivers after commit on a ~1 s polling cycle; wait longer than that for activity.
                services.Configure<TestHarnessOptions>(options => options.TestInactivityTimeout = TimeSpan.FromSeconds(10));
            });
        });
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Create_job_stores_job_and_root_page_and_publishes_first_message_via_outbox()
    {
        var response = await _client.PostAsJsonAsync("/api/jobs", new { url = "https://Example.com:443/docs#intro" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateJobResponse>(Json);
        Assert.Equal($"/api/jobs/{created!.JobId}", response.Headers.Location!.AbsolutePath);

        var job = await _client.GetFromJsonAsync<JobWithPageCountsDto>($"/api/jobs/{created.JobId}", Json);
        Assert.Equal("https://example.com/docs", job!.Url);
        Assert.Equal(CrawlJobStatus.Pending, job.Status);
        Assert.Equal(2, job.MaxDepth);
        Assert.Equal(200, job.MaxPages);
        Assert.Equal(new PageCountsDto(Total: 1, Queued: 1, Done: 0, Skipped: 0, Failed: 0), job.PageCounts);

        // The bus outbox replays stored messages to the transport, so assert on what a consumer receives.
        var harness = _factory.Services.GetTestHarness();
        Assert.True(await harness.Consumed.Any<CrawlPageRequested>(
            x => x.Context.Message.JobId == created.JobId && x.Context.CorrelationId == created.JobId));
    }

    [Fact]
    public async Task Invalid_request_returns_validation_problem_details()
    {
        var response = await _client.PostAsJsonAsync("/api/jobs", new { url = "mailto:someone@example.com", maxDepth = 9 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);
        Assert.Contains("Url", problem!.Errors.Keys);
        Assert.Contains("MaxDepth", problem.Errors.Keys);
    }

    [Fact]
    public async Task Tree_is_not_available_until_the_job_completes()
    {
        var created = await (await _client.PostAsJsonAsync("/api/jobs", new { url = "https://example.com/" }))
            .Content.ReadFromJsonAsync<CreateJobResponse>(Json);

        var tree = await _client.GetAsync($"/api/jobs/{created!.JobId}/tree");
        var unknown = await _client.GetAsync($"/api/jobs/{Guid.NewGuid()}/tree");

        Assert.Equal(HttpStatusCode.Conflict, tree.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Cancel_is_allowed_once_then_conflicts()
    {
        var created = await (await _client.PostAsJsonAsync("/api/jobs", new { url = "https://example.com/" }))
            .Content.ReadFromJsonAsync<CreateJobResponse>(Json);

        var first = await _client.PostAsync($"/api/jobs/{created!.JobId}/cancel", null);
        var second = await _client.PostAsync($"/api/jobs/{created.JobId}/cancel", null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task History_is_paginated_newest_first()
    {
        foreach (var path in new[] { "one", "two", "three" })
            await _client.PostAsJsonAsync("/api/jobs", new { url = $"https://example.com/{path}" });

        var page1 = await _client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?page=1&pageSize=2", Json);
        var page2 = await _client.GetFromJsonAsync<PagedResult<JobSummaryDto>>("/api/jobs?page=2&pageSize=2", Json);
        var invalid = await _client.GetAsync("/api/jobs?page=0&pageSize=1000");

        Assert.Equal(3, page1!.TotalCount);
        Assert.Equal(["https://example.com/three", "https://example.com/two"], page1.Items.Select(j => j.Url));
        Assert.Equal(["https://example.com/one"], page2!.Items.Select(j => j.Url));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}
