using Crawler.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crawler.Infrastructure.Persistence.Configurations;

internal sealed class CrawlJobConfiguration : IEntityTypeConfiguration<CrawlJob>
{
    public void Configure(EntityTypeBuilder<CrawlJob> builder)
    {
        builder.ToTable("CrawlJobs");
        builder.HasKey(job => job.Id);

        builder.Property(job => job.Url).HasMaxLength(CrawlJob.UrlMaxLength).IsRequired();
        builder.Property(job => job.FailureReason).HasMaxLength(CrawlJob.FailureReasonMaxLength);
        builder.Property(job => job.CreatedAt).HasPrecision(3);
        builder.Property(job => job.StartedAt).HasPrecision(3);
        builder.Property(job => job.CompletedAt).HasPrecision(3);
        builder.Ignore(job => job.StartHost);

        // Covers the paginated history query (newest first) without key lookups.
        builder.HasIndex(job => job.CreatedAt)
            .IsDescending()
            .IncludeProperties(job => new { job.Url, job.Status, job.CompletedAt });
    }
}
