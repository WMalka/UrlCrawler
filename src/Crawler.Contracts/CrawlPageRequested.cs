namespace Crawler.Contracts;

/// <summary>
/// Asks a worker to crawl one queued page. The database row is the source of truth for
/// URL, depth and status, so the message only identifies it. Delivered at least once.
/// </summary>
public sealed record CrawlPageRequested(Guid JobId, long PageId);
