using Crawler.Domain.Jobs;
using Crawler.Domain.Links;
using Crawler.Domain.Pages;
using Crawler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crawler.Api.Jobs;

/// <summary>Read side: no-tracking projections straight to DTOs.</summary>
public sealed class JobQueries(CrawlerDbContext db)
{
    public async Task<JobDetailsDto?> GetDetailsAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await db.Jobs.AsNoTracking()
            .Where(j => j.Id == jobId)
            .Select(j => new { j.Id, j.Url, j.Status, j.MaxDepth, j.MaxPages, j.CreatedAt, j.StartedAt, j.CompletedAt, j.FailureReason })
            .SingleOrDefaultAsync(cancellationToken);
        if (job is null)
            return null;

        var counts = await db.Pages.AsNoTracking()
            .Where(p => p.JobId == jobId)
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status, g => g.Count, cancellationToken);

        int CountOf(CrawlPageStatus status) => counts.GetValueOrDefault(status);
        var progress = new JobProgressDto(
            counts.Values.Sum(),
            CountOf(CrawlPageStatus.Queued),
            CountOf(CrawlPageStatus.Done),
            CountOf(CrawlPageStatus.Skipped),
            CountOf(CrawlPageStatus.Failed));

        return new JobDetailsDto(job.Id, job.Url, job.Status, job.MaxDepth, job.MaxPages,
            job.CreatedAt, job.StartedAt, job.CompletedAt, job.FailureReason, progress);
    }

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
    /// Loads all pages of the job in one query (bounded by MaxPages) and links them via ParentPageId.
    /// Works for running jobs too, returning the part of the tree discovered so far.
    /// </summary>
    public async Task<JobTreeDto?> GetTreeAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var status = await db.Jobs
            .Where(j => j.Id == jobId)
            .Select(j => (CrawlJobStatus?)j.Status)
            .SingleOrDefaultAsync(cancellationToken);
        if (status is null)
            return null;

        var pages = await db.Pages.AsNoTracking()
            .Where(p => p.JobId == jobId)
            .OrderBy(p => p.Id)
            .Select(p => new PageRow(p.Id, p.ParentPageId, p.Url, p.Depth, p.Status, p.LinkCount, p.InternalLinkCount, p.Error))
            .ToListAsync(cancellationToken);

        return new JobTreeDto(jobId, status.Value, BuildTree(pages));
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
