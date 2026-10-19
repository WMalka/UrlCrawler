using Crawler.Domain.Jobs;
using Crawler.Domain.Links;
using Microsoft.Extensions.Options;

namespace Crawler.Api.Jobs;

public sealed record CreateJobValidationResult(Uri? Url, byte MaxDepth, IDictionary<string, string[]> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed class CreateJobRequestValidator(IOptions<CrawlJobOptions> options)
{
    public CreateJobValidationResult Validate(CreateJobRequest request)
    {
        var limits = options.Value;
        var errors = new Dictionary<string, string[]>();

        Uri? url = null;
        if (string.IsNullOrWhiteSpace(request.Url))
            errors[nameof(request.Url)] = ["URL is required."];
        else if (request.Url.Length > CrawlJob.UrlMaxLength)
            errors[nameof(request.Url)] = [$"URL must be at most {CrawlJob.UrlMaxLength} characters."];
        else if (!UrlNormalizer.TryNormalize(request.Url, out url))
            errors[nameof(request.Url)] = ["URL must be an absolute http or https address."];

        var maxDepth = request.MaxDepth ?? limits.DefaultMaxDepth;
        if (maxDepth < 0 || maxDepth > limits.MaxAllowedDepth)
            errors[nameof(request.MaxDepth)] = [$"Max depth must be between 0 and {limits.MaxAllowedDepth}."];

        return errors.Count == 0
            ? new CreateJobValidationResult(url, (byte)maxDepth, errors)
            : new CreateJobValidationResult(null, 0, errors);
    }
}
