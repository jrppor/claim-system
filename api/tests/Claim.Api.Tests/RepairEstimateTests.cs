using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Domain.Claims;

namespace Claim.Api.Tests;

public class RepairEstimateTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RepairEstimateTests(ApiFactory factory)
    {
        _factory = factory;
        if (_factory.Policies.SearchAsync("AB-1001", default).Result.Count == 0)
            _factory.Policies.Add("AB-1001", "1กก 1234", 500_000m, "somsak@customer.test");
        if (_factory.Policies.SearchAsync("AB-2002", default).Result.Count == 0)
            _factory.Policies.Add("AB-2002", "2ขข 5678", 10_000m, "small@customer.test");
    }

    private async Task<HttpClient> As(string email)
    {
        var login = await _factory.CreateClient().PostAsJsonAsync("/auth/login", new { email, password = ApiFactory.Password });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var client = _factory.CreateVersionedClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private Task<HttpClient> Staff() => As("staff@claim.test");

    private Domain.Claims.Claim SeedClaim(ClaimStatus status, long policyId = 1) => _factory.Claims.Seed(status, policyId, 1);

    private static object Line(string kind, string description, decimal quantity, decimal unitPrice) =>
        new { kind, description, quantity, unitPrice };

    private static object TwoLines() => new
    {
        lines = new[] { Line("Part", "กันชนหน้า", 1, 4500.50m), Line("Labour", "ค่าแรงเปลี่ยนกันชน", 2.5m, 400m) },
    };

    [Fact]
    public async Task Staff_creates_a_repair_estimate_and_the_estimated_amount_is_computed_from_the_lines()
    {
        var claim = SeedClaim(ClaimStatus.Submitted);

        var response = await (await Staff()).PostAsJsonAsync($"/claims/{claim.Id}/estimates", TwoLines());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var estimate = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, estimate.GetProperty("revision").GetInt32());
        Assert.Equal(5500.50m, estimate.GetProperty("estimatedAmount").GetDecimal());
        Assert.Equal(2, estimate.GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public async Task An_estimated_amount_sent_by_the_client_is_ignored()
    {
        var claim = SeedClaim(ClaimStatus.Submitted);

        var response = await (await Staff()).PostAsJsonAsync($"/claims/{claim.Id}/estimates", new
        {
            estimatedAmount = 1m,
            lines = new[] { Line("Part", "กันชนหน้า", 2, 100m) },
        });

        var estimate = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(200m, estimate.GetProperty("estimatedAmount").GetDecimal());
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    [InlineData("InRepair")]
    [InlineData("Repaired")]
    [InlineData("InvoiceSubmitted")]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public async Task A_repair_estimate_is_refused_with_a_409_problem_unless_the_claim_is_Submitted_or_ChangesRequested(string status)
    {
        var claim = SeedClaim(Enum.Parse<ClaimStatus>(status));

        var response = await (await Staff()).PostAsJsonAsync($"/claims/{claim.Id}/estimates", TwoLines());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(status, problem.GetProperty("detail").GetString());
        Assert.DoesNotContain(_factory.Estimates.All, e => e.ClaimId == claim.Id);
        Assert.DoesNotContain(_factory.History.All, e => e.ClaimId == claim.Id);
    }

    [Fact]
    public async Task A_first_estimate_is_accepted_while_the_claim_is_UnderReview_and_changes_no_status()
    {
        var claim = SeedClaim(ClaimStatus.UnderReview);   // an Adjuster picked it up before any estimate existed

        var response = await (await Staff()).PostAsJsonAsync($"/claims/{claim.Id}/estimates", TwoLines());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt32());
        Assert.Equal(ClaimStatus.UnderReview, _factory.Claims.FindByIdAsync(claim.Id, default).Result!.Status);
        Assert.DoesNotContain(_factory.History.All, e => e.ClaimId == claim.Id);
    }

    [Fact]
    public async Task A_second_estimate_is_still_refused_while_the_claim_is_UnderReview()
    {
        var claim = SeedClaim(ClaimStatus.UnderReview);
        _factory.Estimates.Append(new RepairEstimate([new EstimateLine(EstimateLineKind.Part, "กันชน", 1, 100m)])
            { ClaimId = claim.Id, SubmittedByUserId = 1, SubmittedAt = DateTime.UtcNow });

        var response = await (await Staff()).PostAsJsonAsync($"/claims/{claim.Id}/estimates", TwoLines());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(_factory.Estimates.All, e => e.ClaimId == claim.Id);
    }

    [Fact]
    public async Task A_new_submission_adds_a_revision_and_every_earlier_revision_is_kept_unchanged()
    {
        var claim = SeedClaim(ClaimStatus.Submitted);
        var staff = await Staff();
        await staff.PostAsJsonAsync($"/claims/{claim.Id}/estimates", TwoLines());

        var second = await staff.PostAsJsonAsync($"/claims/{claim.Id}/estimates",
            new { lines = new[] { Line("Part", "กันชนหน้า", 1, 3000m) } });

        Assert.Equal(2, (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt32());
        var list = (await (await staff.GetAsync($"/claims/{claim.Id}/estimates")).Content.ReadFromJsonAsync<JsonElement>())
            .EnumerateArray().ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal(5500.50m, list[0].GetProperty("estimatedAmount").GetDecimal());
        Assert.Equal(2, list[0].GetProperty("lines").GetArrayLength());
        Assert.False(list[0].GetProperty("isLatest").GetBoolean());
        Assert.Equal(3000m, list[1].GetProperty("estimatedAmount").GetDecimal());
        Assert.True(list[1].GetProperty("isLatest").GetBoolean());
    }

    [Fact]
    public async Task An_estimate_above_the_coverage_limit_is_still_accepted_but_carries_a_warning()
    {
        var claim = SeedClaim(ClaimStatus.Submitted, policyId: 2);
        var staff = await Staff();

        var response = await staff.PostAsJsonAsync($"/claims/{claim.Id}/estimates",
            new { lines = new[] { Line("Part", "เครื่องยนต์", 1, 10_000.01m) } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var estimate = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(estimate.GetProperty("exceedsCoverageLimit").GetBoolean());
        Assert.Equal(10_000m, estimate.GetProperty("coverageLimit").GetDecimal());
        Assert.Single(estimate.GetProperty("warnings").EnumerateArray());
        var listed = (await (await staff.GetAsync($"/claims/{claim.Id}/estimates")).Content.ReadFromJsonAsync<JsonElement>())[0];
        Assert.True(listed.GetProperty("exceedsCoverageLimit").GetBoolean());
    }

    [Fact]
    public async Task An_estimate_equal_to_the_coverage_limit_has_no_warning()
    {
        var claim = SeedClaim(ClaimStatus.Submitted, policyId: 2);

        var response = await (await Staff()).PostAsJsonAsync($"/claims/{claim.Id}/estimates",
            new { lines = new[] { Line("Part", "เครื่องยนต์", 1, 10_000m) } });

        var estimate = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(estimate.GetProperty("exceedsCoverageLimit").GetBoolean());
        Assert.Empty(estimate.GetProperty("warnings").EnumerateArray());
    }

    [Fact]
    public async Task Resubmitting_after_changes_were_requested_returns_the_claim_to_review_with_history_and_a_customer_email()
    {
        var claim = SeedClaim(ClaimStatus.ChangesRequested);
        var staff = await Staff();

        var response = await staff.PostAsJsonAsync($"/claims/{claim.Id}/estimates", TwoLines());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var fetched = await (await staff.GetAsync($"/claims/{claim.Id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("UnderReview", fetched.GetProperty("status").GetString());
        var entry = Assert.Single(_factory.History.All, e => e.ClaimId == claim.Id);
        Assert.Equal(ClaimStatus.ChangesRequested, entry.FromStatus);
        Assert.Equal(ClaimStatus.UnderReview, entry.ToStatus);
        Assert.Contains("Repair Estimate", entry.Reason);
        var email = Assert.Single(_factory.Outbox.Messages, m => m.Message.ClaimId == claim.Id);
        Assert.Contains("กำลังพิจารณา", email.Message.Subject);
    }

    [Fact]
    public async Task Submitting_the_first_estimate_leaves_the_claim_Submitted_and_writes_no_history_or_email()
    {
        var claim = SeedClaim(ClaimStatus.Submitted);
        var staff = await Staff();

        await staff.PostAsJsonAsync($"/claims/{claim.Id}/estimates", TwoLines());

        var fetched = await (await staff.GetAsync($"/claims/{claim.Id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Submitted", fetched.GetProperty("status").GetString());
        Assert.DoesNotContain(_factory.History.All, e => e.ClaimId == claim.Id);
        Assert.DoesNotContain(_factory.Outbox.Messages, m => m.Message.ClaimId == claim.Id);
    }

    [Theory]
    [InlineData("adjuster@claim.test")]
    [InlineData("admin@claim.test")]
    public async Task Only_garage_staff_may_submit_an_estimate(string email)
    {
        var claim = SeedClaim(ClaimStatus.Submitted);

        var response = await (await As(email)).PostAsJsonAsync($"/claims/{claim.Id}/estimates", TwoLines());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_adjuster_sees_every_revision_with_the_latest_marked_but_never_those_of_a_draft()
    {
        var staff = await Staff();
        var adjuster = await As("adjuster@claim.test");
        var claim = SeedClaim(ClaimStatus.Submitted);
        await staff.PostAsJsonAsync($"/claims/{claim.Id}/estimates", TwoLines());
        await staff.PostAsJsonAsync($"/claims/{claim.Id}/estimates", TwoLines());
        var draft = SeedClaim(ClaimStatus.Draft);

        var response = await adjuster.GetAsync($"/claims/{claim.Id}/estimates");

        var list = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.Equal([false, true], list.Select(e => e.GetProperty("isLatest").GetBoolean()));
        Assert.Equal(HttpStatusCode.NotFound, (await adjuster.GetAsync($"/claims/{draft.Id}/estimates")).StatusCode);
    }

    [Fact]
    public async Task An_estimate_for_an_unknown_claim_is_a_404_problem()
    {
        var response = await (await Staff()).PostAsJsonAsync("/claims/99999/estimates", TwoLines());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static TheoryData<string, object> InvalidEstimates => new()
    {
        { "lines", new { lines = Array.Empty<object>() } },
        { "lines", new { } },
        { "lines[0].kind", new { lines = new[] { Line("Paint", "สี", 1, 100m) } } },
        { "lines[0].description", new { lines = new[] { Line("Part", "  ", 1, 100m) } } },
        { "lines[0].description", new { lines = new[] { Line("Part", new string('ก', 201), 1, 100m) } } },
        { "lines[0].quantity", new { lines = new[] { Line("Part", "กันชน", 0, 100m) } } },
        { "lines[0].quantity", new { lines = new[] { Line("Part", "กันชน", -1, 100m) } } },
        { "lines[0].unitPrice", new { lines = new[] { Line("Part", "กันชน", 1, -5m) } } },
        { "lines[0].unitPrice", new { lines = new[] { new { kind = "Part", description = "กันชน", quantity = (decimal?)1m, unitPrice = (decimal?)null } } } },
    };

    [Theory]
    [MemberData(nameof(InvalidEstimates))]
    public async Task An_invalid_estimate_is_a_400_validation_problem_naming_the_field_and_stores_nothing(string field, object body)
    {
        var claim = SeedClaim(ClaimStatus.ChangesRequested);

        var response = await (await Staff()).PostAsJsonAsync($"/claims/{claim.Id}/estimates", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out _), $"expected an error for {field}");
        Assert.DoesNotContain(_factory.Estimates.All, e => e.ClaimId == claim.Id);
        Assert.DoesNotContain(_factory.History.All, e => e.ClaimId == claim.Id);
    }
}
