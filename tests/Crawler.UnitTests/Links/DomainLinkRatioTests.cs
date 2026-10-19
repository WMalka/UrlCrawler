using Crawler.Domain.Links;

namespace Crawler.UnitTests.Links;

public class DomainLinkRatioTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 5, 0)]
    [InlineData(5, 5, 1)]
    [InlineData(1, 4, 0.25)]
    public void Calculates_internal_over_total(int internalLinks, int totalLinks, double expected)
    {
        Assert.Equal((decimal)expected, DomainLinkRatio.Calculate(internalLinks, totalLinks));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(3, 2)]
    public void Rejects_invalid_counts(int internalLinks, int totalLinks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DomainLinkRatio.Calculate(internalLinks, totalLinks));
    }
}
