using Claim.Api.Auth;
using Claim.Application.Auth;
using Claim.Domain.Users;

namespace Claim.Api.Users;

public sealed record CreateUserRequest(string? Email, string? DisplayName, string? Password, string? Role);

public sealed record ResetPasswordRequest(string? Password);

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/users").WithTags("Users").RequireAuthorization(AuthPolicies.Admin);

        users.MapGet("/", async (UserService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)));

        users.MapPost("/", async (CreateUserRequest req, UserService service, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(req.Email)) errors["email"] = ["กรุณากรอกอีเมล"];
            if (string.IsNullOrWhiteSpace(req.DisplayName)) errors["displayName"] = ["กรุณากรอกชื่อ"];
            if (PasswordError(req.Password) is { } passwordError) errors["password"] = [passwordError];
            if (req.Role is null || !Role.All.Contains(req.Role)) errors["role"] = ["กรุณาเลือกบทบาทที่ถูกต้อง"];
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var created = await service.CreateAsync(req.Email!, req.DisplayName!, req.Password!, req.Role!, ct);
            return created is null
                ? Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["อีเมลนี้ถูกใช้งานแล้ว"] })
                : Results.Created($"/users/{created.Id}", created);
        });

        users.MapPost("/{id:long}/disable", async (long id, HttpContext http, UserService service, CancellationToken ct) =>
        {
            if (http.User.FindFirst("sub")?.Value == id.ToString())
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["id"] = ["ไม่สามารถปิดใช้งานบัญชีของตัวเองได้"] });
            return await service.DisableAsync(id, ct) ? Results.NoContent() : Results.NotFound();
        });

        users.MapPost("/{id:long}/reset-password", async (long id, ResetPasswordRequest req, UserService service, CancellationToken ct) =>
        {
            if (PasswordError(req.Password) is { } error)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = [error] });
            return await service.ResetPasswordAsync(id, req.Password!, ct) ? Results.NoContent() : Results.NotFound();
        });
    }

    private static string? PasswordError(string? password) =>
        string.IsNullOrEmpty(password) ? "กรุณากรอกรหัสผ่าน"
        : password.Length < UserService.MinPasswordLength ? $"รหัสผ่านต้องมีอย่างน้อย {UserService.MinPasswordLength} ตัวอักษร"
        : null;
}
