using System.ComponentModel.DataAnnotations;

namespace Crawler.Api.Jobs;

/// <summary>Defaults and bounds for new jobs. Effective values are snapshotted on each job (ADR-001).</summary>
public sealed class CrawlJobOptions
{
    public const string SectionName = "CrawlJobs";

    [Range(0, 10)] public byte DefaultMaxDepth { get; init; } = 2;
    [Range(0, 10)] public byte MaxAllowedDepth { get; init; } = 5;
    [Range(1, 10_000)] public int MaxPagesPerJob { get; init; } = 200;
    [Range(1, 100)] public int MaxHistoryPageSize { get; init; } = 100;
}
