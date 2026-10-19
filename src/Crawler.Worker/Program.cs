using Crawler.Infrastructure;
using Crawler.Infrastructure.Messaging;
using Crawler.Worker;
using Crawler.Worker.Fetching;
using Crawler.Worker.Processing;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<CrawlerWorkerOptions>()
    .Bind(builder.Configuration.GetSection(CrawlerWorkerOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddCrawlerPersistence(builder.Configuration);
builder.Services.AddCrawlerMessaging(builder.Configuration, bus =>
{
    bus.AddConsumer<CrawlPageConsumer, CrawlPageConsumerDefinition>();
    bus.AddConsumer<CrawlPageFaultConsumer, CrawlPageFaultConsumerDefinition>();
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddPageFetching();
builder.Services.AddScoped<CrawlPageProcessor>();
builder.Services.AddScoped<CrawlResultWriter>();

var app = builder.Build();

// The worker exposes HTTP only for health probes; the database schema is migrated by the API.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(DependencyInjection.ReadinessTag)
});

app.Run();
