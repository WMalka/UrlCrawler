using Crawler.Contracts;
using Crawler.Infrastructure.Persistence;
using MassTransit;

namespace Crawler.Worker.Processing;

/// <summary>
/// A page message that ended in the DLQ would leave its page Queued and the job Running forever.
/// This consumer marks the page Failed so the job can still finish.
/// </summary>
internal sealed class CrawlPageFaultConsumer(CrawlResultWriter writer, ILogger<CrawlPageFaultConsumer> logger)
    : IConsumer<Fault<CrawlPageRequested>>
{
    private const int UnusedDepth = 0; // failed pages never enqueue children

    public async Task Consume(ConsumeContext<Fault<CrawlPageRequested>> context)
    {
        var message = context.Message.Message;
        using var scope = logger.BeginScope(LogScope.For(context, message.JobId, message.PageId));

        var exceptionType = context.Message.Exceptions.FirstOrDefault()?.ExceptionType ?? "unknown";
        logger.LogError("Page {PageId} of job {JobId} moved to the error queue ({ExceptionType})",
            message.PageId, message.JobId, exceptionType);

        if (exceptionType == typeof(UnknownPageException).FullName)
            return;

        var result = PageResult.Failed(message.JobId, message.PageId, UnusedDepth,
            $"Processing failed after retries ({exceptionType}).");
        await writer.SaveAsync(result, context, context.CancellationToken);
    }
}

internal sealed class CrawlPageFaultConsumerDefinition : ConsumerDefinition<CrawlPageFaultConsumer>
{
    public CrawlPageFaultConsumerDefinition() => EndpointName = "crawl-page-faults";

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<CrawlPageFaultConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseCrawlerRetryPolicy();
        endpointConfigurator.UseEntityFrameworkOutbox<CrawlerDbContext>(context);
    }
}
