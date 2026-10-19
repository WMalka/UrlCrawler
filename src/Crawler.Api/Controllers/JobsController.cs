using Crawler.Api.Jobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Crawler.Api.Controllers;

[ApiController]
[Route("api/jobs")]
[Produces("application/json")]
public sealed class JobsController(JobCommands commands, JobQueries queries) : ControllerBase
{
    public const string CreateJobRateLimitPolicy = "create-job";

    [HttpPost]
    [EnableRateLimiting(CreateJobRateLimitPolicy)]
    [ProducesResponseType<CreateJobResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        CreateJobRequest request, [FromServices] CreateJobRequestValidator validator, CancellationToken cancellationToken)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            foreach (var (field, messages) in validation.Errors)
                foreach (var message in messages)
                    ModelState.AddModelError(field, message);
            return ValidationProblem(ModelState);
        }

        var jobId = await commands.CreateAsync(validation.Url!, validation.MaxDepth, cancellationToken);
        return AcceptedAtAction(nameof(Get), new { jobId }, new CreateJobResponse(jobId));
    }

    [HttpGet("{jobId:guid}")]
    [ProducesResponseType<JobWithPageCountsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid jobId, CancellationToken cancellationToken) =>
        OkOrNotFound(await queries.GetJobWithPageCountsAsync(jobId, cancellationToken));

    /// <summary>Hierarchical result; available once the job has completed (409 before that).</summary>
    [HttpGet("{jobId:guid}/tree")]
    [ProducesResponseType<JobTreeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetTree(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await queries.GetTreeAsync(jobId, cancellationToken);
        return result.Availability switch
        {
            JobTreeAvailability.Available => Ok(result.Tree),
            JobTreeAvailability.NotCompleted => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Result tree is not available yet.",
                detail: $"The tree is available once the job has completed. Current status: {result.JobStatus}."),
            _ => NotFoundProblem()
        };
    }

    [HttpGet("{jobId:guid}/pages/{pageId:long}/links")]
    [ProducesResponseType<IReadOnlyList<PageLinkDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPageLinks(Guid jobId, long pageId, CancellationToken cancellationToken) =>
        OkOrNotFound(await queries.GetLinksAsync(jobId, pageId, cancellationToken));

    /// <summary>Job history, most recent first.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<JobSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetHistory(
        [FromServices] IOptions<CrawlJobOptions> options,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        var maxPageSize = options.Value.MaxHistoryPageSize;
        if (page < 1)
            ModelState.AddModelError(nameof(page), "Page must be 1 or greater.");
        if (pageSize < 1 || pageSize > maxPageSize)
            ModelState.AddModelError(nameof(pageSize), $"Page size must be between 1 and {maxPageSize}.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return Ok(await queries.GetHistoryAsync(page, pageSize, cancellationToken));
    }

    [HttpPost("{jobId:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid jobId, CancellationToken cancellationToken) =>
        await commands.CancelAsync(jobId, cancellationToken) switch
        {
            CancelJobResult.Canceled => NoContent(),
            CancelJobResult.NotFound => NotFoundProblem(),
            _ => Problem(statusCode: StatusCodes.Status409Conflict, title: "Job has already finished.")
        };

    private IActionResult OkOrNotFound(object? result) => result is null ? NotFoundProblem() : Ok(result);

    private ObjectResult NotFoundProblem() => Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found.");
}
