using System.ComponentModel.DataAnnotations;

namespace Crawler.Worker;

public sealed class CrawlerWorkerOptions
{
    public const string SectionName = "Crawler";

    /// <summary>Pages processed in parallel per worker instance.</summary>
    [Range(1, 64)] public int ConcurrentMessageLimit { get; init; } = 8;

    /// <summary>Larger responses are skipped (protects memory and the parser).</summary>
    [Range(1_024, 50 * 1024 * 1024)] public int MaxResponseBytes { get; init; } = 2 * 1024 * 1024;

    /// <summary>Edges stored per page. Link counts and the ratio always use all links.</summary>
    [Range(1, 10_000)] public int MaxStoredLinksPerPage { get; init; } = 1_000;

    [Required] public string UserAgent { get; init; } = "AltevaCrawler/1.0";

    [Range(1, 120)] public int AttemptTimeoutSeconds { get; init; } = 10;
    [Range(1, 300)] public int TotalTimeoutSeconds { get; init; } = 45;
    [Range(0, 10)] public int MaxRetryAttempts { get; init; } = 3;
    [Range(0, 20)] public int MaxRedirects { get; init; } = 5;
}
