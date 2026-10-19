using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Crawler.Worker.Fetching;

internal static class FetchingServiceCollectionExtensions
{
    public static IServiceCollection AddPageFetching(this IServiceCollection services)
    {
        services.AddSingleton<HtmlLinkExtractor>();

        services.AddHttpClient<HtmlPageFetcher>((provider, client) =>
            {
                var settings = provider.GetRequiredService<IOptions<CrawlerWorkerOptions>>().Value;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
                client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.1");
                // Timeouts are enforced by the resilience pipeline and HtmlPageFetcher.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .ConfigurePrimaryHttpMessageHandler(provider => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = Math.Max(1, provider.GetRequiredService<IOptions<CrawlerWorkerOptions>>().Value.MaxRedirects),
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            })
            // Custom pipeline instead of AddStandardResilienceHandler: its circuit breaker is per client,
            // so one failing website would block crawling of every other site.
            .AddResilienceHandler("page-fetch", (pipeline, context) =>
            {
                var settings = context.ServiceProvider.GetRequiredService<IOptions<CrawlerWorkerOptions>>().Value;
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    // Default ShouldHandle: HttpRequestException, attempt timeout, 5xx, 408, 429 (honours Retry-After).
                    MaxRetryAttempts = settings.MaxRetryAttempts,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromSeconds(1)
                });
                pipeline.AddTimeout(TimeSpan.FromSeconds(settings.AttemptTimeoutSeconds));
            });

        return services;
    }
}
