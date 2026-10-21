using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Domain.Users;

namespace Claim.Api.Tests;

public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public AuthTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private sealed record Tokens(string AccessToken, string RefreshToken, int ExpiresIn, JsonElement User);

    private Task<HttpResponseMessage> Login(string email, string password) =>
        _client.PostAsJsonAsync("/auth/login", new { email, password });

    private async Task<Tokens> LoginOk(string email)
    {
        var response = await Login(email, ApiFactory.Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new Tokens(
            body.GetProperty("accessToken").GetString()!,
            body.GetProperty("refreshToken").GetString()!,
            body.GetProperty("expiresIn").GetInt32(),
            body.GetProperty("user"));
    }

    private Task<HttpResponseMessage> Get(string path, string? accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (accessToken is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request);
    }

    private static void AssertProblem(HttpResponseMessage response) =>
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

    // ---- login ----

    [Fact]
    public async Task Login_with_valid_credentials_returns_15_minute_access_token_and_user()
    {
        var tokens = await LoginOk("staff@claim.test");

        Assert.Equal(15 * 60, tokens.ExpiresIn);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.Equal(Role.GarageStaff, tokens.User.GetProperty("role").GetString());
        Assert.Equal("staff@claim.test", tokens.User.GetProperty("email").GetString());
    }

    [Theory]
    [InlineData("staff@claim.test", "wrong-password")]
    [InlineData("nobody@claim.test", ApiFactory.Password)]
    [InlineData("former@claim.test", ApiFactory.Password)]
    public async Task Login_with_bad_credentials_or_inactive_user_returns_401_problem_with_same_message(string email, string password)
    {
        var response = await Login(email, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertProblem(response);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid email or password.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_with_missing_fields_returns_400_validation_problem()
    {
        var response = await Login("", "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertProblem(response);
    }

    // ---- refresh / sign-out ----

    private Task<HttpResponseMessage> Refresh(string token) =>
        _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = token });

    [Fact]
    public async Task Refresh_rotates_the_token_and_the_old_one_cannot_be_reused()
    {
        var first = await LoginOk("adjuster@claim.test");

        var rotated = await Refresh(first.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var body = await rotated.Content.ReadFromJsonAsync<JsonElement>();
        var newRefresh = body.GetProperty("refreshToken").GetString()!;
        Assert.NotEqual(first.RefreshToken, newRefresh);
        Assert.Equal(Role.InsuranceAdjuster, body.GetProperty("user").GetProperty("role").GetString());

        var reuse = await Refresh(first.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        AssertProblem(reuse);

        var next = await Refresh(newRefresh);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_unknown_token_returns_401()
    {
        var response = await Refresh("not-a-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertProblem(response);
    }

    [Fact]
    public async Task Refresh_fails_once_the_user_has_been_deactivated()
    {
        _factory.Users.Add("temp@claim.test", ApiFactory.Password, Role.GarageStaff);
        var tokens = await LoginOk("temp@claim.test");
        _factory.Users.Deactivate("temp@claim.test");

        var response = await Refresh(tokens.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sign_out_revokes_the_refresh_token()
    {
        var tokens = await LoginOk("staff@claim.test");

        var logout = await _client.PostAsJsonAsync("/auth/logout", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await Refresh(tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    // ---- 401 / 403 ----

    [Fact]
    public async Task Protected_endpoint_without_token_returns_401_problem()
    {
        var response = await Get("/auth/me", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertProblem(response);
    }

    [Fact]
    public async Task Protected_endpoint_with_tampered_token_returns_401()
    {
        var tokens = await LoginOk("admin@claim.test");

        var response = await Get("/users", tokens.AccessToken + "x");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_signed_in_user()
    {
        var tokens = await LoginOk("adjuster@claim.test");

        var response = await Get("/auth/me", tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Role.InsuranceAdjuster, body.GetProperty("role").GetString());
    }

    [Theory]
    [InlineData("staff@claim.test")]
    [InlineData("adjuster@claim.test")]
    public async Task Admin_only_endpoint_returns_403_problem_for_other_roles(string email)
    {
        var tokens = await LoginOk(email);

        var response = await Get("/users", tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        AssertProblem(response);
    }

    [Fact]
    public async Task Admin_only_endpoint_succeeds_for_admin_and_never_exposes_password_hashes()
    {
        var tokens = await LoginOk("admin@claim.test");

        var response = await Get("/users", tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("admin@claim.test", text);
        Assert.DoesNotContain("PBKDF2", text);
    }

    // ---- CORS ----

    [Fact]
    public async Task Cors_preflight_allows_the_ui_origin_only()
    {
        async Task<HttpResponseMessage> Preflight(string origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Options, "/auth/login");
            request.Headers.Add("Origin", origin);
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
            return await _client.SendAsync(request);
        }

        var allowed = await Preflight("http://localhost:3000");
        var denied = await Preflight("http://evil.example");

        Assert.Equal("http://localhost:3000", allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public void Passwords_are_stored_hashed_not_plain()
    {
        foreach (var user in _factory.Users.Users)
        {
            Assert.DoesNotContain(ApiFactory.Password, user.PasswordHash);
            Assert.StartsWith("PBKDF2$", user.PasswordHash);
        }
    }
}
