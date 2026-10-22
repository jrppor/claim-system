using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Domain.Claims;

namespace Claim.Api.Tests;

public class DraftClaimTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public DraftClaimTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        if (_factory.Policies.SearchAsync("AB-1001", default).Result.Count == 0)
        {
            _factory.Policies.Add("AB-1001", "1กก 1234", 500_000m, "somsak@customer.test");
            _factory.Policies.Add("AB-1002", "2ขข 5678", 300_000m, "malee@customer.test");
        }
    }

    private async Task<HttpClient> As(string email)
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new { email, password = ApiFactory.Password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var client = _factory.CreateVersionedClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private Task<HttpClient> Staff() => As("staff@claim.test");
    private Task<HttpClient> Adjuster() => As("adjuster@claim.test");
    private Task<HttpClient> Admin() => As("admin@claim.test");

    private static object NewClaim(long policyId = 1, string? damage = "กันชนหน้าบุบ") => new
    {
        policyId,
        vehiclePlate = "1กก 1234",
        vehicleMake = "Toyota",
        vehicleModel = "Camry",
        customerEmail = "somsak@customer.test",
        damageDescription = damage,
    };

    // ---- create ----

    [Fact]
    public async Task Staff_creates_a_draft_and_numbers_run_consecutively_without_repeats()
    {
        var staff = await Staff();
        var year = DateTime.UtcNow.Year;

        var first = await staff.PostAsJsonAsync("/claims", NewClaim());
        var second = await staff.PostAsJsonAsync("/claims", NewClaim(policyId: 2));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var a = await first.Content.ReadFromJsonAsync<JsonElement>();
        var b = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", a.GetProperty("status").GetString());
        Assert.Matches($"^CLM-{year}-\\d{{6}}$", a.GetProperty("claimNumber").GetString());
        var na = int.Parse(a.GetProperty("claimNumber").GetString()![^6..]);
        var nb = int.Parse(b.GetProperty("claimNumber").GetString()![^6..]);
        Assert.Equal(na + 1, nb);
        Assert.Equal($"/claims/{a.GetProperty("id").GetInt64()}", first.Headers.Location?.ToString());
        Assert.Equal("1กก 1234", a.GetProperty("vehiclePlate").GetString());
        Assert.Equal("AB-1001", a.GetProperty("policy").GetProperty("policyNumber").GetString());
        Assert.Equal(500_000m, a.GetProperty("policy").GetProperty("coverageLimit").GetDecimal());
    }

    [Fact]
    public async Task Create_rejects_unknown_policy_and_malformed_email_as_validation_problem()
    {
        var staff = await Staff();

        var unknownPolicy = await staff.PostAsJsonAsync("/claims", NewClaim(policyId: 999));
        var badEmail = await staff.PostAsJsonAsync("/claims", new { policyId = 1, customerEmail = "not-an-email" });

        Assert.Equal(HttpStatusCode.BadRequest, unknownPolicy.StatusCode);
        Assert.Equal("application/problem+json", unknownPolicy.Content.Headers.ContentType?.MediaType);
        Assert.Contains("policyId", (await unknownPolicy.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
        Assert.Contains("customerEmail", (await badEmail.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Create_allows_an_incomplete_draft_with_only_a_policy()
    {
        var staff = await Staff();

        var response = await staff.PostAsJsonAsync("/claims", new { policyId = 1 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("damageDescription").ValueKind);
    }

    // ---- edit ----

    [Fact]
    public async Task Staff_edits_a_draft_and_the_detail_shows_the_change()
    {
        var staff = await Staff();
        var created = await (await staff.PostAsJsonAsync("/claims", NewClaim())).Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt64();

        var edit = await staff.PutAsJsonAsync($"/claims/{id}", new
        {
            policyId = 1,
            vehiclePlate = "1กก 1234",
            vehicleMake = "Toyota",
            vehicleModel = "Camry",
            customerEmail = "new@customer.test",
            damageDescription = "ประตูขวาบุบ",
        });
        var detail = await staff.GetFromJsonAsync<JsonElement>($"/claims/{id}");

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal("ประตูขวาบุบ", detail.GetProperty("damageDescription").GetString());
        Assert.Equal("new@customer.test", detail.GetProperty("customerEmail").GetString());
        Assert.Equal(created.GetProperty("claimNumber").GetString(), detail.GetProperty("claimNumber").GetString());
        Assert.Equal("Draft", detail.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Editing_or_reading_an_unknown_claim_is_404()
    {
        var staff = await Staff();

        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync("/claims/99999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.PutAsJsonAsync("/claims/99999", new { policyId = 1 })).StatusCode);
    }

    [Theory]
    [InlineData(ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Cancelled)]
    public async Task A_claim_past_draft_cannot_be_edited_and_is_left_unchanged(ClaimStatus status)
    {
        var staff = await Staff();
        var seeded = _factory.Claims.Seed(status, policyId: 1, createdByUserId: 1);

        var edit = await staff.PutAsJsonAsync($"/claims/{seeded.Id}", new { policyId = 1, damageDescription = "แก้ทีหลัง" });

        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal("application/problem+json", edit.Content.Headers.ContentType?.MediaType);
        var detail = await staff.GetFromJsonAsync<JsonElement>($"/claims/{seeded.Id}");
        Assert.Equal("ชนท้าย", detail.GetProperty("damageDescription").GetString());
    }

    // ---- roles and visibility ----

    [Fact]
    public async Task Only_garage_staff_may_create_or_edit_and_anonymous_is_refused()
    {
        var draft = await (await (await Staff()).PostAsJsonAsync("/claims", NewClaim())).Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetInt64();

        foreach (var other in new[] { await Adjuster(), await Admin() })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync("/claims", NewClaim())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.PutAsJsonAsync($"/claims/{id}", NewClaim())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/policies?q=AB")).StatusCode);
        }
        var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/claims", NewClaim())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/claims/{id}")).StatusCode);
    }

    [Fact]
    public async Task Insurance_adjuster_cannot_see_a_draft_but_sees_it_once_submitted_and_admin_always_can()
    {
        var staff = await Staff();
        var draft = await (await staff.PostAsJsonAsync("/claims", NewClaim())).Content.ReadFromJsonAsync<JsonElement>();
        var draftId = draft.GetProperty("id").GetInt64();
        var submitted = _factory.Claims.Seed(ClaimStatus.Submitted, policyId: 1, createdByUserId: 1);
        var adjuster = await Adjuster();
        var admin = await Admin();

        Assert.Equal(HttpStatusCode.NotFound, (await adjuster.GetAsync($"/claims/{draftId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adjuster.GetAsync($"/claims/{submitted.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/claims/{draftId}")).StatusCode);
    }

    // ---- policy lookup ----

    [Fact]
    public async Task Staff_finds_a_policy_by_policy_number_or_plate_with_customer_and_coverage_limit()
    {
        var staff = await Staff();

        var byNumber = await staff.GetFromJsonAsync<JsonElement>("/policies?q=ab-1001");
        var byPlate = await staff.GetFromJsonAsync<JsonElement>("/policies?q=" + Uri.EscapeDataString("2ขข"));

        var found = Assert.Single(byNumber.EnumerateArray());
        Assert.Equal("AB-1001", found.GetProperty("policyNumber").GetString());
        Assert.Equal(500_000m, found.GetProperty("coverageLimit").GetDecimal());
        Assert.Equal("somsak@customer.test", found.GetProperty("customer").GetProperty("email").GetString());
        Assert.Equal("AB-1002", Assert.Single(byPlate.EnumerateArray()).GetProperty("policyNumber").GetString());
    }
}
