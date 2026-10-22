using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Http;

public static class ControllerExtensions
{
    /// <summary>A 400 ProblemDetails listing field errors, the same shape the exception handler writes for ClaimValidationException.</summary>
    public static IActionResult ValidationError(this ControllerBase controller, IDictionary<string, string[]> errors) =>
        controller.ValidationProblem(new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest });

    public static IActionResult ClaimNotFound(this ControllerBase controller, long id) =>
        controller.Problem(title: "Not Found", detail: $"Claim {id} was not found.", statusCode: StatusCodes.Status404NotFound);
}
