using Crawler.Contracts;
using Crawler.Domain.Jobs;
using Crawler.Domain.Pages;
using Crawler.IntegrationTests.Infrastructure;
using Crawler.Worker.Processing;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;

namespace Crawler.IntegrationTests;

/// <summary>
/// End-to-end worker behaviour against the fixture site (Fixtures/site):
///   /        → /a, /b, /doc.pdf, /missing, external   (5 links, 4 internal)
///   /a       → /, /a/1, /b  via &lt;base href="/a/"&gt;    (3 links, 3 internal)
///   /b       → no links
///   /a/1     → /a/2 (depth 3, not crawled), external   (2 links, 1 internal)
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class WorkerPipelineTests(SqlServerFixture sql) : IAsyncLifetime
{
    private WorkerTestHost _host = null!;

    public async Task InitializeAsync() => _host = await WorkerTestHost.StartAsync(sql.CreateDatabaseConnectionString());

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Crawl_builds_expected_pages_edges_and_ratios_within_max_depth()
    {
        var (jobId, rootPageId) = await _host.SeedJobAsync(maxDepth: 2);

        await _host.PublishAsync(jobId, rootPageId);
        var job = await _host.WaitForJobToFinishAsync(jobId);

        Assert.Equal(CrawlJobStatus.Completed, job.Status);
        Assert.NotNull(job.StartedAt);
        Assert.NotNull(job.CompletedAt);

        var pages = await LoadPagesAsync(jobId);
        Assert.Equal(
            ["https://fixture.test/", "https://fixture.test/a", "https://fixture.test/a/1",
             "https://fixture.test/b", "https://fixture.test/doc.pdf", "https://fixture.test/missing"],
            pages.Keys.Order());

        AssertPage(pages["https://fixture.test/"], CrawlPageStatus.Done, depth: 0, links: 5, internalLinks: 4, ratio: 0.8m);
        AssertPage(pages["https://fixture.test/a"], CrawlPageStatus.Done, depth: 1, links: 3, internalLinks: 3, ratio: 1m);
        AssertPage(pages["https://fixture.test/b"], CrawlPageStatus.Done, depth: 1, links: 0, internalLinks: 0, ratio: 0m);
        AssertPage(pages["https://fixture.test/a/1"], CrawlPageStatus.Done, depth: 2, links: 2, internalLinks: 1, ratio: 0.5m);

        Assert.Equal(CrawlPageStatus.Skipped, pages["https://fixture.test/doc.pdf"].Status);
        Assert.Equal(CrawlPageStatus.Failed, pages["https://fixture.test/missing"].Status);
        Assert.Contains("404", pages["https://fixture.test/missing"].Error);

        // Tree: /a/1 was discovered by /a; depth-3 links are recorded as edges but never crawled.
        Assert.Equal(pages["https://fixture.test/a"].Id, pages["https://fixture.test/a/1"].ParentPageId);
        Assert.False(_host.Site.RequestCounts.ContainsKey("/a/2"));
        Assert.Equal(10, await CountLinksAsync(jobId));
    }

    [Fact]
    public async Task Same_message_delivered_concurrently_creates_no_duplicate_pages_or_edges()
    {
        var (jobId, rootPageId) = await _host.SeedJobAsync(maxDepth: 2);

        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => _host.PublishAsync(jobId, rootPageId)));
        var job = await _host.WaitForJobToFinishAsync(jobId);

        Assert.Equal(CrawlJobStatus.Completed, job.Status);
        Assert.True(await _host.Harness.Consumed.Any<CrawlPageRequested>(x => x.Context.Message.PageId == rootPageId));

        var pages = await LoadPagesAsync(jobId);
        Assert.Equal(6, pages.Count);
        Assert.Equal(10, await CountLinksAsync(jobId));
        Assert.Equal(5, pages["https://fixture.test/"].LinkCount);
    }

    [Fact]
    public async Task Redelivery_after_completion_changes_nothing()
    {
        var (jobId, rootPageId) = await _host.SeedJobAsync(maxDepth: 1);
        await _host.PublishAsync(jobId, rootPageId);
        await _host.WaitForJobToFinishAsync(jobId);
        var pagesBefore = await LoadPagesAsync(jobId);
        var linksBefore = await CountLinksAsync(jobId);

        await _host.PublishAsync(jobId, rootPageId);
        Assert.True(await _host.Harness.Consumed.Any<CrawlPageRequested>(
            x => x.Context.Message.PageId == rootPageId && x.Context.MessageId != null));
        await Task.Delay(500);

        Assert.Equal(pagesBefore.Count, (await LoadPagesAsync(jobId)).Count);
        Assert.Equal(linksBefore, await CountLinksAsync(jobId));
    }

    [Fact]
    public async Task Max_pages_limit_is_respected()
    {
        var (jobId, rootPageId) = await _host.SeedJobAsync(maxDepth: 2, maxPages: 3);

        await _host.PublishAsync(jobId, rootPageId);
        var job = await _host.WaitForJobToFinishAsync(jobId);

        Assert.Equal(CrawlJobStatus.Completed, job.Status);
        var pages = await LoadPagesAsync(jobId);
        Assert.Equal(3, pages.Count);
        // The budget is taken in document order: root, then /a and /b.
        Assert.Contains("https://fixture.test/a", pages.Keys);
        Assert.Contains("https://fixture.test/b", pages.Keys);
    }

    [Fact]
    public async Task Max_depth_zero_crawls_only_the_start_page_but_records_its_links()
    {
        var (jobId, rootPageId) = await _host.SeedJobAsync(maxDepth: 0);

        await _host.PublishAsync(jobId, rootPageId);
        var job = await _host.WaitForJobToFinishAsync(jobId);

        Assert.Equal(CrawlJobStatus.Completed, job.Status);
        Assert.Single(await LoadPagesAsync(jobId));
        Assert.Equal(5, await CountLinksAsync(jobId));
    }

    [Fact]
    public async Task Unreachable_start_page_fails_the_job_with_reason()
    {
        var (jobId, rootPageId) = await _host.SeedJobAsync(path: "/missing");

        await _host.PublishAsync(jobId, rootPageId);
        var job = await _host.WaitForJobToFinishAsync(jobId);

        Assert.Equal(CrawlJobStatus.Failed, job.Status);
        Assert.Contains("HTTP 404", job.FailureReason);
    }

    [Fact]
    public async Task Canceled_job_is_not_crawled()
    {
        var (jobId, rootPageId) = await _host.SeedJobAsync();
        await _host.WithDbAsync(db => db.Jobs.Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, CrawlJobStatus.Canceled)));

        await _host.PublishAsync(jobId, rootPageId);
        Assert.True(await _host.Harness.Consumed.Any<CrawlPageRequested>(x => x.Context.Message.JobId == jobId));

        var root = (await LoadPagesAsync(jobId)).Values.Single();
        Assert.Equal(CrawlPageStatus.Queued, root.Status);
        Assert.Empty(_host.Site.RequestCounts);
    }

    [Fact]
    public async Task Message_for_unknown_page_is_not_retried_and_raises_a_fault()
    {
        var message = new CrawlPageRequested(Guid.NewGuid(), 987_654);

        await _host.Harness.Bus.Publish(message);

        Assert.True(await _host.Harness.Published.Any<Fault<CrawlPageRequested>>(
            x => x.Context.Message.Message.PageId == message.PageId));
        var attempts = _host.Harness.Consumed.Select<CrawlPageRequested>(x => x.Context.Message.PageId == message.PageId).ToList();
        var attempt = Assert.Single(attempts);
        Assert.True(attempt.Exception is UnknownPageException, attempt.Exception?.ToString());
    }

    private Task<Dictionary<string, CrawlPage>> LoadPagesAsync(Guid jobId) =>
        _host.WithDbAsync(db => db.Pages.AsNoTracking().Where(p => p.JobId == jobId).ToDictionaryAsync(p => p.Url));

    private Task<int> CountLinksAsync(Guid jobId) =>
        _host.WithDbAsync(db => db.Links.CountAsync(l => db.Pages.Any(p => p.Id == l.SourcePageId && p.JobId == jobId)));

    private static void AssertPage(CrawlPage page, CrawlPageStatus status, int depth, int links, int internalLinks, decimal ratio)
    {
        Assert.Equal(status, page.Status);
        Assert.Equal(depth, page.Depth);
        Assert.Equal(links, page.LinkCount);
        Assert.Equal(internalLinks, page.InternalLinkCount);
        Assert.Equal(ratio, page.DomainLinkRatio);
    }
}
