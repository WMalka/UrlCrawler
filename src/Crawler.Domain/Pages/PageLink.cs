namespace Crawler.Domain.Pages;

/// <summary>An outgoing link (edge) from a crawled page. Targets may be external and never crawled.</summary>
public class PageLink
{
    private PageLink() { }

    public long Id { get; private set; }
    public long SourcePageId { get; private set; }
    public string TargetUrl { get; private set; } = string.Empty;
    /// <summary>Persisted computed column (SHA-256 of <see cref="TargetUrl"/>) used for de-duplication.</summary>
    public byte[] TargetUrlHash { get; private set; } = [];
    public bool IsInternal { get; private set; }
}
