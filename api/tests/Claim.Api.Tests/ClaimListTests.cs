using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Domain.Claims;

namespace Claim.Api.Tests;

public sealed class ClaimListTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public ClaimListTests()
    {
        _factory.Policies.Add("AB-1001", "1กก 1234", 500_000m, "somsak@customer.test");
        _factory.Policies.Add("AB-1002", "2ขข 5678", 300_000m, "malee@customer.test");
    }

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> As(string email)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/auth/login", new { email, password = ApiFactory.Password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var client = _factory.CreateVersionedClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private static string[] Numbers(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("claimNumber").GetString()!).ToArray();

    [Fact]
    public async Task Staff_lists_claims_newest_first_with_total()
    {
        var a = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var b = _factory.Claims.Seed(ClaimStatus.Submitted, 2, 1);

        var response = await (await As("staff@claim.test")).GetAsync("/claims");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await Json(response);
        Assert.Equal(2, page.GetProperty("total").GetInt32());
        Assert.Equal([b.ClaimNumber, a.ClaimNumber], Numbers(page));
    }

    [Fact]
    public async Task Status_filter_narrows_the_list_and_an_unknown_status_is_a_validation_problem()
    {
        _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var approved = _factory.Claims.Seed(ClaimStatus.Approved, 1, 1);
        var staff = await As("staff@claim.test");

        var page = await Json(await staff.GetAsync("/claims?status=Approved"));
        var bad = await staff.GetAsync("/claims?status=Bogus");

        Assert.Equal([approved.ClaimNumber], Numbers(page));
        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Search_matches_claim_number_plate_or_customer_case_insensitively()
    {
        var a = _factory.Claims.Seed(ClaimStatus.Submitted, 1, 1);   // Policy 1 customer "สมศักดิ์ ใจดี", plate 1กก 1234
        var b = _factory.Claims.Seed(ClaimStatus.Submitted, 2, 1, plate: "9ฮฮ 9999");
        var staff = await As("staff@claim.test");

        var byNumber = Numbers(await Json(await staff.GetAsync($"/claims?search={a.ClaimNumber.ToLowerInvariant()}")));
        var byPlate = Numbers(await Json(await staff.GetAsync($"/claims?search={Uri.EscapeDataString("9ฮฮ")}")));
        var byCustomer = Numbers(await Json(await staff.GetAsync($"/claims?search={Uri.EscapeDataString("ใจดี")}")));
        var none = Numbers(await Json(await staff.GetAsync("/claims?search=nomatch")));

        Assert.Equal([a.ClaimNumber], byNumber);
        Assert.Equal([b.ClaimNumber], byPlate);
        Assert.Equal([b.ClaimNumber, a.ClaimNumber], byCustomer);
        Assert.Empty(none);
    }

    [Fact]
    public async Task List_is_paged_and_total_counts_all_matches()
    {
        var seeded = Enumerable.Range(0, 5).Select(_ => _factory.Claims.Seed(ClaimStatus.Submitted, 1, 1).ClaimNumber).Reverse().ToArray();
        var staff = await As("staff@claim.test");

        var second = await Json(await staff.GetAsync("/claims?page=2&pageSize=2"));
        var last = await Json(await staff.GetAsync("/claims?page=3&pageSize=2"));
        var beyond = await Json(await staff.GetAsync("/claims?page=4&pageSize=2"));

        Assert.Equal(seeded[2..4], Numbers(second));
        Assert.Equal(5, second.GetProperty("total").GetInt32());
        Assert.Equal(2, second.GetProperty("page").GetInt32());
        Assert.Equal(2, second.GetProperty("pageSize").GetInt32());
        Assert.Equal(seeded[4..], Numbers(last));
        Assert.Empty(Numbers(beyond));
    }

    [Fact]
    public async Task Dashboard_counts_claims_by_status_and_hides_drafts_from_the_adjuster()
    {
        _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        _factory.Claims.Seed(ClaimStatus.Submitted, 1, 1);
        _factory.Claims.Seed(ClaimStatus.Paid, 1, 1);

        var staff = await Json(await (await As("staff@claim.test")).GetAsync("/dashboard/summary"));
        var admin = await Json(await (await As("admin@claim.test")).GetAsync("/dashboard/summary"));
        var adjuster = await Json(await (await As("adjuster@claim.test")).GetAsync("/dashboard/summary"));

        Assert.Equal(2, staff.GetProperty("countsByStatus").GetProperty("Draft").GetInt32());
        Assert.Equal(1, staff.GetProperty("countsByStatus").GetProperty("Submitted").GetInt32());
        Assert.Equal(0, staff.GetProperty("countsByStatus").GetProperty("Approved").GetInt32());
        Assert.Equal(4, staff.GetProperty("total").GetInt32());
        Assert.Equal(4, admin.GetProperty("total").GetInt32());
        Assert.False(adjuster.GetProperty("countsByStatus").TryGetProperty("Draft", out _));
        Assert.Equal(1, adjuster.GetProperty("countsByStatus").GetProperty("Submitted").GetInt32());
        Assert.Equal(2, adjuster.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Admin_sees_every_claim_but_cannot_change_any_and_anonymous_is_refused()
    {
        var draft = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var admin = await As("admin@claim.test");

        var list = await Json(await admin.GetAsync("/claims"));
        var create = await admin.PostAsJsonAsync("/claims", new { policyId = 1 });
        var edit = await admin.PutAsJsonAsync($"/claims/{draft.Id}", new { policyId = 1, damageDescription = "x" });
        var anonymous = await _factory.CreateClient().GetAsync("/claims");
        var anonymousSummary = await _factory.CreateClient().GetAsync("/dashboard/summary");

        Assert.Equal([draft.ClaimNumber], Numbers(list));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousSummary.StatusCode);
    }

    [Fact]
    public async Task Adjuster_never_sees_drafts_even_when_filtering_by_draft()
    {
        _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var submitted = _factory.Claims.Seed(ClaimStatus.Submitted, 1, 1);
        var adjuster = await As("adjuster@claim.test");

        var all = await Json(await adjuster.GetAsync("/claims"));
        var drafts = await Json(await adjuster.GetAsync("/claims?status=Draft"));

        Assert.Equal([submitted.ClaimNumber], Numbers(all));
        Assert.Equal(1, all.GetProperty("total").GetInt32());
        Assert.Empty(Numbers(drafts));
        Assert.Equal(0, drafts.GetProperty("total").GetInt32());
    }
}
