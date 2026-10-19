using Crawler.Domain.Jobs;
using Crawler.Domain.Pages;

namespace Crawler.Api.Jobs;

public sealed record CreateJobRequest(string? Url, int? MaxDepth);

public sealed record CreateJobResponse(Guid JobId);

public sealed record PageCountsDto(int Total, int Queued, int Done, int Skipped, int Failed);

public sealed record JobWithPageCountsDto(
    Guid JobId,
    string Url,
    CrawlJobStatus Status,
    int MaxDepth,
    int MaxPages,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? FailureReason,
    PageCountsDto PageCounts);

public sealed record JobSummaryDto(
    Guid JobId,
    string Url,
    CrawlJobStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record PageNodeDto(
    long PageId,
    string Url,
    int Depth,
    CrawlPageStatus Status,
    int? LinkCount,
    int? InternalLinkCount,
    decimal? DomainLinkRatio,
    string? Error,
    IReadOnlyList<PageNodeDto> Children);

public sealed record JobTreeDto(Guid JobId, CrawlJobStatus Status, PageNodeDto? Root);

public enum JobTreeAvailability
{
    Available,
    NotFound,
    NotCompleted
}

public sealed record JobTreeResult(JobTreeAvailability Availability, CrawlJobStatus? JobStatus, JobTreeDto? Tree)
{
    public static readonly JobTreeResult NotFound = new(JobTreeAvailability.NotFound, null, null);
    public static JobTreeResult NotCompleted(CrawlJobStatus status) => new(JobTreeAvailability.NotCompleted, status, null);
    public static JobTreeResult Available(JobTreeDto tree) => new(JobTreeAvailability.Available, tree.Status, tree);
}

public sealed record PageLinkDto(string Url, bool IsInternal);
