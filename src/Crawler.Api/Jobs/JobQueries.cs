using Crawler.Domain.Jobs;
using Crawler.Domain.Links;
using Crawler.Domain.Pages;
using Crawler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crawler.Api.Jobs;

/// <summary>Read side: no-tracking projections straight to DTOs.</summary>
public sealed class JobQueries(CrawlerDbContext db)
{
    /// <summary>
    /// One round trip: the page counts are correlated scalar subqueries, each an index seek on
    /// (JobId, Status). This is the endpoint the UI polls while a job runs.
    /// </summary>
    public Task<JobWithPageCountsDto?> GetJobWithPageCountsAsync(Guid jobId, CancellationToken cancellationToken) =>
        db.Jobs.AsNoTracking()
            .Where(j => j.Id == jobId)
            .Select(j => new JobWithPageCountsDto(
                j.Id, j.Url, j.Status, j.MaxDepth, j.MaxPages,
                j.CreatedAt, j.StartedAt, j.CompletedAt, j.FailureReason,
                new PageCountsDto(
                    db.Pages.Count(p => p.JobId == j.Id),
                    db.Pages.Count(p => p.JobId == j.Id && p.Status == CrawlPageStatus.Queued),
                    db.Pages.Count(p => p.JobId == j.Id && p.Status == CrawlPageStatus.Done),
                    db.Pages.Count(p => p.JobId == j.Id && p.Status == CrawlPageStatus.Skipped),
                    db.Pages.Count(p => p.JobId == j.Id && p.Status == CrawlPageStatus.Failed))))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<JobSummaryDto>> GetHistoryAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var totalCount = await db.Jobs.CountAsync(cancellationToken);
        var items = await db.Jobs.AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .ThenByDescending(j => j.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JobSummaryDto(j.Id, j.Url, j.Status, j.CreatedAt, j.CompletedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<JobSummaryDto>(items, page, pageSize, totalCount);
    }

    /// <summary>
    /// The tree is only served for completed jobs, so clients never render a half-built result.
    /// Loads all pages in one query (bounded by MaxPages) and links them via ParentPageId.
    /// </summary>
    public async Task<JobTreeResult> GetTreeAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var status = await db.Jobs
            .Where(j => j.Id == jobId)
            .Select(j => (CrawlJobStatus?)j.Status)
            .SingleOrDefaultAsync(cancellationToken);
        if (status is null)
            return JobTreeResult.NotFound;
        if (status != CrawlJobStatus.Completed)
            return JobTreeResult.NotCompleted(status.Value);

        var pages = await db.Pages.AsNoTracking()
            .Where(p => p.JobId == jobId)
            .OrderBy(p => p.Id)
            .Select(p => new PageRow(p.Id, p.ParentPageId, p.Url, p.Depth, p.Status, p.LinkCount, p.InternalLinkCount, p.Error))
            .ToListAsync(cancellationToken);

        return JobTreeResult.Available(new JobTreeDto(jobId, status.Value, BuildTree(pages)));
    }

    public async Task<IReadOnlyList<PageLinkDto>?> GetLinksAsync(Guid jobId, long pageId, CancellationToken cancellationToken)
    {
        var pageBelongsToJob = await db.Pages.AnyAsync(p => p.Id == pageId && p.JobId == jobId, cancellationToken);
        if (!pageBelongsToJob)
            return null;

        return await db.Links.AsNoTracking()
            .Where(l => l.SourcePageId == pageId)
            .OrderBy(l => l.Id)
            .Select(l => new PageLinkDto(l.TargetUrl, l.IsInternal))
            .ToListAsync(cancellationToken);
    }

    private sealed record PageRow(
        long Id, long? ParentId, string Url, byte Depth, CrawlPageStatus Status, int? LinkCount, int? InternalLinkCount, string? Error);

    private static PageNodeDto? BuildTree(IReadOnlyList<PageRow> pages)
    {
        var childrenByParent = pages
            .Where(p => p.ParentId is not null)
            .ToLookup(p => p.ParentId!.Value);

        PageNodeDto ToNode(PageRow page) => new(
            page.Id,
            page.Url,
            page.Depth,
            page.Status,
            page.LinkCount,
            page.InternalLinkCount,
            page.Status == CrawlPageStatus.Done && page.LinkCount is { } total && page.InternalLinkCount is { } @internal
                ? DomainLinkRatio.Calculate(@internal, total)
                : null,
            page.Error,
            childrenByParent[page.Id].Select(ToNode).ToList());

        var root = pages.FirstOrDefault(p => p.ParentId is null);
        return root is null ? null : ToNode(root);
    }
}
