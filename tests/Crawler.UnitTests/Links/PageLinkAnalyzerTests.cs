using Crawler.Domain.Links;

namespace Crawler.UnitTests.Links;

public class PageLinkAnalyzerTests
{
    private static readonly Uri PageUri = new("https://example.com/blog/");
    private const string StartHost = "example.com";

    [Fact]
    public void Classifies_internal_and_external_links_and_computes_ratio()
    {
        string[] hrefs =
        [
            "/about",                       // internal
            "post-1",                       // internal
            "https://EXAMPLE.com/contact",  // internal (host is case-insensitive)
            "https://www.example.com/",     // external: exact host match only
            "https://other.org/"            // external
        ];

        var analysis = PageLinkAnalyzer.Analyze(hrefs, PageUri, StartHost);

        Assert.Equal(5, analysis.Links.Count);
        Assert.Equal(3, analysis.InternalCount);
        Assert.Equal(0.6m, analysis.Ratio);
    }

    [Fact]
    public void Ignores_non_http_schemes_and_deduplicates_after_normalization()
    {
        string?[] hrefs =
        [
            "mailto:me@example.com",
            "tel:123",
            "javascript:void(0)",
            null,
            "/about",
            "/about#team",
            "https://example.com:443/about"
        ];

        var analysis = PageLinkAnalyzer.Analyze(hrefs, PageUri, StartHost);

        var link = Assert.Single(analysis.Links);
        Assert.Equal("https://example.com/about", link.Url.AbsoluteUri);
        Assert.True(link.IsInternal);
    }

    [Fact]
    public void Page_without_http_links_has_ratio_zero()
    {
        var analysis = PageLinkAnalyzer.Analyze(["mailto:x@y.z", "tel:1"], PageUri, StartHost);

        Assert.Empty(analysis.Links);
        Assert.Equal(0m, analysis.Ratio);
    }

    [Fact]
    public void Ignores_links_back_to_the_page_itself()
    {
        string[] hrefs = ["#top", "", "./", "https://EXAMPLE.com:443/blog/#comments", "/other"];

        var analysis = PageLinkAnalyzer.Analyze(hrefs, PageUri, StartHost);

        var link = Assert.Single(analysis.Links);
        Assert.Equal("https://example.com/other", link.Url.AbsoluteUri);
    }

    [Fact]
    public void Resolves_relative_links_against_base_href_when_present()
    {
        var baseHref = new Uri("https://example.com/static/");

        var analysis = PageLinkAnalyzer.Analyze(["img.html"], PageUri, StartHost, baseHref);

        Assert.Equal("https://example.com/static/img.html", Assert.Single(analysis.Links).Url.AbsoluteUri);
    }
}
