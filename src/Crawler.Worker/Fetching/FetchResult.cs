namespace Crawler.Worker.Fetching;

internal enum FetchOutcome
{
    Html,
    NotHtml,
    TooLarge,
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
    public static FetchResult HttpError(int statusCode, string? reason) =>
        new(FetchOutcome.HttpError, null, null, $"HTTP {statusCode} {reason}".TrimEnd());
    public static FetchResult NetworkError(string error) => new(FetchOutcome.NetworkError, null, null, error);
}
