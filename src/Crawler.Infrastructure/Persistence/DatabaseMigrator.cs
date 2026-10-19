using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Crawler.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Applies pending migrations, waiting for the database container to accept connections.
    /// Meant for local/compose runs; production should run a migration bundle as a deploy step.
    /// </summary>
    public static async Task MigrateCrawlerDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CrawlerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseMigrator));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations applied");
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Database not ready (attempt {Attempt}/{MaxAttempts}); retrying in {Delay}",
                    attempt, MaxAttempts, RetryDelay);
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }
}
