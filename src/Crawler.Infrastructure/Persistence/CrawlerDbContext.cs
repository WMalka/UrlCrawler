using Crawler.Domain.Jobs;
using Crawler.Domain.Pages;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Crawler.Infrastructure.Persistence;

public class CrawlerDbContext(DbContextOptions<CrawlerDbContext> options) : DbContext(options)
{
    public DbSet<CrawlJob> Jobs => Set<CrawlJob>();
    public DbSet<CrawlPage> Pages => Set<CrawlPage>();
    public DbSet<PageLink> Links => Set<PageLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrawlerDbContext).Assembly);

        // Transactional outbox/inbox: messages are published only if the DB transaction commits.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
