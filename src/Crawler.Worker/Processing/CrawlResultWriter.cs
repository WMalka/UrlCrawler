using Crawler.Contracts;
using Crawler.Domain.Jobs;
using Crawler.Domain.Links;
using Crawler.Domain.Pages;
using Crawler.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Crawler.Worker.Processing;

/// <summary>
/// Persists a page result inside the consumer's outbox transaction (ADR-001, ADR-003):
/// lock the job row → finish the page only if it is still Queued → store edges → enqueue new
/// same-domain children within the page budget → update the job status → publish via the outbox.
/// The job row lock serializes writers of one job, so de-duplication and the page budget are race-free.
/// </summary>
internal sealed class CrawlResultWriter(
    CrawlerDbContext db,
    IServiceScopeFactory scopeFactory,
    IOptions<CrawlerWorkerOptions> options,
    TimeProvider clock,
    ILogger<CrawlResultWriter> logger)
{
    /// <returns>False when nothing was written: duplicate delivery, or the job already finished.</returns>
    public async Task<bool> SaveAsync(PageResult result, IPublishEndpoint publisher, CancellationToken cancellationToken)
    {
        var job = await LockJobAsync(result.JobId, cancellationToken);
        if (job is null)
        {
            logger.LogWarning("Job {JobId} no longer exists; result for page {PageId} dropped", result.JobId, result.PageId);
            return false;
        }

        if (job.Status.IsTerminal())
        {
            logger.LogInformation("Job {JobId} is {Status}; result for page {PageId} dropped", job.Id, job.Status, result.PageId);
            return false;
        }

        if (!await TryFinishPageAsync(result, cancellationToken))
        {
            logger.LogInformation("Page {PageId} was already processed (duplicate delivery); nothing written", result.PageId);
            return false;
        }

        if (result.Links is { } links)
        {
            AddLinks(result.PageId, links);
            if (job.CanEnqueueChildrenOf(result.Depth))
                await EnqueueChildrenAsync(job, result, links, publisher, cancellationToken);
        }

        await UpdateJobStatusAsync(job, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Marks a pending job as running when its first page starts. Uses its own short-lived context
    /// (auto-commit) so no lock on the job row is held while the page is downloaded.
    /// </summary>
    public async Task MarkJobStartedAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var separateDb = scope.ServiceProvider.GetRequiredService<CrawlerDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;

        await separateDb.Jobs
            .Where(job => job.Id == jobId && job.Status == CrawlJobStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.Status, CrawlJobStatus.Running)
                .SetProperty(job => job.StartedAt, now), cancellationToken);
    }

    private Task<CrawlJob?> LockJobAsync(Guid jobId, CancellationToken cancellationToken) =>
        db.Jobs
            .FromSql($"SELECT * FROM CrawlJobs WITH (UPDLOCK, ROWLOCK) WHERE Id = {jobId}")
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Idempotency gate: only the first delivery moves the page out of Queued.</summary>
    private async Task<bool> TryFinishPageAsync(PageResult result, CancellationToken cancellationToken)
    {
        var linkCount = result.Links?.Links.Count;
        var internalLinkCount = result.Links?.InternalCount;
        var error = CrawlPage.TruncateError(result.Error);

        var updated = await db.Pages
            .Where(page => page.Id == result.PageId && page.Status == CrawlPageStatus.Queued)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(page => page.Status, result.Status)
                .SetProperty(page => page.LinkCount, linkCount)
                .SetProperty(page => page.InternalLinkCount, internalLinkCount)
                .SetProperty(page => page.Error, error), cancellationToken);

        return updated == 1;
    }

    private void AddLinks(long pageId, PageLinkAnalysis links)
    {
        var maxStored = options.Value.MaxStoredLinksPerPage;
        if (links.Links.Count > maxStored)
            logger.LogInformation("Page {PageId} has {LinkCount} links; storing the first {MaxStored}", pageId, links.Links.Count, maxStored);

        db.Links.AddRange(links.Links
            .Take(maxStored)
            .Select(link => PageLink.Create(pageId, link.Url, link.IsInternal)));
    }

    private async Task EnqueueChildrenAsync(
        CrawlJob job, PageResult result, PageLinkAnalysis links, IPublishEndpoint publisher, CancellationToken cancellationToken)
    {
        var candidates = links.Links
            .Where(link => link.IsInternal)
            .Select(link => link.Url)
            .ToList();
        if (candidates.Count == 0)
            return;

        var pageCount = await db.Pages.CountAsync(page => page.JobId == job.Id, cancellationToken);
        var slots = job.RemainingPageSlots(pageCount);
        if (slots == 0)
        {
            logger.LogInformation("Job {JobId} reached its limit of {MaxPages} pages; no more pages enqueued", job.Id, job.MaxPages);
            return;
        }

        var candidateUrls = candidates.Select(url => url.AbsoluteUri).ToList();
        var alreadyKnown = (await db.Pages
                .Where(page => page.JobId == job.Id && candidateUrls.Contains(page.Url))
                .Select(page => page.Url)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var childDepth = (byte)(result.Depth + 1);
        var children = candidates
            .Where(url => !alreadyKnown.Contains(url.AbsoluteUri))
            .Take(slots)
            .Select(url => CrawlPage.CreateChild(job.Id, result.PageId, childDepth, url))
            .ToList();
        if (children.Count == 0)
            return;

        db.Pages.AddRange(children);
        await db.SaveChangesAsync(cancellationToken); // assigns the page ids the messages refer to

        foreach (var child in children)
            await publisher.Publish(new CrawlPageRequested(job.Id, child.Id), cancellationToken);

        logger.LogInformation("Enqueued {ChildCount} pages at depth {Depth} for job {JobId}", children.Count, childDepth, job.Id);
    }

    private async Task UpdateJobStatusAsync(CrawlJob job, CancellationToken cancellationToken)
    {
        var root = await db.Pages
            .Where(page => page.JobId == job.Id && page.ParentPageId == null)
            .Select(page => new { page.Status, page.Error })
            .SingleAsync(cancellationToken);
        var queued = await db.Pages.CountAsync(
            page => page.JobId == job.Id && page.Status == CrawlPageStatus.Queued, cancellationToken);

        var previous = job.Status;
        job.RecordPageFinished(root.Status, root.Error, queued, clock.GetUtcNow().UtcDateTime);

        if (job.Status != previous && job.Status.IsTerminal())
            logger.LogInformation("Job {JobId} finished with status {Status}", job.Id, job.Status);
    }
}
