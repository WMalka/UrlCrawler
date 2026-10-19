namespace Crawler.Domain.Links;

public sealed record DiscoveredLink(Uri Url, bool IsInternal);

public sealed record PageLinkAnalysis(IReadOnlyList<DiscoveredLink> Links)
{
    public int InternalCount => Links.Count(link => link.IsInternal);
    public decimal Ratio => DomainLinkRatio.Calculate(InternalCount, Links.Count);
}

public static class PageLinkAnalyzer
{
    /// <summary>
    /// Normalizes raw hrefs, drops non-http(s) ones (mailto:, tel:, javascript:, ...),
    /// links back to the page itself (e.g. "#top") and duplicates.
    /// Internal = same host as the job's start URL (exact, case-insensitive).
    /// </summary>
    /// <param name="pageUrl">Normalized URL of the page the hrefs were found on.</param>
    /// <param name="baseUri">The page's &lt;base href&gt;, if any; otherwise <paramref name="pageUrl"/> is the base.</param>
    public static PageLinkAnalysis Analyze(
        IEnumerable<string?> hrefs, Uri pageUrl, string startHost, Uri? baseUri = null)
    {
        ArgumentNullException.ThrowIfNull(hrefs);
        ArgumentNullException.ThrowIfNull(pageUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(startHost);

        var seen = new HashSet<string>(StringComparer.Ordinal) { pageUrl.AbsoluteUri };
        var links = new List<DiscoveredLink>();

        foreach (var href in hrefs)
        {
            if (!UrlNormalizer.TryNormalize(href, baseUri ?? pageUrl, out var url) || !seen.Add(url.AbsoluteUri))
                continue;

            links.Add(new DiscoveredLink(url, IsInternal(url, startHost)));
        }

        return new PageLinkAnalysis(links);
    }

    public static bool IsInternal(Uri url, string startHost) =>
        string.Equals(url.Host, startHost, StringComparison.OrdinalIgnoreCase);
}
