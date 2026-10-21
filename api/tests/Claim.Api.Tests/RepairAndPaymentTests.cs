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
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private Task<HttpClient> Staff() => As("staff@claim.test");
    private Task<HttpClient> Adjuster() => As("adjuster@claim.test");
    private Task<HttpClient> Admin() => As("admin@claim.test");

    private Domain.Claims.Claim Seed(ClaimStatus status, decimal? approved = null)
    {
        var claim = _factory.Claims.Seed(status, 1, 1);
        if (approved is { } amount) claim.RestoreApproval(amount, 1);
        return claim;
    }

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
