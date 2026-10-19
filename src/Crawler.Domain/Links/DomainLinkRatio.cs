namespace Crawler.Domain.Links;

public static class DomainLinkRatio
{
    /// <returns>internal / total, or 0 when the page has no outgoing links.</returns>
    public static decimal Calculate(int internalLinks, int totalLinks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(internalLinks);
        ArgumentOutOfRangeException.ThrowIfNegative(totalLinks);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(internalLinks, totalLinks);

        return totalLinks == 0 ? 0m : (decimal)internalLinks / totalLinks;
    }
}
