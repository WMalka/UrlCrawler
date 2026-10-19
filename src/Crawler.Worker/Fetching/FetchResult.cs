namespace Crawler.Worker.Fetching;

internal enum FetchOutcome
{
    Html,
    NotHtml,
    TooLarge,
    RedirectNotFollowed,
    HttpError,
    NetworkError
}

/// <param name="FinalUrl">URL after redirects; relative links resolve against it.</param>
internal sealed record FetchResult(FetchOutcome Outcome, Uri? FinalUrl, byte[]? Body, string? Error)
{
    public static FetchResult Html(Uri finalUrl, byte[] body) => new(FetchOutcome.Html, finalUrl, body, null);
    public static FetchResult NotHtml(string? mediaType) =>
        new(FetchOutcome.NotHtml, null, null, $"Not an HTML page ({mediaType ?? "no content type"}).");
    public static FetchResult TooLarge(int maxBytes) =>
        new(FetchOutcome.TooLarge, null, null, $"Response larger than {maxBytes} bytes.");
    /// <summary>HttpClient refuses HTTPS→HTTP downgrades and stops at the redirect limit.</summary>
    public static FetchResult RedirectNotFollowed(int statusCode, Uri? location) =>
        new(FetchOutcome.RedirectNotFollowed, null, null,
            $"Redirect (HTTP {statusCode}) to {location?.ToString() ?? "unknown location"} not followed: insecure downgrade or redirect limit.");
    public static FetchResult HttpError(int statusCode, string? reason) =>
        new(FetchOutcome.HttpError, null, null, $"HTTP {statusCode} {reason}".TrimEnd());
    public static FetchResult NetworkError(string error) => new(FetchOutcome.NetworkError, null, null, error);
}
