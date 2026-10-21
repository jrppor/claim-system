using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Domain.Claims;

namespace Claim.Api.Tests;

public class CancelClaimTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CancelClaimTests(ApiFactory factory)
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
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private Task<HttpClient> Staff() => As("staff@claim.test");

    private Domain.Claims.Claim SeedClaim(ClaimStatus status) => _factory.Claims.Seed(status, 1, 1);

    [Theory]
    [InlineData(ClaimStatus.Draft)]
    [InlineData(ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.ChangesRequested)]
    public async Task Garage_staff_can_cancel_from_every_status_before_approval(ClaimStatus from)
    {
        var claim = SeedClaim(from);

        var response = await (await Staff()).PostAsJsonAsync($"/claims/{claim.Id}/cancel", new { reason = "เปิดผิดเรื่อง" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(from, Assert.Single(_factory.History.All, e => e.ClaimId == claim.Id).FromStatus);
    }

    [Theory]
    [InlineData(ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Rejected)]
    [InlineData(ClaimStatus.InRepair)]
    [InlineData(ClaimStatus.Repaired)]
    [InlineData(ClaimStatus.InvoiceSubmitted)]
    [InlineData(ClaimStatus.Paid)]
    [InlineData(ClaimStatus.Cancelled)]
    public async Task Cancelling_from_approved_or_later_is_refused_with_409_and_nothing_is_recorded(ClaimStatus from)
    {
        var claim = SeedClaim(from);

        var response = await (await Staff()).PostAsJsonAsync($"/claims/{claim.Id}/cancel", new { reason = "ไม่เอาแล้ว" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.DoesNotContain(_factory.History.All, e => e.ClaimId == claim.Id);
        Assert.DoesNotContain(_factory.Outbox.Messages, m => m.Message.ClaimId == claim.Id);
    }

    [Theory]
    [InlineData("adjuster@claim.test")]
    [InlineData("admin@claim.test")]
    public async Task Other_roles_cannot_cancel(string email)
    {
        var claim = SeedClaim(ClaimStatus.Submitted);

        var response = await (await As(email)).PostAsJsonAsync($"/claims/{claim.Id}/cancel", new { reason = "x" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_reason_is_required_to_cancel(string? reason)
    {
        var claim = SeedClaim(ClaimStatus.Submitted);

        var response = await (await Staff()).PostAsJsonAsync($"/claims/{claim.Id}/cancel", new { reason });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(_factory.History.All, e => e.ClaimId == claim.Id);
    }

    [Fact]
    public async Task A_cancelled_claim_stays_visible_but_accepts_no_edits_estimates_or_review_actions()
    {
        var claim = SeedClaim(ClaimStatus.Submitted);
        var staff = await Staff();
        await staff.PostAsJsonAsync($"/claims/{claim.Id}/cancel", new { reason = "ถอนเรื่อง" });
        var adjuster = await As("adjuster@claim.test");

        var detail = await staff.GetAsync($"/claims/{claim.Id}");
        var asAdjuster = await adjuster.GetAsync($"/claims/{claim.Id}");
        var history = await staff.GetFromJsonAsync<JsonElement[]>($"/claims/{claim.Id}/history");
        var edit = await staff.PutAsJsonAsync($"/claims/{claim.Id}", new { policyId = 1, vehiclePlate = "x" });
        var estimate = await staff.PostAsJsonAsync($"/claims/{claim.Id}/estimates",
            new { lines = new[] { new { kind = "Part", description = "a", quantity = 1m, unitPrice = 1m } } });
        var review = await adjuster.PostAsync($"/claims/{claim.Id}/review", null);
        var approve = await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/approve", new { approvedAmount = 1m, reason = "x" });
        var reject = await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/reject", new { reason = "x" });
        var changes = await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/request-changes", new { comments = "x" });

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asAdjuster.StatusCode);
        Assert.Single(history!);
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, estimate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, review.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reject.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changes.StatusCode);
        Assert.Single(_factory.History.All, e => e.ClaimId == claim.Id);
    }

    [Fact]
    public async Task Garage_staff_cancels_a_submitted_claim_with_a_reason_and_history_and_email_are_recorded()
    {
        var claim = SeedClaim(ClaimStatus.Submitted);

        var response = await (await Staff()).PostAsJsonAsync($"/claims/{claim.Id}/cancel", new { reason = "ลูกค้าถอนเรื่อง" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Cancelled", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        var entry = Assert.Single(_factory.History.All, e => e.ClaimId == claim.Id);
        Assert.Equal(ClaimStatus.Submitted, entry.FromStatus);
        Assert.Equal(ClaimStatus.Cancelled, entry.ToStatus);
        Assert.Equal("ลูกค้าถอนเรื่อง", entry.Reason);
        var email = Assert.Single(_factory.Outbox.Messages, m => m.Message.ClaimId == claim.Id).Message;
        Assert.Contains("ยกเลิก", email.Subject);
        Assert.Contains("ลูกค้าถอนเรื่อง", email.Body);
    }
}
