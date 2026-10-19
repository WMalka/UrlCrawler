using Crawler.Domain.Links;

namespace Crawler.UnitTests.Links;

public class UrlNormalizerTests
{
    private static readonly Uri PageUri = new("https://example.com/docs/guide/intro.html");

    [Theory]
    [InlineData("page.html", "https://example.com/docs/guide/page.html")]
    [InlineData("./page.html", "https://example.com/docs/guide/page.html")]
    [InlineData("../api/", "https://example.com/docs/api/")]
    [InlineData("/about", "https://example.com/about")]
    [InlineData("//cdn.example.org/lib", "https://cdn.example.org/lib")]
    [InlineData("?q=1", "https://example.com/docs/guide/intro.html?q=1")]
    public void Resolves_relative_links_against_base(string href, string expected)
    {
        Assert.True(UrlNormalizer.TryNormalize(href, PageUri, out var result));
        Assert.Equal(expected, result.AbsoluteUri);
    }

    [Theory]
    [InlineData("#section", "https://example.com/docs/guide/intro.html")]
    [InlineData("other.html#top", "https://example.com/docs/guide/other.html")]
    [InlineData("https://example.com/a?x=1#frag", "https://example.com/a?x=1")]
    public void Removes_fragment(string href, string expected)
    {
        Assert.True(UrlNormalizer.TryNormalize(href, PageUri, out var result));
        Assert.Equal(expected, result.AbsoluteUri);
    }

    [Theory]
    [InlineData("HTTPS://Example.COM/Path", "https://example.com/Path")]
    [InlineData("https://example.com:443/a", "https://example.com/a")]
    [InlineData("http://example.com:80/a", "http://example.com/a")]
    [InlineData("http://example.com:8080/a", "http://example.com:8080/a")]
    [InlineData("https://example.com", "https://example.com/")]
    [InlineData("https://user:secret@example.com/a", "https://example.com/a")]
    [InlineData("  https://example.com/a  ", "https://example.com/a")]
    public void Canonicalizes_absolute_urls(string url, string expected)
    {
        Assert.True(UrlNormalizer.TryNormalize(url, out var result));
        Assert.Equal(expected, result.AbsoluteUri);
    }

    [Fact]
    public void Equivalent_urls_normalize_to_the_same_value()
    {
        string[] variants =
        [
            "https://EXAMPLE.com:443/docs/../docs/a#x",
            "/docs/a",
            "a#y"
        ];
        var baseUri = new Uri("https://example.com/docs/");

        var normalized = variants
            .Select(v => UrlNormalizer.TryNormalize(v, baseUri, out var u) ? u.AbsoluteUri : null)
            .Distinct()
            .ToList();

        Assert.Equal(["https://example.com/docs/a"], normalized);
    }

    [Theory]
    [InlineData("mailto:someone@example.com")]
    [InlineData("tel:+123456")]
    [InlineData("javascript:void(0)")]
    [InlineData("data:text/html,hi")]
    [InlineData("ftp://example.com/file")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_non_http_or_empty_links(string? href)
    {
        Assert.False(UrlNormalizer.TryNormalize(href, PageUri, out _));
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("/relative/without/base")]
    [InlineData("not a url")]
    public void Rejects_non_absolute_urls_without_base(string url)
    {
        Assert.False(UrlNormalizer.TryNormalize(url, out _));
    }
}
