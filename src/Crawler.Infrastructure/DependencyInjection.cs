using Crawler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Crawler";
    /// <summary>Health checks with this tag gate readiness; MassTransit adds its bus check under the same tag.</summary>
    public const string ReadinessTag = "ready";

    public static IServiceCollection AddCrawlerPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

        // No EnableRetryOnFailure: transient SQL errors are retried at the message level (whole
        // consumer re-runs idempotently), which also keeps explicit transactions simple.
        services.AddDbContext<CrawlerDbContext>(options => options.UseSqlServer(connectionString));
        services.AddHealthChecks().AddDbContextCheck<CrawlerDbContext>("database", tags: [ReadinessTag]);
        return services;
    }
}
