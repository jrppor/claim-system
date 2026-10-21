using Claim.Api.Auth;
using Claim.Application;
using Claim.Application.Health;
using Claim.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
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
    if (ex is not null) Log.Error(ex, "Unhandled exception");
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await ctx.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new()
    {
        HttpContext = ctx,
        ProblemDetails = new ProblemDetails
        {
            Status = 500,
            Title = "An unexpected error occurred.",
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

app.Run();

public partial class Program;
