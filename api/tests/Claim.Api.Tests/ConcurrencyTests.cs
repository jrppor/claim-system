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

    private static async Task<string> ReadVersion(HttpClient client, long id) =>
        (await (await client.GetAsync($"/claims/{id}")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetString()!;

    private async Task<ClaimStatus> StatusOf(long id) => (await _factory.Claims.FindByIdAsync(id, default))!.Status;

    [Fact]
    public async Task Submitting_with_a_stale_version_answers_409_and_does_not_move_the_claim_or_write_history()
    {
        var staff = await Staff();
        var claim = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var stale = await ReadVersion(staff, claim.Id);
        await staff.SendAsync(With(HttpMethod.Put, $"/claims/{claim.Id}", stale, Details("แก้ระหว่างที่อีกคนเปิดอยู่")));

        var response = await staff.SendAsync(With(HttpMethod.Post, $"/claims/{claim.Id}/submit", stale));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ClaimStatus.Draft, await StatusOf(claim.Id));
        Assert.DoesNotContain(_factory.History.All, h => h.ClaimId == claim.Id);
        Assert.DoesNotContain(_factory.Outbox.Messages, m => m.Message.ClaimId == claim.Id);
    }

    private static readonly object[] OneLine = [new { kind = "Part", description = "กันชนหน้า", quantity = 1, unitPrice = 5000 }];

    [Fact]
    public async Task Submitting_a_repair_estimate_with_a_stale_version_answers_409_and_stores_no_revision()
    {
        var staff = await Staff();
        var claim = _factory.Claims.Seed(ClaimStatus.Submitted, 1, 1);
        var stale = await ReadVersion(staff, claim.Id);
        await (await Adjuster()).PostAsync($"/claims/{claim.Id}/review", null);   // someone else moves the Claim on

        var response = await staff.SendAsync(With(HttpMethod.Post, $"/claims/{claim.Id}/estimates", stale, new { lines = OneLine }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.DoesNotContain(_factory.Estimates.All, e => e.ClaimId == claim.Id);
    }

    [Fact]
    public async Task A_repair_estimate_that_leaves_the_claim_in_Submitted_still_refuses_a_stale_version()
    {
        var staff = await Staff();
        var claim = _factory.Claims.Seed(ClaimStatus.Submitted, 1, 1);
        var read = await ReadVersion(staff, claim.Id);
        var other = Convert.ToBase64String(BitConverter.GetBytes(long.MaxValue));   // not what the store holds

        var stale = await staff.SendAsync(With(HttpMethod.Post, $"/claims/{claim.Id}/estimates", other, new { lines = OneLine }));
        var current = await staff.SendAsync(With(HttpMethod.Post, $"/claims/{claim.Id}/estimates", read, new { lines = OneLine }));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Created, current.StatusCode);
        Assert.Single(_factory.Estimates.All, e => e.ClaimId == claim.Id);
    }

    private const string Elsewhere = "//////////8=";   // a well-formed version the store does not hold

    [Theory]
    [InlineData("Submitted", "review", "adjuster")]
    [InlineData("UnderReview", "reject", "adjuster")]
    [InlineData("UnderReview", "request-changes", "adjuster")]
    [InlineData("UnderReview", "approve", "adjuster")]
    [InlineData("Submitted", "cancel", "staff")]
    [InlineData("Draft", "cancel", "staff")]
    [InlineData("Approved", "start-repair", "staff")]
    [InlineData("InRepair", "mark-repaired", "staff")]
    [InlineData("Repaired", "invoice", "staff")]
    [InlineData("InvoiceSubmitted", "pay", "adjuster")]
    public async Task Every_transition_refuses_a_stale_version_with_409_and_leaves_status_history_and_outbox_untouched(string status, string action, string who)
    {
        var claim = _factory.Claims.Seed(Enum.Parse<ClaimStatus>(status), 1, 1);
        _factory.Estimates.Append(new RepairEstimate([new EstimateLine(EstimateLineKind.Part, "กันชน", 1, 5000m)]) { ClaimId = claim.Id, SubmittedByUserId = 1, SubmittedAt = DateTime.UtcNow });
        var client = who == "staff" ? await Staff() : await Adjuster();
        var body = new { reason = "เหตุผล", comments = "ความเห็น", approvedAmount = 4000m, actualAmount = 4000m };

        var response = await client.SendAsync(With(HttpMethod.Post, $"/claims/{claim.Id}/{action}", Elsewhere, body));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        Assert.Equal(Enum.Parse<ClaimStatus>(status), await StatusOf(claim.Id));
        Assert.DoesNotContain(_factory.History.All, h => h.ClaimId == claim.Id);
        Assert.DoesNotContain(_factory.Outbox.Messages, m => m.Message.ClaimId == claim.Id);
    }

    [Fact]
    public async Task The_current_version_succeeds_and_the_response_carries_a_new_version_that_the_old_one_no_longer_matches()
    {
        var staff = await Staff();
        var claim = _factory.Claims.Seed(ClaimStatus.Submitted, 1, 1);
        var read = await ReadVersion(staff, claim.Id);

        var cancelled = await staff.SendAsync(With(HttpMethod.Post, $"/claims/{claim.Id}/cancel", read, new { reason = "ลูกค้าขอยกเลิก" }));

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        var after = (await cancelled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetString();
        Assert.NotEqual(read, after);
        Assert.Equal(after, await ReadVersion(staff, claim.Id));
    }

    [Fact]
    public async Task Without_a_version_the_change_is_not_checked_against_the_client()
    {
        var staff = await Staff();
        var claim = _factory.Claims.Seed(ClaimStatus.Submitted, 1, 1);

        var response = await staff.SendAsync(With(HttpMethod.Post, $"/claims/{claim.Id}/cancel", null, new { reason = "ลูกค้าขอยกเลิก" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_version_that_is_not_a_version_token_is_a_400_problem()
    {
        var staff = await Staff();
        var claim = _factory.Claims.Seed(ClaimStatus.Submitted, 1, 1);

        var response = await staff.SendAsync(With(HttpMethod.Post, $"/claims/{claim.Id}/cancel", "not base64!!", new { reason = "x" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ClaimStatus.Submitted, await StatusOf(claim.Id));
    }

    [Fact]
    public async Task Two_simultaneous_decisions_on_the_same_version_let_exactly_one_win()
    {
        var claim = _factory.Claims.Seed(ClaimStatus.UnderReview, 1, 1);
        _factory.Estimates.Append(new RepairEstimate([new EstimateLine(EstimateLineKind.Part, "กันชน", 1, 5000m)]) { ClaimId = claim.Id, SubmittedByUserId = 1, SubmittedAt = DateTime.UtcNow });
        var adjuster = await Adjuster();
        var read = await ReadVersion(adjuster, claim.Id);

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(i =>
            adjuster.SendAsync(With(HttpMethod.Post, $"/claims/{claim.Id}/reject", read, new { reason = $"คนที่ {i}" }))));

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], results.Select(r => r.StatusCode).Order());
        Assert.Single(_factory.History.All, h => h.ClaimId == claim.Id);
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
        // The UI tells this 409 (reload and retry) apart from a forbidden status move by the problem type.
        Assert.Equal("urn:claim:version-conflict", (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
        var stored = await (await staff.GetAsync($"/claims/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ของคนแรก", stored.GetProperty("damageDescription").GetString());
    }
}
