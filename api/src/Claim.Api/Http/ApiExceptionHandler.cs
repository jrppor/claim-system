using Claim.Application.Claims;
using Claim.Domain.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Http;

/// <summary>
/// The one place that turns the application's exceptions into ProblemDetails, so endpoints do not catch them one by one.
/// Anything not listed here is a bug: it is logged and answered as a 500 without leaking details.
/// </summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Lets the UI tell a stale-version 409 from a forbidden status move.</summary>
    public const string VersionConflictType = "urn:claim:version-conflict";

    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var problem = exception switch
        {
            ClaimValidationException ex => new HttpValidationProblemDetails(ex.Errors.ToDictionary(e => e.Key, e => e.Value))
                { Status = StatusCodes.Status400BadRequest, Title = "One or more validation errors occurred." },
            ClaimRuleException ex => Problem(StatusCodes.Status409Conflict, "Invalid status change", ex.Message),
            ConcurrencyException ex => Problem(StatusCodes.Status409Conflict, "The Claim was changed by someone else", ex.Message, VersionConflictType),
            InvalidVersionException ex => Problem(StatusCodes.Status400BadRequest, "Invalid Claim version", ex.Message),
            VersionRequiredException ex => Problem(StatusCodes.Status428PreconditionRequired, "Claim version required", ex.Message),
            _ => null,
        };
        if (problem is null)
        {
            logger.LogError(exception, "Unhandled exception");
            problem = Problem(StatusCodes.Status500InternalServerError, "An unexpected error occurred.", detail: null);
        }

        http.Response.StatusCode = problem.Status!.Value;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem });
    }

    private static ProblemDetails Problem(int status, string title, string? detail, string? type = null) =>
        new() { Status = status, Title = title, Detail = detail, Type = type };
}
