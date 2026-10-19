using System.Text;
using Crawler.Worker.Fetching;

namespace Crawler.UnitTests.Fetching;

public class HtmlLinkExtractorTests
{
    private static readonly Uri PageUrl = new("https://example.com/docs/index.html");
    private readonly HtmlLinkExtractor _extractor = new();

    private ExtractedLinks Extract(string html) => _extractor.Extract(Encoding.UTF8.GetBytes(html), PageUrl);

    [Fact]
    public void Returns_raw_href_values_of_anchors_and_areas_in_document_order()
    {
        var result = Extract("""
            <html><body>
              <a href="/a">A</a>
              <a>no href</a>
              <map><area href="b.html"></map>
              <a href="mailto:x@y.z">mail</a>
              <link href="/style.css" rel="stylesheet">
            </body></html>
            """);

        Assert.Equal(["/a", "b.html", "mailto:x@y.z"], result.Hrefs);
        Assert.Null(result.BaseHref);
    }

    [Fact]
    public void Resolves_base_href_against_the_page_url()
    {
        var result = Extract("""<html><head><base href="/static/"></head><body><a href="x">x</a></body></html>""");

        Assert.Equal(new Uri("https://example.com/static/"), result.BaseHref);
    }

    [Fact]
    public void Ignores_non_http_base_href()
    {
        var result = Extract("""<html><head><base href="javascript:alert(1)"></head></html>""");

        Assert.Null(result.BaseHref);
    }

    [Fact]
    public void Tolerates_malformed_html()
    {
        var result = Extract("<div><a href='/one'>one<p><a href=\"/two\">two");

        // HTML5 error recovery may re-open the unclosed <a>, repeating its href; PageLinkAnalyzer de-duplicates.
        Assert.Equal(["/one", "/two"], result.Hrefs.Distinct());
    }

    [Fact]
    public void Detects_encoding_from_meta_charset()
    {
        var html = "<html><head><meta charset=\"windows-1252\"></head><body><a href=\"/café\">x</a></body></html>";
        var bytes = Encoding.Latin1.GetBytes(html);

        var result = _extractor.Extract(bytes, PageUrl);

        Assert.Equal(["/café"], result.Hrefs);
    }
}
