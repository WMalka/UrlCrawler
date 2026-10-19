using AngleSharp.Html.Parser;

namespace Crawler.Worker.Fetching;

/// <param name="BaseHref">The document's &lt;base href&gt; resolved to an absolute http(s) URL, if present.</param>
internal sealed record ExtractedLinks(IReadOnlyList<string> Hrefs, Uri? BaseHref);

internal sealed class HtmlLinkExtractor
{
    /// <summary>Raw href values of &lt;a&gt; and &lt;area&gt; elements; normalization happens in the domain.</summary>
    /// <param name="html">Raw bytes; AngleSharp detects the encoding (BOM, meta charset).</param>
    public ExtractedLinks Extract(byte[] html, Uri pageUrl)
    {
        var parser = new HtmlParser();
        using var stream = new MemoryStream(html, writable: false);
        using var document = parser.ParseDocument(stream);

        var hrefs = document.QuerySelectorAll("a[href], area[href]")
            .Select(element => element.GetAttribute("href"))
            .OfType<string>()
            .ToList();

        var baseValue = document.QuerySelector("base[href]")?.GetAttribute("href");
        Uri? baseHref = null;
        if (!string.IsNullOrWhiteSpace(baseValue)
            && Uri.TryCreate(pageUrl, baseValue.Trim(), out var resolved)
            && (resolved.Scheme == Uri.UriSchemeHttp || resolved.Scheme == Uri.UriSchemeHttps))
        {
            baseHref = resolved;
        }

        return new ExtractedLinks(hrefs, baseHref);
    }
}
