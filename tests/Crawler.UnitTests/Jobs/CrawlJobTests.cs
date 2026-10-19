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
