using Crawler.Domain.Links;
using Crawler.Domain.Pages;

namespace Crawler.Worker.Processing;

/// <summary>Outcome of crawling one page, ready to be persisted by <see cref="CrawlResultWriter"/>.</summary>
internal sealed record PageResult(
    Guid JobId, long PageId, byte Depth, CrawlPageStatus Status, PageLinkAnalysis? Links, string? Error)
{
    public static PageResult Done(Guid jobId, long pageId, byte depth, PageLinkAnalysis links) =>
        new(jobId, pageId, depth, CrawlPageStatus.Done, links, null);

    public static PageResult Skipped(Guid jobId, long pageId, byte depth, string? reason) =>
        new(jobId, pageId, depth, CrawlPageStatus.Skipped, null, reason);

    public static PageResult Failed(Guid jobId, long pageId, byte depth, string? error) =>
        new(jobId, pageId, depth, CrawlPageStatus.Failed, null, error);
}

/// <summary>
/// The message refers to a page that does not exist: a poison message. It is not retried and goes
/// straight to the error queue (DLQ).
/// </summary>
internal sealed class UnknownPageException(Guid jobId, long pageId)
    : Exception($"Page {pageId} of job {jobId} does not exist.");
