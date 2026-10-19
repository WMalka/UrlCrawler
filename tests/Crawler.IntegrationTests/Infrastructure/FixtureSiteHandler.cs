using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace Crawler.IntegrationTests.Infrastructure;

/// <summary>
/// Serves the HTML files in Fixtures/site as https://fixture.test, replacing the network.
/// Unknown paths return 404; /doc.pdf returns a non-HTML response.
/// </summary>
public sealed class FixtureSiteHandler : HttpMessageHandler
{
    public const string Host = "fixture.test";
    public static readonly Uri BaseUri = new($"https://{Host}/");

    private static readonly Dictionary<string, string> PagesByPath = new(StringComparer.Ordinal)
    {
        ["/"] = "index.html",
        ["/a"] = "a.html",
        ["/b"] = "b.html",
        ["/a/1"] = "a-1.html"
    };

    private static readonly string SiteDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "site");

    public ConcurrentDictionary<string, int> RequestCounts { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        RequestCounts.AddOrUpdate(path, 1, (_, count) => count + 1);

        if (request.RequestUri.Host != Host)
            return new HttpResponseMessage(HttpStatusCode.BadGateway) { RequestMessage = request };

        if (path == "/doc.pdf")
        {
            var pdf = new ByteArrayContent("%PDF-1.4"u8.ToArray());
            pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = pdf, RequestMessage = request };
        }

        if (!PagesByPath.TryGetValue(path, out var file))
            return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };

        var html = new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine(SiteDirectory, file), cancellationToken));
        html.Headers.ContentType = new MediaTypeHeaderValue("text/html") { CharSet = "utf-8" };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = html, RequestMessage = request };
    }
}
