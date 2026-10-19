using Microsoft.Extensions.Options;
using Polly.Timeout;

namespace Crawler.Worker.Fetching;

/// <summary>
/// Downloads a page. Transient failures (network errors, timeouts, 5xx, 408, 429) are retried by the
/// resilience pipeline configured in <see cref="FetchingServiceCollectionExtensions"/>; whatever is
/// still failing afterwards becomes a page-level outcome rather than an exception.
/// </summary>
internal sealed class HtmlPageFetcher(HttpClient http, IOptions<CrawlerWorkerOptions> options, ILogger<HtmlPageFetcher> logger)
{
    private static readonly string[] HtmlMediaTypes = ["text/html", "application/xhtml+xml"];

    public async Task<FetchResult> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        // Also bounds reading the body, which happens after the resilience pipeline has returned.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TotalTimeoutSeconds));

        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if ((int)response.StatusCode is >= 300 and < 400)
                return FetchResult.RedirectNotFollowed((int)response.StatusCode, response.Headers.Location);
            if (!response.IsSuccessStatusCode)
                return FetchResult.HttpError((int)response.StatusCode, response.ReasonPhrase);

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is null || !HtmlMediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase))
                return FetchResult.NotHtml(mediaType);

            if (response.Content.Headers.ContentLength > settings.MaxResponseBytes)
                return FetchResult.TooLarge(settings.MaxResponseBytes);

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            var body = await ReadAtMostAsync(stream, settings.MaxResponseBytes, timeout.Token);
            if (body is null)
                return FetchResult.TooLarge(settings.MaxResponseBytes);

            return FetchResult.Html(response.RequestMessage?.RequestUri ?? url, body);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutRejectedException)
        {
            return FetchResult.NetworkError("Timed out.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogDebug(ex, "Request to {Url} failed", url);
            return FetchResult.NetworkError($"Network error: {ex.HttpRequestError}.");
        }
    }

    /// <returns>The body, or null when it exceeds <paramref name="maxBytes"/> (servers may omit or understate Content-Length).</returns>
    private static async Task<byte[]?> ReadAtMostAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }
}
