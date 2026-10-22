using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Http;

public static class ControllerExtensions
{
    /// <summary>A 400 ProblemDetails listing field errors, the same shape the exception handler writes for ClaimValidationException.</summary>
    public static IActionResult ValidationError(this ControllerBase controller, IDictionary<string, string[]> errors) =>
        controller.ValidationProblem(new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest });
}
