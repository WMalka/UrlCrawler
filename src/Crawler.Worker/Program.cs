using Crawler.Infrastructure;
using Crawler.Infrastructure.Messaging;
using Crawler.Worker;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCrawlerPersistence();
builder.Services.AddCrawlerMessaging(builder.Configuration, bus => bus.AddCrawlerWorkerConsumers());
builder.Services.AddCrawlerWorker(builder.Configuration);

var app = builder.Build();

// The worker exposes HTTP only for health probes; the database schema is migrated by the API.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(DependencyInjection.ReadinessTag)
});

app.Run();
