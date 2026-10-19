using Crawler.Domain.Jobs;
using Crawler.Domain.Pages;

namespace Crawler.UnitTests.Jobs;

public class CrawlJobTests
{
    private static CrawlJob NewJob(byte maxDepth = 2, int maxPages = 200) =>
        CrawlJob.Create(new Uri("https://example.com/"), maxDepth, maxPages, DateTime.UtcNow);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public void Children_are_enqueued_only_below_max_depth(byte parentDepth, bool expected)
    {
        Assert.Equal(expected, NewJob(maxDepth: 2).CanEnqueueChildrenOf(parentDepth));
    }

    [Fact]
    public void Depth_zero_job_crawls_only_the_start_page()
    {
        Assert.False(NewJob(maxDepth: 0).CanEnqueueChildrenOf(0));
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(199, 1)]
    [InlineData(200, 0)]
    [InlineData(250, 0)]
    public void Remaining_slots_never_go_negative(int existing, int expected)
    {
        Assert.Equal(expected, NewJob(maxPages: 200).RemainingPageSlots(existing));
    }

    [Theory]
    [InlineData(CrawlPageStatus.Done, 3, CrawlJobStatus.Running)]
    [InlineData(CrawlPageStatus.Done, 0, CrawlJobStatus.Completed)]
    [InlineData(CrawlPageStatus.Failed, 0, CrawlJobStatus.Failed)]
    [InlineData(CrawlPageStatus.Skipped, 0, CrawlJobStatus.Failed)]
    public void Resolves_status_after_a_page_finished(
        CrawlPageStatus rootStatus, int queued, CrawlJobStatus expected)
    {
        Assert.Equal(expected, NewJob().ResolveStatusAfterPageFinished(rootStatus, queued));
    }

    [Fact]
    public void New_job_is_pending_with_normalized_url_and_start_host()
    {
        var job = CrawlJob.Create(new Uri("https://Example.com/a"), 2, 200, DateTime.UtcNow);

        Assert.Equal(CrawlJobStatus.Pending, job.Status);
        Assert.Equal("example.com", job.StartHost);
    }
}

public class CrawlJobRecordPageFinishedTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private static CrawlJob NewJob() => CrawlJob.Create(new Uri("https://example.com/"), 2, 200, Now.AddMinutes(-1));

    [Fact]
    public void Running_job_gets_started_timestamp_once()
    {
        var job = NewJob();

        job.RecordPageFinished(CrawlPageStatus.Done, null, queuedPageCount: 5, Now);
        job.RecordPageFinished(CrawlPageStatus.Done, null, queuedPageCount: 4, Now.AddSeconds(5));

        Assert.Equal(CrawlJobStatus.Running, job.Status);
        Assert.Equal(Now, job.StartedAt);
        Assert.Null(job.CompletedAt);
    }

    [Fact]
    public void Last_page_completes_the_job()
    {
        var job = NewJob();

        job.RecordPageFinished(CrawlPageStatus.Done, null, queuedPageCount: 0, Now);

        Assert.Equal(CrawlJobStatus.Completed, job.Status);
        Assert.Equal(Now, job.CompletedAt);
        Assert.Null(job.FailureReason);
    }

    [Fact]
    public void Failed_root_page_fails_the_job_with_reason()
    {
        var job = NewJob();

        job.RecordPageFinished(CrawlPageStatus.Failed, "HTTP 404 Not Found", queuedPageCount: 0, Now);

        Assert.Equal(CrawlJobStatus.Failed, job.Status);
        Assert.Equal("Start page could not be crawled: HTTP 404 Not Found", job.FailureReason);
        Assert.Equal(Now, job.CompletedAt);
    }

    [Fact]
    public void Failure_reason_is_truncated_to_column_length()
    {
        var job = NewJob();

        job.RecordPageFinished(CrawlPageStatus.Failed, new string('x', 2_000), 0, Now);

        Assert.Equal(CrawlJob.FailureReasonMaxLength, job.FailureReason!.Length);
    }

    [Fact]
    public void Terminal_job_is_not_changed()
    {
        var job = NewJob();
        job.RecordPageFinished(CrawlPageStatus.Done, null, 0, Now);

        job.RecordPageFinished(CrawlPageStatus.Failed, "late result", 0, Now.AddMinutes(1));

        Assert.Equal(CrawlJobStatus.Completed, job.Status);
        Assert.Equal(Now, job.CompletedAt);
    }
}
