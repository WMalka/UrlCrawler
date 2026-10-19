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

    public static IServiceCollection AddCrawlerPersistence(this IServiceCollection services)
    {
        // The connection string is resolved when the context is created, not at registration, so
        // configuration added later (e.g. by WebApplicationFactory in tests) is honoured.
        // No EnableRetryOnFailure: transient SQL errors are retried at the message level (whole
        // consumer re-runs idempotently), which also keeps explicit transactions simple.
        services.AddDbContext<CrawlerDbContext>((provider, options) =>
            options.UseSqlServer(GetConnectionString(provider.GetRequiredService<IConfiguration>())));
        services.AddHealthChecks().AddDbContextCheck<CrawlerDbContext>("database", tags: [ReadinessTag]);
        return services;
    }

    private static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.")
            : connectionString;
    }
}
