using MassTransit;

namespace Crawler.Worker.Processing;

internal static class RetryPolicy
{
    /// <summary>
    /// Message-level retry for transient infrastructure failures (SQL deadlock/timeout, broker hiccups).
    /// HTTP failures never reach this: they are retried by the HTTP pipeline and recorded on the page.
    /// Poison messages skip the retries and go straight to the error queue.
    /// </summary>
    public static void UseCrawlerRetryPolicy(this IReceiveEndpointConfigurator endpoint) =>
        endpoint.UseMessageRetry(retry =>
        {
            retry.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15));
            retry.Ignore<UnknownPageException>();
        });
}

internal static class LogScope
{
    /// <summary>Structured log scope so every log line of a message carries job and message ids.</summary>
    public static Dictionary<string, object?> For(ConsumeContext context, Guid jobId, long pageId) => new()
    {
        ["JobId"] = jobId,
        ["PageId"] = pageId,
        ["MessageId"] = context.MessageId,
        ["CorrelationId"] = context.CorrelationId
    };
}
