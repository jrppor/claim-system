using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Domain.Users;

namespace Claim.Api.Tests;

public class UserManagementTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public UserManagementTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<JsonElement> LoginBody(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<string> Token(string email) =>
        (await LoginBody(email, ApiFactory.Password)).GetProperty("accessToken").GetString()!;

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> AsAdmin(HttpMethod method, string path, object? body = null) =>
        await Send(method, path, await Token("admin@claim.test"), body);

    private static object NewUser(string email, string role = Role.GarageStaff, string password = "Brand-new-pw1") =>
        new { email, displayName = "ผู้ใช้ใหม่", password, role };

    [Fact]
    public async Task Admin_creates_a_user_who_can_then_sign_in_with_the_given_role()
    {
        var response = await AsAdmin(HttpMethod.Post, "/users", NewUser("new.adjuster@claim.test", Role.InsuranceAdjuster));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("new.adjuster@claim.test", created.GetProperty("email").GetString());
        Assert.Equal(Role.InsuranceAdjuster, created.GetProperty("role").GetString());
        Assert.True(created.GetProperty("isActive").GetBoolean());
        Assert.DoesNotContain("PBKDF2", await response.Content.ReadAsStringAsync());

        var login = await LoginBody("new.adjuster@claim.test", "Brand-new-pw1");
        Assert.Equal(Role.InsuranceAdjuster, login.GetProperty("user").GetProperty("role").GetString());
    }

    [Fact]
    public async Task Creating_a_user_with_a_taken_email_is_rejected_with_a_readable_validation_error()
    {
        var response = await AsAdmin(HttpMethod.Post, "/users", NewUser("STAFF@claim.test"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("อีเมลนี้ถูกใช้งานแล้ว", body.GetProperty("errors").GetProperty("email")[0].GetString());
    }

    [Fact]
    public async Task Creating_a_user_with_missing_or_invalid_fields_is_rejected_per_field()
    {
        var response = await AsAdmin(HttpMethod.Post, "/users", new { email = "", displayName = "x", password = "pw", role = "Boss" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("role", out _));
    }

    [Theory]
    [InlineData("staff@claim.test")]
    [InlineData("adjuster@claim.test")]
    public async Task Non_admins_get_403_on_every_user_management_endpoint(string email)
    {
        var token = await Token(email);

        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, "/users", token, NewUser("x@claim.test"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, "/users/1/disable", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Send(HttpMethod.Post, "/users/1/reset-password", token, new { password = "Another-pw-1" })).StatusCode);
    }

    [Fact]
    public async Task Listing_includes_active_state()
    {
        var response = await AsAdmin(HttpMethod.Get, "/users");

        var rows = await response.Content.ReadFromJsonAsync<JsonElement>();
        var former = rows.EnumerateArray().Single(r => r.GetProperty("email").GetString() == "former@claim.test");
        Assert.False(former.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Reset_password_replaces_the_password_and_revokes_refresh_tokens()
    {
        var target = _factory.Users.Add("locked@claim.test", ApiFactory.Password, Role.GarageStaff);
        var held = (await LoginBody("locked@claim.test", ApiFactory.Password)).GetProperty("refreshToken").GetString()!;

        var reset = await AsAdmin(HttpMethod.Post, $"/users/{target.Id}/reset-password", new { password = "Fresh-start-9" });

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        var oldLogin = await _client.PostAsJsonAsync("/auth/login", new { email = "locked@claim.test", password = ApiFactory.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        await LoginBody("locked@claim.test", "Fresh-start-9");
        Assert.False(_factory.RefreshTokens.Contains(held));
    }

    [Fact]
    public async Task Reset_password_rejects_a_short_password_and_unknown_user()
    {
        var target = _factory.Users.Add("short@claim.test", ApiFactory.Password, Role.GarageStaff);

        var tooShort = await AsAdmin(HttpMethod.Post, $"/users/{target.Id}/reset-password", new { password = "abc" });
        var unknown = await AsAdmin(HttpMethod.Post, "/users/9999/reset-password", new { password = "Fresh-start-9" });

        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_disable_their_own_account()
    {
        var self = _factory.Users.Users.Single(u => u.Email == "admin@claim.test");

        var response = await AsAdmin(HttpMethod.Post, $"/users/{self.Id}/disable");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(_factory.Users.Users.Single(u => u.Email == "admin@claim.test").IsActive);
    }

    [Fact]
    public async Task Disabled_user_cannot_sign_in_and_their_refresh_tokens_are_revoked()
    {
        var target = _factory.Users.Add("leaver@claim.test", ApiFactory.Password, Role.GarageStaff);
        var held = (await LoginBody("leaver@claim.test", ApiFactory.Password)).GetProperty("refreshToken").GetString()!;
        var otherHeld = (await LoginBody("leaver@claim.test", ApiFactory.Password)).GetProperty("refreshToken").GetString()!;

        var disable = await AsAdmin(HttpMethod.Post, $"/users/{target.Id}/disable");

        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);
        var login = await _client.PostAsJsonAsync("/auth/login", new { email = "leaver@claim.test", password = ApiFactory.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.False(_factory.RefreshTokens.Contains(held));
        Assert.False(_factory.RefreshTokens.Contains(otherHeld));
    }
}
