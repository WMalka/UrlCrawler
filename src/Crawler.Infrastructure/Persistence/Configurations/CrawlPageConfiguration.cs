using Crawler.Domain.Jobs;
using Crawler.Domain.Pages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crawler.Infrastructure.Persistence.Configurations;

internal sealed class CrawlPageConfiguration : IEntityTypeConfiguration<CrawlPage>
{
    public void Configure(EntityTypeBuilder<CrawlPage> builder)
    {
        builder.ToTable("CrawlPages");
        builder.HasKey(page => page.Id);

        builder.Property(page => page.Url).HasMaxLength(CrawlJob.UrlMaxLength).IsRequired();
        builder.Property(page => page.UrlHash)
            .HasColumnType(Sha256Column.ColumnType)
            .HasComputedColumnSql(Sha256Column.Of(nameof(CrawlPage.Url)), stored: true);
        builder.Property(page => page.Error).HasMaxLength(CrawlPage.ErrorMaxLength);
        builder.Ignore(page => page.DomainLinkRatio);

        builder.HasOne<CrawlJob>().WithMany()
            .HasForeignKey(page => page.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<CrawlPage>().WithMany()
            .HasForeignKey(page => page.ParentPageId)
            .OnDelete(DeleteBehavior.NoAction);

        // A normalized URL is crawled at most once per job (idempotency backstop, see ADR-001).
        builder.HasIndex(page => new { page.JobId, page.UrlHash }).IsUnique();
        // Progress counts, completion check and tree load.
        builder.HasIndex(page => new { page.JobId, page.Status });
    }
}
