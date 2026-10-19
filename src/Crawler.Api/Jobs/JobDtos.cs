using Crawler.Domain.Jobs;
using Crawler.Domain.Pages;

namespace Crawler.Api.Jobs;

public sealed record CreateJobRequest(string? Url, int? MaxDepth);

public sealed record CreateJobResponse(Guid JobId);

public sealed record JobProgressDto(int Total, int Queued, int Done, int Skipped, int Failed);

public sealed record JobDetailsDto(
    Guid JobId,
    string Url,
    CrawlJobStatus Status,
    int MaxDepth,
    int MaxPages,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? FailureReason,
    JobProgressDto Progress);

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

public sealed record PageLinkDto(string Url, bool IsInternal);
