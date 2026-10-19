using System.Data;
using Crawler.Contracts;
using Crawler.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crawler.Infrastructure.Messaging;

public static class MessagingExtensions
{
    /// <summary>
    /// RabbitMQ transport with the EF Core transactional outbox shared by API and worker.
    /// </summary>
    /// <param name="configureBus">Process-specific registrations (consumers, endpoint settings).</param>
    public static IServiceCollection AddCrawlerMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Every message carries the job id as CorrelationId, so logs and traces group by job.
        MessageCorrelation.UseCorrelationId<CrawlPageRequested>(message => message.JobId);

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            bus.AddEntityFrameworkOutbox<CrawlerDbContext>(outbox =>
            {
                outbox.UseSqlServer();
                // Messages published outside a consumer (API job creation) are stored in the
                // caller's transaction and delivered after commit.
                outbox.UseBusOutbox();
                // Reads inside consumers must not hold shared locks while the page is fetched (ADR-001).
                outbox.IsolationLevel = IsolationLevel.ReadCommitted;
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
            });

            configureBus?.Invoke(bus);

            bus.UsingRabbitMq((context, transport) =>
            {
                var rabbit = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                transport.Host(rabbit.Host, rabbit.VirtualHost, host =>
                {
                    host.Username(rabbit.Username);
                    host.Password(rabbit.Password);
                });
                transport.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
