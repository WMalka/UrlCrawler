using Crawler.Contracts;
using Crawler.Domain.Jobs;
using Crawler.Domain.Links;
using Crawler.Domain.Pages;
using Crawler.Infrastructure.Persistence;
using Crawler.Worker.Fetching;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Crawler.Worker.Processing;

/// <summary>Crawls one page: check → fetch (no locks held) → analyze links → persist.</summary>
internal sealed class CrawlPageProcessor(
    CrawlerDbContext db,
    HtmlPageFetcher fetcher,
    HtmlLinkExtractor extractor,
    CrawlResultWriter writer,
    ILogger<CrawlPageProcessor> logger)
{
    public async Task ProcessAsync(CrawlPageRequested message, IPublishEndpoint publisher, CancellationToken cancellationToken)
    {
        // A retry re-runs this method in the same scope; start from a clean change tracker.
        db.ChangeTracker.Clear();

        var target = await db.Pages.AsNoTracking()
            .Where(page => page.Id == message.PageId && page.JobId == message.JobId)
            .Select(page => new
            {
                page.Url,
                page.Depth,
                page.Status,
                Job = db.Jobs.Where(job => job.Id == page.JobId).Select(job => new { job.Status, job.Url }).Single()
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnknownPageException(message.JobId, message.PageId);

        // Cheap pre-checks avoid re-downloading on duplicate delivery; CrawlResultWriter re-checks under the lock.
        if (target.Status != CrawlPageStatus.Queued)
        {
            logger.LogInformation("Page {PageId} already {Status} (duplicate delivery); skipped", message.PageId, target.Status);
            return;
        }
        if (target.Job.Status.IsTerminal())
        {
            logger.LogInformation("Job {JobId} is {Status}; page {PageId} skipped", message.JobId, target.Job.Status, message.PageId);
            return;
        }

        if (target.Depth == 0)
            await writer.MarkJobStartedAsync(message.JobId, cancellationToken);

        var pageUrl = new Uri(target.Url);
        var startHost = new Uri(target.Job.Url).Host;
        var fetch = await fetcher.FetchAsync(pageUrl, cancellationToken);

        var result = fetch.Outcome switch
        {
            FetchOutcome.Html => PageResult.Done(message.JobId, message.PageId, target.Depth,
                AnalyzeLinks(fetch, pageUrl, startHost)),
            FetchOutcome.NotHtml or FetchOutcome.TooLarge or FetchOutcome.RedirectNotFollowed => PageResult.Skipped(message.JobId, message.PageId, target.Depth, fetch.Error),
            _ => PageResult.Failed(message.JobId, message.PageId, target.Depth, fetch.Error)
        };

        if (await writer.SaveAsync(result, publisher, cancellationToken))
        {
            logger.LogInformation(
                "Crawled {Url}: {Status}, {LinkCount} links, {InternalLinkCount} internal, ratio {DomainLinkRatio} {Error}",
                target.Url, result.Status, result.Links?.Links.Count, result.Links?.InternalCount, result.Links?.Ratio, result.Error);
        }
    }

    private PageLinkAnalysis AnalyzeLinks(FetchResult fetch, Uri requestedUrl, string startHost)
    {
        // After a redirect, links resolve against (and self-links are detected for) the final URL.
        var pageUrl = UrlNormalizer.TryNormalize(fetch.FinalUrl?.AbsoluteUri, out var finalUrl) ? finalUrl : requestedUrl;
        var extracted = extractor.Extract(fetch.Body!, pageUrl);
        return PageLinkAnalyzer.Analyze(extracted.Hrefs, pageUrl, startHost, extracted.BaseHref);
    }
}
