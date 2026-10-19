using Crawler.Domain.Pages;

namespace Crawler.Domain.Jobs;

public class CrawlJob
{
    public const int UrlMaxLength = 2048;
    public const int FailureReasonMaxLength = 500;

    private CrawlJob() { }

    public Guid Id { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public byte MaxDepth { get; private set; }
    public int MaxPages { get; private set; }
    public CrawlJobStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? FailureReason { get; private set; }

    /// <summary>Host of the start URL; links to this host count as internal.</summary>
    public string StartHost => new Uri(Url).Host;

    // Transition rules live here; Infrastructure applies them atomically in SQL (see ADR-003).

    public bool CanEnqueueChildrenOf(byte parentDepth) => parentDepth < MaxDepth;

    public int RemainingPageSlots(int existingPageCount) => Math.Max(0, MaxPages - existingPageCount);

    /// <summary>Status the job should have after one of its pages reached a terminal page status.</summary>
    /// <param name="rootPageStatus">Current status of the job's root (depth 0) page.</param>
    /// <param name="queuedPageCount">Pages of this job still waiting to be processed.</param>
    public CrawlJobStatus ResolveStatusAfterPageFinished(CrawlPageStatus rootPageStatus, int queuedPageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(queuedPageCount);

        if (Status.IsTerminal())
            return Status;
        if (rootPageStatus is CrawlPageStatus.Failed or CrawlPageStatus.Skipped)
            return CrawlJobStatus.Failed;
        return queuedPageCount == 0 ? CrawlJobStatus.Completed : CrawlJobStatus.Running;
    }

    /// <param name="normalizedUrl">Start URL already normalized by <see cref="Links.UrlNormalizer"/>.</param>
    public static CrawlJob Create(Uri normalizedUrl, byte maxDepth, int maxPages, DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(normalizedUrl);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPages, 1);

        return new CrawlJob
        {
            Url = normalizedUrl.AbsoluteUri,
            MaxDepth = maxDepth,
            MaxPages = maxPages,
            Status = CrawlJobStatus.Pending,
            CreatedAt = createdAtUtc
        };
    }
}
