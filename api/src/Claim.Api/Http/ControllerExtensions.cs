using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Http;

/// <summary>
/// Error responses written by the same <c>Results.Problem</c> / <c>Results.ValidationProblem</c> code the API has always used,
/// so the bytes clients see (field order, content type, ProblemDetails defaults) did not change when endpoints became controllers.
/// </summary>
public static class ControllerExtensions
{
    /// <summary>A 400 ProblemDetails listing field errors, the same shape the exception handler writes for ClaimValidationException.</summary>
    public static IActionResult ValidationError(this ControllerBase controller, IDictionary<string, string[]> errors)
    {
        // Filled in field by field (not via ModelState) so the errors keep the order the controller added them in.
        var problem = controller.ProblemDetailsFactory.CreateValidationProblemDetails(
            controller.HttpContext, new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary(), StatusCodes.Status400BadRequest);
        foreach (var (field, messages) in errors) problem.Errors[field] = messages;
        return controller.ValidationProblem(problem);
    }

    public static IActionResult ProblemResult(this ControllerBase controller, string title, string? detail, int statusCode) =>
        new ResultActionResult(Results.Problem(title: title, detail: detail, statusCode: statusCode));

    public static IActionResult ClaimNotFound(this ControllerBase controller, long id) =>
        controller.ProblemResult("Not Found", $"Claim {id} was not found.", StatusCodes.Status404NotFound);

    private sealed class ResultActionResult(IResult result) : IActionResult
    {
        public Task ExecuteResultAsync(ActionContext context) => result.ExecuteAsync(context.HttpContext);
    }
}
