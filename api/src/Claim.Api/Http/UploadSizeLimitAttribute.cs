using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Claim.Api.Http;

/// <summary>
/// Caps the size of a request body for one action, and answers a body over the cap as a 413 ProblemDetails (not Kestrel's bare
/// 413, and not the 400 "Failed to read the request form" that form binding turns it into). A declared Content-Length over the
/// cap is refused before anything is read. Otherwise the server limit is set to the cap and the form is read here, once, so a
/// body that only turns out too big while streaming (chunked, or a lying length) is caught as a 413; model binding then
/// reuses the form already read.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class UploadSizeLimitAttribute(long maxBytes) : Attribute, IAsyncResourceFilter, IOrderedFilter
{
    /// <summary>After <c>RequestFormLimits</c> (order 900), so the form is read with that attribute's limits.</summary>
    public int Order => 1000;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (http.Request.ContentLength > maxBytes)
        {
            context.Result = TooLarge(maxBytes);
            return;
        }

        var limit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = maxBytes;

        if (http.Request.HasFormContentType)
        {
            try
            {
                await http.Request.ReadFormAsync(http.RequestAborted);
            }
            catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                context.Result = TooLarge(maxBytes);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A malformed form is model binding's to report, in its usual way.
            }
        }
        await next();
    }

    private static ObjectResult TooLarge(long maxBytes) =>
        new(PayloadTooLarge.Problem(maxBytes)) { StatusCode = StatusCodes.Status413PayloadTooLarge };
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
