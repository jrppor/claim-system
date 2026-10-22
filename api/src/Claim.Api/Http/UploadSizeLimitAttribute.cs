using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Claim.Api.Http;

/// <summary>
/// Caps the size of a request body for one action, and answers a body over the cap as a 413 ProblemDetails (not Kestrel's bare
/// 413). A declared Content-Length over the cap is refused here, before the form is read; a body that only turns out too big
/// while streaming (chunked, or a lying length) is stopped by the server limit set here and written as the same ProblemDetails
/// by <see cref="ApiExceptionHandler"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class UploadSizeLimitAttribute(long maxBytes) : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var http = context.HttpContext;
        if (http.Request.ContentLength > maxBytes)
        {
            context.Result = new ObjectResult(PayloadTooLarge.Problem(maxBytes)) { StatusCode = StatusCodes.Status413PayloadTooLarge };
            return;
        }
        var limit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = maxBytes;
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}

/// <summary>The one description of a "request body too large" answer, shared by the size filter and the exception handler.</summary>
public static class PayloadTooLarge
{
    public static ProblemDetails Problem(long? maxBytes) => new()
    {
        Status = StatusCodes.Status413PayloadTooLarge,
        Title = "The request is too large",
        Detail = maxBytes is { } max ? $"The request body may be at most {max / (1024 * 1024)} MB." : "The request body is larger than the server accepts.",
    };
}
