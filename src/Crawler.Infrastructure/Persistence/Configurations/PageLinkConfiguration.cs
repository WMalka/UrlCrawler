using Crawler.Domain.Jobs;
using Crawler.Domain.Pages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crawler.Infrastructure.Persistence.Configurations;

internal sealed class PageLinkConfiguration : IEntityTypeConfiguration<PageLink>
{
    public void Configure(EntityTypeBuilder<PageLink> builder)
    {
        builder.ToTable("PageLinks");
        builder.HasKey(link => link.Id);

        builder.Property(link => link.TargetUrl).HasMaxLength(CrawlJob.UrlMaxLength).IsRequired();
        builder.Property(link => link.TargetUrlHash)
            .HasColumnType(Sha256Column.ColumnType)
            .HasComputedColumnSql(Sha256Column.Of(nameof(PageLink.TargetUrl)), stored: true);

        builder.HasOne<CrawlPage>().WithMany()
            .HasForeignKey(link => link.SourcePageId)
            .OnDelete(DeleteBehavior.Cascade);

        // One edge per (source, target): a redelivered message cannot duplicate edges.
        builder.HasIndex(link => new { link.SourcePageId, link.TargetUrlHash }).IsUnique();
    }
}
