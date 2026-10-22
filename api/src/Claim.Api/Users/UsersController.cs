using Claim.Api.Auth;
using Claim.Api.Http;
using Claim.Application.Auth;
using Claim.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Claim.Api.Users;

public sealed record CreateUserRequest(string? Email, string? DisplayName, string? Password, string? Role);

public sealed record ResetPasswordRequest(string? Password);

/// <summary>Admin user management. The Admin UI shows these messages as they are, so this group's messages are Thai (the Claim and Auth groups are English).</summary>
[ApiController]
[Route("users")]
[Tags("Users")]
[Authorize(Policy = AuthPolicies.Admin)]
public sealed class UsersController(UserService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest req, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(req.Email)) errors["email"] = ["กรุณากรอกอีเมล"];
        if (string.IsNullOrWhiteSpace(req.DisplayName)) errors["displayName"] = ["กรุณากรอกชื่อ"];
        if (PasswordError(req.Password) is { } passwordError) errors["password"] = [passwordError];
        if (req.Role is null || !Role.All.Contains(req.Role)) errors["role"] = ["กรุณาเลือกบทบาทที่ถูกต้อง"];
        if (errors.Count > 0) return this.ValidationError(errors);

        var created = await service.CreateAsync(req.Email!, req.DisplayName!, req.Password!, req.Role!, ct);
        return created is null
            ? this.ValidationError(new Dictionary<string, string[]> { ["email"] = ["อีเมลนี้ถูกใช้งานแล้ว"] })
            : Created($"/users/{created.Id}", created);
    }

    [HttpPost("{id:long}/disable")]
    public async Task<IActionResult> Disable(long id, CancellationToken ct)
    {
        if (Actor.From(User).UserId == id)
            return this.ValidationError(new Dictionary<string, string[]> { ["id"] = ["ไม่สามารถปิดใช้งานบัญชีของตัวเองได้"] });
        return await service.DisableAsync(id, ct) ? NoContent() : UserNotFound();
    }

    [HttpPost("{id:long}/reset-password")]
    public async Task<IActionResult> ResetPassword(long id, ResetPasswordRequest req, CancellationToken ct)
    {
        if (PasswordError(req.Password) is { } error)
            return this.ValidationError(new Dictionary<string, string[]> { ["password"] = [error] });
        return await service.ResetPasswordAsync(id, req.Password!, ct) ? NoContent() : UserNotFound();
    }

    private IActionResult UserNotFound() =>
        this.ProblemResult("ไม่พบข้อมูล", "ไม่พบผู้ใช้นี้", StatusCodes.Status404NotFound);

    private static string? PasswordError(string? password) =>
        string.IsNullOrEmpty(password) ? "กรุณากรอกรหัสผ่าน"
        : password.Length < UserService.MinPasswordLength ? $"รหัสผ่านต้องมีอย่างน้อย {UserService.MinPasswordLength} ตัวอักษร"
        : null;
}
