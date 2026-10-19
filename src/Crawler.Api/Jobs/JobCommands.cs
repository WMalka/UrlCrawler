using Crawler.Contracts;
using Crawler.Domain.Jobs;
using Crawler.Domain.Pages;
using Crawler.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Crawler.Api.Jobs;

public enum CancelJobResult
{
    Canceled,
    NotFound,
    AlreadyFinished
}

public sealed class JobCommands(
    CrawlerDbContext db,
    IPublishEndpoint publishEndpoint,
    IOptions<CrawlJobOptions> options,
    TimeProvider clock,
    ILogger<JobCommands> logger)
{
    /// <summary>
    /// Stores the job, its root page and the first crawl message in one transaction.
    /// The bus outbox delivers the message only after commit, so a job is never left without work
    /// and no message ever refers to a job that was rolled back.
    /// </summary>
    public async Task<Guid> CreateAsync(Uri normalizedUrl, byte maxDepth, CancellationToken cancellationToken)
    {
        var job = CrawlJob.Create(normalizedUrl, maxDepth, options.Value.MaxPagesPerJob, clock.GetUtcNow().UtcDateTime);
        db.Jobs.Add(job);
        var rootPage = CrawlPage.CreateRoot(job.Id, normalizedUrl);
        db.Pages.Add(rootPage);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await publishEndpoint.Publish(new CrawlPageRequested(job.Id, rootPage.Id), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Created crawl job {JobId} for {Url} (max depth {MaxDepth}, max pages {MaxPages})",
            job.Id, job.Url, job.MaxDepth, job.MaxPages);
        return job.Id;
    }

    /// <summary>
    /// Conditional update: only a Pending or Running job can be canceled, which is race-free
    /// against a worker completing the job at the same moment.
    /// </summary>
    public async Task<CancelJobResult> CancelAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var updated = await db.Jobs
            .Where(job => job.Id == jobId &&
                          (job.Status == CrawlJobStatus.Pending || job.Status == CrawlJobStatus.Running))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.Status, CrawlJobStatus.Canceled)
                .SetProperty(job => job.CompletedAt, now), cancellationToken);

        if (updated == 1)
        {
            logger.LogInformation("Canceled crawl job {JobId}", jobId);
            return CancelJobResult.Canceled;
        }

        return await db.Jobs.AnyAsync(job => job.Id == jobId, cancellationToken)
            ? CancelJobResult.AlreadyFinished
            : CancelJobResult.NotFound;
    }
}
