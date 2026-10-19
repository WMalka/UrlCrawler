namespace Crawler.Domain.Pages;

public enum CrawlPageStatus : byte
{
    Queued = 0,
    Done = 1,
    /// <summary>Fetched but not crawlable (e.g. non-HTML content). Not an error.</summary>
    Skipped = 2,
    Failed = 3
}
