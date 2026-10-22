using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Domain.Claims;

namespace Claim.Api.Tests;

public class AdjusterReviewTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdjusterReviewTests(ApiFactory factory)
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
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private Task<HttpClient> Staff() => As("staff@claim.test");
    private Task<HttpClient> Adjuster() => As("adjuster@claim.test");

    /// <summary>The Claim as the store has it now (the object returned by Seed is only a copy of what was seeded).</summary>
    private Domain.Claims.Claim Stored(Domain.Claims.Claim seeded) => _factory.Claims.FindByIdAsync(seeded.Id, default).Result!;

    private Domain.Claims.Claim SeedClaim(ClaimStatus status, long policyId = 1) => _factory.Claims.Seed(status, policyId, 1);

    private async Task<int> SubmitEstimate(long claimId, decimal unitPrice = 10_000m)
    {
        var response = await (await Staff()).PostAsJsonAsync($"/claims/{claimId}/estimates", new
        {
            lines = new[] { new { kind = "Part", description = "กันชนหน้า", quantity = 1m, unitPrice } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt32();
    }

    private async Task<Domain.Claims.Claim> UnderReviewWithEstimate(long policyId = 1, decimal unitPrice = 10_000m)
    {
        var claim = SeedClaim(ClaimStatus.Submitted, policyId);
        await SubmitEstimate(claim.Id, unitPrice);
        Assert.Equal(HttpStatusCode.OK, (await (await Adjuster()).PostAsync($"/claims/{claim.Id}/review", null)).StatusCode);
        return claim;
    }

    [Fact]
    public async Task Adjuster_picks_up_a_submitted_claim_and_it_becomes_UnderReview_with_history_and_email()
    {
        var claim = SeedClaim(ClaimStatus.Submitted);

        var response = await (await Adjuster()).PostAsync($"/claims/{claim.Id}/review", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("UnderReview", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        var entry = Assert.Single(_factory.History.All, e => e.ClaimId == claim.Id);
        Assert.Equal(ClaimStatus.Submitted, entry.FromStatus);
        Assert.Equal(ClaimStatus.UnderReview, entry.ToStatus);
        Assert.Equal(2, entry.ActorUserId);
        Assert.Contains("กำลังพิจารณา", Assert.Single(_factory.Outbox.Messages, m => m.Message.ClaimId == claim.Id).Message.Subject);
    }

    [Fact]
    public async Task Adjuster_approves_with_an_approved_amount_stored_separately_from_the_estimated_amount()
    {
        var claim = await UnderReviewWithEstimate(unitPrice: 12_000m);

        var response = await (await Adjuster()).PostAsJsonAsync($"/claims/{claim.Id}/approve",
            new { approvedAmount = 9_000m, reason = "หักค่าเสื่อมสภาพ" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", body.GetProperty("status").GetString());
        Assert.Equal(9_000m, body.GetProperty("approvedAmount").GetDecimal());
        var estimate = Assert.Single(await (await Adjuster()).GetFromJsonAsync<JsonElement[]>($"/claims/{claim.Id}/estimates") ?? []);
        Assert.Equal(12_000m, estimate.GetProperty("estimatedAmount").GetDecimal());
        var entry = _factory.History.All.Last(e => e.ClaimId == claim.Id);
        Assert.Equal(ClaimStatus.Approved, entry.ToStatus);
        Assert.Equal("หักค่าเสื่อมสภาพ", entry.Reason);
        var email = _factory.Outbox.Messages.Last(m => m.Message.ClaimId == claim.Id).Message;
        Assert.Contains("อนุมัติ", email.Subject);
        Assert.Contains("หักค่าเสื่อมสภาพ", email.Body);
    }

    [Fact]
    public async Task Approved_amount_above_the_coverage_limit_is_refused_and_the_claim_stays_UnderReview()
    {
        var claim = await UnderReviewWithEstimate(policyId: 2, unitPrice: 15_000m); // Coverage Limit 10,000
        var historyBefore = _factory.History.All.Count(e => e.ClaimId == claim.Id);

        var response = await (await Adjuster()).PostAsJsonAsync($"/claims/{claim.Id}/approve",
            new { approvedAmount = 10_000.01m, reason = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("approvedAmount", await response.Content.ReadAsStringAsync());
        Assert.Equal(ClaimStatus.UnderReview, Stored(claim).Status);
        Assert.Null(Stored(claim).ApprovedAmount);
        Assert.Equal(historyBefore, _factory.History.All.Count(e => e.ClaimId == claim.Id));
    }

    [Fact]
    public async Task Approved_amount_equal_to_the_coverage_limit_is_accepted_even_when_the_estimate_is_higher()
    {
        var claim = await UnderReviewWithEstimate(policyId: 2, unitPrice: 15_000m);

        var response = await (await Adjuster()).PostAsJsonAsync($"/claims/{claim.Id}/approve",
            new { approvedAmount = 10_000m, reason = "จ่ายเต็มวงเงิน" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(10_000m, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("approvedAmount").GetDecimal());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Approving_without_a_reason_is_refused(string? reason)
    {
        var claim = await UnderReviewWithEstimate();

        var response = await (await Adjuster()).PostAsJsonAsync($"/claims/{claim.Id}/approve", new { approvedAmount = 5_000m, reason });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ClaimStatus.UnderReview, Stored(claim).Status);
    }

    [Fact]
    public async Task Approval_is_tied_to_the_latest_revision_and_a_stale_revision_is_refused()
    {
        var claim = SeedClaim(ClaimStatus.Submitted);
        await SubmitEstimate(claim.Id);
        var adjuster = await Adjuster();
        await adjuster.PostAsync($"/claims/{claim.Id}/review", null);
        await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/request-changes", new { comments = "ราคาสูง" });
        await SubmitEstimate(claim.Id, 8_000m); // revision 2, Claim back to UnderReview

        var stale = await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/approve", new { approvedAmount = 5_000m, reason = "ok", revision = 1 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(ClaimStatus.UnderReview, Stored(claim).Status);

        var current = await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/approve", new { approvedAmount = 5_000m, reason = "ok", revision = 2 });
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        var latest = _factory.Estimates.All.Single(e => e.ClaimId == claim.Id && e.Revision == 2);
        Assert.Equal(latest.Id, (await current.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("approvedEstimateId").GetInt64());
    }

    [Fact]
    public async Task A_claim_that_is_not_UnderReview_cannot_be_approved()
    {
        var submitted = SeedClaim(ClaimStatus.Submitted);
        await SubmitEstimate(submitted.Id);

        var response = await (await Adjuster()).PostAsJsonAsync($"/claims/{submitted.Id}/approve", new { approvedAmount = 5_000m, reason = "ok" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ClaimStatus.Submitted, Stored(submitted).Status);
    }

    [Fact]
    public async Task A_claim_without_any_repair_estimate_cannot_be_approved()
    {
        var claim = SeedClaim(ClaimStatus.Submitted);
        var adjuster = await Adjuster();
        await adjuster.PostAsync($"/claims/{claim.Id}/review", null);

        var response = await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/approve", new { approvedAmount = 5_000m, reason = "ok" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ClaimStatus.UnderReview, Stored(claim).Status);
    }

    [Fact]
    public async Task Adjuster_rejects_with_a_reason_and_the_reason_reaches_history_and_the_customer_email()
    {
        var claim = await UnderReviewWithEstimate();

        var response = await (await Adjuster()).PostAsJsonAsync($"/claims/{claim.Id}/reject", new { reason = "ไม่อยู่ในความคุ้มครอง" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Rejected", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        var entry = _factory.History.All.Last(e => e.ClaimId == claim.Id);
        Assert.Equal((ClaimStatus.UnderReview, ClaimStatus.Rejected, "ไม่อยู่ในความคุ้มครอง"), (entry.FromStatus!.Value, entry.ToStatus, entry.Reason));
        var email = _factory.Outbox.Messages.Last(m => m.Message.ClaimId == claim.Id).Message;
        Assert.Contains("ปฏิเสธ", email.Subject);
        Assert.Contains("ไม่อยู่ในความคุ้มครอง", email.Body);
    }

    [Fact]
    public async Task Rejecting_without_a_reason_is_refused()
    {
        var claim = await UnderReviewWithEstimate();

        var response = await (await Adjuster()).PostAsJsonAsync($"/claims/{claim.Id}/reject", new { reason = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ClaimStatus.UnderReview, Stored(claim).Status);
    }

    [Fact]
    public async Task A_rejected_claim_is_final_and_cannot_be_reopened_in_any_way()
    {
        var claim = await UnderReviewWithEstimate();
        var adjuster = await Adjuster();
        await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/reject", new { reason = "ปฏิเสธ" });
        var historyCount = _factory.History.All.Count(e => e.ClaimId == claim.Id);

        var attempts = new[]
        {
            await adjuster.PostAsync($"/claims/{claim.Id}/review", null),
            await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/approve", new { approvedAmount = 1_000m, reason = "ลองใหม่" }),
            await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/request-changes", new { comments = "แก้ไข" }),
            await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/reject", new { reason = "อีกครั้ง" }),
            await (await Staff()).PostAsJsonAsync($"/claims/{claim.Id}/estimates", new { lines = new[] { new { kind = "Part", description = "x", quantity = 1m, unitPrice = 1m } } }),
            await (await Staff()).PostAsync($"/claims/{claim.Id}/submit", null),
        };

        Assert.All(attempts, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(ClaimStatus.Rejected, Stored(claim).Status);
        Assert.Equal(historyCount, _factory.History.All.Count(e => e.ClaimId == claim.Id));
    }

    [Fact]
    public async Task Changes_requested_then_a_new_revision_loops_back_to_review_any_number_of_times_and_the_adjuster_sees_every_revision()
    {
        var claim = SeedClaim(ClaimStatus.Submitted);
        var adjuster = await Adjuster();
        await SubmitEstimate(claim.Id, 10_000m);
        await adjuster.PostAsync($"/claims/{claim.Id}/review", null);

        for (var round = 1; round <= 3; round++)
        {
            var asked = await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/request-changes", new { comments = $"ลดราคารอบ {round}" });
            Assert.Equal(HttpStatusCode.OK, asked.StatusCode);
            Assert.Equal(ClaimStatus.ChangesRequested, Stored(claim).Status);
            await SubmitEstimate(claim.Id, 10_000m - round * 1_000m);
            Assert.Equal(ClaimStatus.UnderReview, Stored(claim).Status);
        }

        var revisions = await adjuster.GetFromJsonAsync<JsonElement[]>($"/claims/{claim.Id}/estimates") ?? [];
        Assert.Equal([1, 2, 3, 4], revisions.Select(r => r.GetProperty("revision").GetInt32()));
        Assert.Equal([false, false, false, true], revisions.Select(r => r.GetProperty("isLatest").GetBoolean()));
        Assert.Equal(7_000m, revisions[^1].GetProperty("estimatedAmount").GetDecimal());

        var done = await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/approve", new { approvedAmount = 7_000m, reason = "ตกลงราคา", revision = 4 });
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        var statuses = _factory.History.All.Where(e => e.ClaimId == claim.Id).Select(e => e.ToStatus).ToList();
        Assert.Equal(
            [ClaimStatus.UnderReview, ClaimStatus.ChangesRequested, ClaimStatus.UnderReview, ClaimStatus.ChangesRequested, ClaimStatus.UnderReview,
             ClaimStatus.ChangesRequested, ClaimStatus.UnderReview, ClaimStatus.Approved], statuses);
        Assert.Equal(statuses.Count, _factory.Outbox.Messages.Count(m => m.Message.ClaimId == claim.Id));
    }

    [Fact]
    public async Task Requesting_changes_needs_comments_and_carries_them_to_the_customer_email()
    {
        var claim = await UnderReviewWithEstimate();
        var adjuster = await Adjuster();

        var missing = await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/request-changes", new { comments = "" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(ClaimStatus.UnderReview, Stored(claim).Status);

        await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/request-changes", new { comments = "ขอใบเสนอราคาอู่อื่นเทียบ" });
        var email = _factory.Outbox.Messages.Last(m => m.Message.ClaimId == claim.Id).Message;
        Assert.Contains("ขอให้แก้ไข", email.Subject);
        Assert.Contains("ขอใบเสนอราคาอู่อื่นเทียบ", email.Body);
    }

    [Theory]
    [InlineData("staff@claim.test")]
    [InlineData("admin@claim.test")]
    public async Task Only_an_insurance_adjuster_may_pick_up_approve_reject_or_request_changes(string email)
    {
        var claim = SeedClaim(ClaimStatus.UnderReview);
        var client = await As(email);

        var responses = new[]
        {
            await client.PostAsync($"/claims/{claim.Id}/review", null),
            await client.PostAsJsonAsync($"/claims/{claim.Id}/approve", new { approvedAmount = 1m, reason = "x" }),
            await client.PostAsJsonAsync($"/claims/{claim.Id}/reject", new { reason = "x" }),
            await client.PostAsJsonAsync($"/claims/{claim.Id}/request-changes", new { comments = "x" }),
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
        Assert.Equal(ClaimStatus.UnderReview, Stored(claim).Status);
        Assert.DoesNotContain(_factory.History.All, e => e.ClaimId == claim.Id);
    }

    [Fact]
    public async Task An_adjuster_cannot_act_on_a_draft_claim()
    {
        var draft = SeedClaim(ClaimStatus.Draft);

        var response = await (await Adjuster()).PostAsync($"/claims/{draft.Id}/review", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ClaimStatus.Draft, Stored(draft).Status);
    }
}
