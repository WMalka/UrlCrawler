using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Crawler.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef` create migrations without a startup project or a running database.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CrawlerDbContext>
{
    public CrawlerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CrawlerDbContext>()
            .UseSqlServer("Server=localhost;Database=Crawler;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        return new CrawlerDbContext(options);
    }
}
