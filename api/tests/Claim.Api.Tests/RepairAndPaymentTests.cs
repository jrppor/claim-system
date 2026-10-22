using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Domain.Claims;

namespace Claim.Api.Tests;

public class RepairAndPaymentTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RepairAndPaymentTests(ApiFactory factory)
    {
        _factory = factory;
        if (_factory.Policies.SearchAsync("RP-1001", default).Result.Count == 0)
            _factory.Policies.Add("RP-1001", "9กก 9999", 500_000m, "repair@customer.test");
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
    private Task<HttpClient> Adjuster() => As("adjuster@claim.test");
    private Task<HttpClient> Admin() => As("admin@claim.test");

    private Domain.Claims.Claim Seed(ClaimStatus status, decimal? approved = null)
        => _factory.Claims.Seed(status, 1, 1, approvedAmount: approved);

    private static async Task<string> StatusOf(HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString()!;

    [Fact]
    public async Task Garage_staff_starts_repair_on_an_approved_claim_with_history_and_email()
    {
        var claim = Seed(ClaimStatus.Approved, 9_000m);

        var response = await (await Staff()).PostAsync($"/claims/{claim.Id}/start-repair", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("InRepair", await StatusOf(response));
        var entry = Assert.Single(_factory.History.All, e => e.ClaimId == claim.Id);
        Assert.Equal(ClaimStatus.Approved, entry.FromStatus);
        Assert.Equal(ClaimStatus.InRepair, entry.ToStatus);
        Assert.Contains("กำลังซ่อม", Assert.Single(_factory.Outbox.Messages, m => m.Message.ClaimId == claim.Id).Message.Subject);
    }

    [Fact]
    public async Task Garage_staff_submits_an_invoice_for_a_repaired_claim_shown_next_to_the_approved_amount()
    {
        var claim = Seed(ClaimStatus.Repaired, 9_000m);

        var response = await (await Staff()).PostAsJsonAsync($"/claims/{claim.Id}/invoice", new { actualAmount = 9_500.50m, note = "เปลี่ยนไฟหน้าเพิ่ม" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invoice = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(9_500.50m, invoice.GetProperty("actualAmount").GetDecimal());
        Assert.Equal(9_000m, invoice.GetProperty("approvedAmount").GetDecimal());
        Assert.Equal(500.50m, invoice.GetProperty("difference").GetDecimal());
        var entry = Assert.Single(_factory.History.All, e => e.ClaimId == claim.Id);
        Assert.Equal(ClaimStatus.Repaired, entry.FromStatus);
        Assert.Equal(ClaimStatus.InvoiceSubmitted, entry.ToStatus);
        Assert.Contains("ส่งใบเรียกเก็บแล้ว", Assert.Single(_factory.Outbox.Messages, m => m.Message.ClaimId == claim.Id).Message.Subject);

        var read = await (await Adjuster()).GetAsync($"/claims/{claim.Id}/invoice");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(9_000m, (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("approvedAmount").GetDecimal());
    }

    [Fact]
    public async Task Adjuster_confirms_payment_and_the_claim_is_Paid_and_immutable()
    {
        var claim = Seed(ClaimStatus.Repaired, 9_000m);
        Assert.Equal(HttpStatusCode.Created, (await (await Staff()).PostAsJsonAsync($"/claims/{claim.Id}/invoice", new { actualAmount = 9_000m })).StatusCode);

        var response = await (await Adjuster()).PostAsJsonAsync($"/claims/{claim.Id}/pay", new { reason = "โอนเงินแล้ว" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Paid", await StatusOf(response));
        var entry = _factory.History.All.Last(e => e.ClaimId == claim.Id);
        Assert.Equal(ClaimStatus.InvoiceSubmitted, entry.FromStatus);
        Assert.Equal(ClaimStatus.Paid, entry.ToStatus);
        Assert.Equal("โอนเงินแล้ว", entry.Reason);
        Assert.Contains("จ่ายแล้ว", _factory.Outbox.Messages.Last(m => m.Message.ClaimId == claim.Id).Message.Subject);

        var historyCount = _factory.History.All.Count(e => e.ClaimId == claim.Id);
        var staff = await Staff();
        var adjuster = await Adjuster();
        var attempts = new[]
        {
            await adjuster.PostAsJsonAsync($"/claims/{claim.Id}/pay", new { }),
            await staff.PostAsync($"/claims/{claim.Id}/start-repair", null),
            await staff.PostAsync($"/claims/{claim.Id}/mark-repaired", null),
            await staff.PostAsJsonAsync($"/claims/{claim.Id}/invoice", new { actualAmount = 1m }),
            await staff.PostAsJsonAsync($"/claims/{claim.Id}/cancel", new { reason = "x" }),
            await staff.PostAsJsonAsync($"/claims/{claim.Id}/estimates", new { lines = new[] { new { kind = "Part", description = "x", quantity = 1m, unitPrice = 1m } } }),
            await staff.PutAsJsonAsync($"/claims/{claim.Id}", new { policyId = 1, vehiclePlate = "x" }),
        };
        Assert.All(attempts, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(historyCount, _factory.History.All.Count(e => e.ClaimId == claim.Id));
        Assert.Equal(9_000m, (await _factory.Invoices.FindByClaimAsync(claim.Id, default))!.ActualAmount);
    }

    [Fact]
    public async Task Each_step_is_refused_with_403_for_the_wrong_role_and_409_out_of_order()
    {
        var approved = Seed(ClaimStatus.Approved, 9_000m);
        var repaired = Seed(ClaimStatus.Repaired, 9_000m);
        var submitted = Seed(ClaimStatus.InvoiceSubmitted, 9_000m);
        var staff = await Staff();
        var adjuster = await Adjuster();
        var admin = await Admin();

        var forbidden = new[]
        {
            await adjuster.PostAsync($"/claims/{approved.Id}/start-repair", null),
            await adjuster.PostAsync($"/claims/{approved.Id}/mark-repaired", null),
            await adjuster.PostAsJsonAsync($"/claims/{repaired.Id}/invoice", new { actualAmount = 1m }),
            await staff.PostAsJsonAsync($"/claims/{submitted.Id}/pay", new { }),
            await admin.PostAsync($"/claims/{approved.Id}/start-repair", null),
            await admin.PostAsJsonAsync($"/claims/{submitted.Id}/pay", new { }),
        };
        Assert.All(forbidden, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));

        var conflict = new[]
        {
            await staff.PostAsJsonAsync($"/claims/{approved.Id}/invoice", new { actualAmount = 1m }),
            await adjuster.PostAsJsonAsync($"/claims/{repaired.Id}/pay", new { }),
            await adjuster.PostAsJsonAsync($"/claims/{approved.Id}/pay", new { }),
            await staff.PostAsync($"/claims/{submitted.Id}/mark-repaired", null),
        };
        Assert.All(conflict, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Empty(_factory.History.All.Where(e => e.ClaimId == approved.Id || e.ClaimId == repaired.Id || e.ClaimId == submitted.Id));
    }

    [Fact]
    public async Task Invoice_amount_must_be_positive_with_at_most_two_decimals()
    {
        var claim = Seed(ClaimStatus.Repaired, 9_000m);
        var staff = await Staff();

        foreach (var amount in new[] { 0m, -5m, 10.123m })
        {
            var response = await staff.PostAsJsonAsync($"/claims/{claim.Id}/invoice", new { actualAmount = amount });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync($"/claims/{claim.Id}/invoice", new { })).StatusCode);
        Assert.Empty(_factory.Invoices.All.Where(i => i.ClaimId == claim.Id));
        Assert.Equal(ClaimStatus.Repaired, _factory.Claims.All.Single(c => c.Id == claim.Id).Status);
    }

    [Fact]
    public async Task Full_flow_from_Draft_to_Paid_through_HTTP_keeps_history_and_customer_emails_in_step()
    {
        var staff = await Staff();
        var adjuster = await Adjuster();

        var created = await staff.PostAsJsonAsync("/claims", new
        {
            policyId = 1, vehiclePlate = "9กก 9999", vehicleMake = "Honda", vehicleModel = "Civic",
            customerEmail = "flow@customer.test", damageDescription = "ประตูซ้ายบุบ",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsync($"/claims/{id}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await staff.PostAsJsonAsync($"/claims/{id}/estimates", new
        {
            lines = new[] { new { kind = "Part", description = "ประตู", quantity = 1m, unitPrice = 12_000m } },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adjuster.PostAsync($"/claims/{id}/review", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adjuster.PostAsJsonAsync($"/claims/{id}/approve", new { approvedAmount = 10_000m, reason = "หักค่าเสื่อม" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsync($"/claims/{id}/start-repair", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsync($"/claims/{id}/mark-repaired", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await staff.PostAsJsonAsync($"/claims/{id}/invoice", new { actualAmount = 11_500m })).StatusCode);

        var invoice = await (await adjuster.GetAsync($"/claims/{id}/invoice")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(11_500m, invoice.GetProperty("actualAmount").GetDecimal());
        Assert.Equal(10_000m, invoice.GetProperty("approvedAmount").GetDecimal());
        Assert.Equal(1_500m, invoice.GetProperty("difference").GetDecimal());

        var paid = await adjuster.PostAsync($"/claims/{id}/pay", null);
        Assert.Equal("Paid", await StatusOf(paid));

        var history = await (await adjuster.GetAsync($"/claims/{id}/history")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            ["Draft", "Submitted", "UnderReview", "Approved", "InRepair", "Repaired", "InvoiceSubmitted", "Paid"],
            history.EnumerateArray().Select(h => h.GetProperty("toStatus").GetString()).Prepend("Draft").ToArray());
        Assert.Equal(7, _factory.Outbox.Messages.Count(m => m.Message.ClaimId == id));
        Assert.All(_factory.Outbox.Messages.Where(m => m.Message.ClaimId == id), m => Assert.Equal("flow@customer.test", m.Message.ToAddress));
    }

    [Fact]
    public async Task Garage_staff_marks_an_in_repair_claim_Repaired_but_cannot_skip_from_Approved()
    {
        var approved = Seed(ClaimStatus.Approved, 9_000m);
        var skip = await (await Staff()).PostAsync($"/claims/{approved.Id}/mark-repaired", null);
        Assert.Equal(HttpStatusCode.Conflict, skip.StatusCode);
        Assert.Empty(_factory.History.All.Where(e => e.ClaimId == approved.Id));

        var inRepair = Seed(ClaimStatus.InRepair, 9_000m);
        var response = await (await Staff()).PostAsync($"/claims/{inRepair.Id}/mark-repaired", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Repaired", await StatusOf(response));
        var entry = Assert.Single(_factory.History.All, e => e.ClaimId == inRepair.Id);
        Assert.Equal(ClaimStatus.Repaired, entry.ToStatus);
        Assert.Contains("ซ่อมเสร็จ", Assert.Single(_factory.Outbox.Messages, m => m.Message.ClaimId == inRepair.Id).Message.Subject);
    }
}
