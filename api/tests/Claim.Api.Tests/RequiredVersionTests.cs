using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Domain.Claims;

namespace Claim.Api.Tests;

/// <summary>
/// Every endpoint that changes a Claim demands the version the caller read (If-Match): missing → 428, `*` does not bypass it,
/// stale → 409 with the version-conflict type. These tests use a plain client; the other test classes use a client that
/// reads the Claim first and sends its version, as the UI does.
/// </summary>
public class RequiredVersionTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RequiredVersionTests(ApiFactory factory)
    {
        _factory = factory;
        if (_factory.Policies.SearchAsync("AB-1001", default).Result.Count == 0)
            _factory.Policies.Add("AB-1001", "1กก 1234", 500_000m, "somsak@customer.test");
    }

    private async Task<HttpClient> As(string email)
    {
        var login = await _factory.CreateClient().PostAsJsonAsync("/auth/login", new { email, password = ApiFactory.Password });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static readonly object Details = new
    {
        policyId = 1, vehiclePlate = "1กก 1234", vehicleMake = "Toyota", vehicleModel = "Camry",
        customerEmail = "somsak@customer.test", damageDescription = "ชนท้าย",
    };

    private static readonly object Estimate = new { lines = new[] { new { kind = "Part", description = "กันชนหน้า", quantity = 1, unitPrice = 5000 } } };

    public static TheoryData<string, string, string, ClaimStatus> Endpoints => new()
    {
        { "PUT", "", "staff@claim.test", ClaimStatus.Draft },
        { "POST", "/submit", "staff@claim.test", ClaimStatus.Draft },
        { "POST", "/review", "adjuster@claim.test", ClaimStatus.Submitted },
        { "POST", "/approve", "adjuster@claim.test", ClaimStatus.UnderReview },
        { "POST", "/request-changes", "adjuster@claim.test", ClaimStatus.UnderReview },
        { "POST", "/reject", "adjuster@claim.test", ClaimStatus.UnderReview },
        { "POST", "/cancel", "staff@claim.test", ClaimStatus.Draft },
        { "POST", "/estimates", "staff@claim.test", ClaimStatus.Submitted },
        { "POST", "/start-repair", "staff@claim.test", ClaimStatus.Approved },
        { "POST", "/mark-repaired", "staff@claim.test", ClaimStatus.InRepair },
        { "POST", "/invoice", "staff@claim.test", ClaimStatus.Repaired },
        { "POST", "/pay", "adjuster@claim.test", ClaimStatus.InvoiceSubmitted },
    };

    private static object BodyFor(string path) => path switch
    {
        "" => Details,
        "/approve" => new { approvedAmount = 1000m, reason = "ok" },
        "/estimates" => Estimate,
        "/invoice" => new { actualAmount = 1000m },
        _ => new { reason = "x", comments = "x" },
    };

    private async Task<HttpResponseMessage> Send(string method, string path, string email, ClaimStatus status, string? ifMatch)
    {
        var claim = _factory.Claims.Seed(status, 1, 1);
        var request = new HttpRequestMessage(new HttpMethod(method), $"/claims/{claim.Id}{path}") { Content = JsonContent.Create(BodyFor(path)) };
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return await (await As(email)).SendAsync(request);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task A_missing_If_Match_answers_428_problem_details(string method, string path, string email, ClaimStatus status)
    {
        var response = await Send(method, path, email, status, ifMatch: null);

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(428, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32());
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task A_star_If_Match_does_not_bypass_the_version_check(string method, string path, string email, ClaimStatus status)
    {
        var response = await Send(method, path, email, status, ifMatch: "*");

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task A_stale_version_answers_409_with_the_version_conflict_type(string method, string path, string email, ClaimStatus status)
    {
        var response = await Send(method, path, email, status, ifMatch: "\"AAAAAAAAAAA=\"");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("urn:claim:version-conflict", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
    }
}
