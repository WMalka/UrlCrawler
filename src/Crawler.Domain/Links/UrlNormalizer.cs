using System.Diagnostics.CodeAnalysis;

namespace Crawler.Domain.Links;

/// <summary>
/// Produces the canonical form used to de-duplicate URLs within a job:
/// absolute http(s) only, fragment and user-info removed, scheme/host lowercased,
/// default port removed, dot-segments resolved, empty path becomes "/".
/// Path and query are kept as-is because they are case- and order-sensitive on most servers.
/// </summary>
public static class UrlNormalizer
{
    private const UriComponents CanonicalComponents =
        UriComponents.SchemeAndServer | UriComponents.PathAndQuery;

    public static bool TryNormalize(string? url, [NotNullWhen(true)] out Uri? normalized) =>
        TryNormalize(url, baseUri: null, out normalized);

    /// <param name="url">Absolute or relative URL (e.g. an href value).</param>
    /// <param name="baseUri">Base for resolving relative URLs; the page URL or its &lt;base href&gt;.</param>
    public static bool TryNormalize(string? url, Uri? baseUri, [NotNullWhen(true)] out Uri? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var trimmed = url.Trim();
        var parsed = baseUri is null
            ? Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute) ? absolute : null
            : Uri.TryCreate(baseUri, trimmed, out var resolved) ? resolved : null;

        if (parsed is null || !IsHttp(parsed) || string.IsNullOrEmpty(parsed.Host))
            return false;

        normalized = new Uri(parsed.GetComponents(CanonicalComponents, UriFormat.UriEscaped));
        return true;
    }

    private static bool IsHttp(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
}
