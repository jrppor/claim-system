using Claim.Api.Auth;
using Claim.Api.Claims;
using Claim.Api.Email;
using Claim.Api.Users;
using Claim.Application;
using Claim.Application.Claims;
using Claim.Application.Health;
using Claim.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEmailOutboxWorker(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddClaimAuth(builder.Configuration);
builder.Services.AddSwaggerGen(o =>
{
    o.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the accessToken returned by POST /auth/login.",
    });
});

var app = builder.Build();

app.UseExceptionHandler(errors => errors.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, title) = ex switch
    {
        ConcurrencyException => (StatusCodes.Status409Conflict, "The Claim was changed by someone else"),
        InvalidVersionException => (StatusCodes.Status400BadRequest, "Invalid Claim version"),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
    };
    if (ex is not null && status == 500) Log.Error(ex, "Unhandled exception");
    ctx.Response.StatusCode = status;
    await ctx.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new()
    {
        HttpContext = ctx,
        ProblemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == 500 ? null : ex!.Message,
        },
    });
}));
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseCors(AuthSetup.CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", async (HealthService health, CancellationToken ct) =>
{
    var report = await health.CheckAsync(ct);
    return report.IsHealthy
        ? Results.Ok(report)
        : Results.Problem(title: "Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
})
.WithName("Health")
.WithTags("System");

app.MapAuthEndpoints();
app.MapClaimEndpoints();
app.MapClaimStatusEndpoints();
app.MapClaimListEndpoints();
app.MapUserEndpoints();

app.Run();

public partial class Program;
