using Crawler.Contracts;
using Crawler.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Options;

namespace Crawler.Worker.Processing;

internal sealed class CrawlPageConsumer(CrawlPageProcessor processor, ILogger<CrawlPageConsumer> logger)
    : IConsumer<CrawlPageRequested>
{
    public async Task Consume(ConsumeContext<CrawlPageRequested> context)
    {
        using var scope = logger.BeginScope(LogScope.For(context, context.Message.JobId, context.Message.PageId));
        // The consume context is the publisher, so child messages go through the consumer outbox.
        await processor.ProcessAsync(context.Message, context, context.CancellationToken);
    }
}

/// <summary>
/// Endpoint "crawl-page-requested"; MassTransit moves messages that still fail after retries to
/// "crawl-page-requested_error" (the DLQ) and publishes a Fault&lt;CrawlPageRequested&gt;.
/// </summary>
internal sealed class CrawlPageConsumerDefinition : ConsumerDefinition<CrawlPageConsumer>
{
    public CrawlPageConsumerDefinition(IOptions<CrawlerWorkerOptions> options)
    {
        EndpointName = "crawl-page-requested";
        ConcurrentMessageLimit = options.Value.ConcurrentMessageLimit;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<CrawlPageConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseCrawlerRetryPolicy();
        endpointConfigurator.UseEntityFrameworkOutbox<CrawlerDbContext>(context);
    }
}
