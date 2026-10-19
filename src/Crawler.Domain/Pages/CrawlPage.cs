using Crawler.Domain.Links;

namespace Crawler.Domain.Pages;

public class CrawlPage
{
    public const int ErrorMaxLength = 500;

    private CrawlPage() { }

    public long Id { get; private set; }
    public Guid JobId { get; private set; }
    /// <summary>The page that first discovered this one; null for the root. Defines the result tree.</summary>
    public long? ParentPageId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    /// <summary>Persisted computed column (SHA-256 of <see cref="Url"/>) used for de-duplication.</summary>
    public byte[] UrlHash { get; private set; } = [];
    public byte Depth { get; private set; }
    public CrawlPageStatus Status { get; private set; }
    public int? LinkCount { get; private set; }
    public int? InternalLinkCount { get; private set; }
    public string? Error { get; private set; }

    public decimal? DomainLinkRatio =>
        Status == CrawlPageStatus.Done && LinkCount is { } total && InternalLinkCount is { } @internal
            ? Links.DomainLinkRatio.Calculate(@internal, total)
            : null;

    public static CrawlPage CreateRoot(Guid jobId, Uri normalizedUrl) => new()
    {
        JobId = jobId,
        Url = normalizedUrl.AbsoluteUri,
        Depth = 0,
        Status = CrawlPageStatus.Queued
    };
}
