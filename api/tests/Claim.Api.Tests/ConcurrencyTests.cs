using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Domain.Claims;

namespace Claim.Api.Tests;

/// <summary>
/// Optimistic concurrency: a Claim JSON carries `version`; a client that changes the Claim sends it back as `If-Match`.
/// A version that is no longer current answers 409 and nothing is written.
/// </summary>
public class ConcurrencyTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _anonymous;

    public ConcurrencyTests(ApiFactory factory)
    {
        _factory = factory;
        _anonymous = factory.CreateClient();
        if (_factory.Policies.SearchAsync("AB-1001", default).Result.Count == 0)
            _factory.Policies.Add("AB-1001", "1กก 1234", 500_000m, "somsak@customer.test");
    }

    private async Task<HttpClient> As(string email)
    {
        var response = await _anonymous.PostAsJsonAsync("/auth/login", new { email, password = ApiFactory.Password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private Task<HttpClient> Staff() => As("staff@claim.test");
    private Task<HttpClient> Adjuster() => As("adjuster@claim.test");

    private static object Details(string damage) => new
    {
        policyId = 1, vehiclePlate = "1กก 1234", vehicleMake = "Toyota", vehicleModel = "Camry",
        customerEmail = "somsak@customer.test", damageDescription = damage,
    };

    private static HttpRequestMessage With(HttpMethod method, string url, string? version, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (version is not null) request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    [Fact]
    public async Task Editing_a_draft_with_a_version_someone_else_already_replaced_answers_409_and_keeps_their_edit()
    {
        var staff = await Staff();
        var created = await staff.PostAsJsonAsync("/claims", Details("เดิม"));
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetInt64();
        var readByBoth = body.GetProperty("version").GetString()!;

        var first = await staff.SendAsync(With(HttpMethod.Put, $"/claims/{id}", readByBoth, Details("ของคนแรก")));
        var second = await staff.SendAsync(With(HttpMethod.Put, $"/claims/{id}", readByBoth, Details("ของคนที่สอง")));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        var stored = await (await staff.GetAsync($"/claims/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ของคนแรก", stored.GetProperty("damageDescription").GetString());
    }
}
