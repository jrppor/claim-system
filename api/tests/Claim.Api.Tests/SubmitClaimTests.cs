using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Claim.Api.Tests;

public class SubmitClaimTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SubmitClaimTests(ApiFactory factory)
    {
        _factory = factory;
        if (_factory.Policies.SearchAsync("AB-1001", default).Result.Count == 0)
            _factory.Policies.Add("AB-1001", "1กก 1234", 500_000m, "somsak@customer.test");
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

    private static object CompleteDraft(string email = "somsak@customer.test") => new
    {
        policyId = 1,
        vehiclePlate = "1กก 1234",
        vehicleMake = "Toyota",
        vehicleModel = "Camry",
        customerEmail = email,
        damageDescription = "กันชนหน้าบุบ",
    };

    private static async Task<long> CreateDraft(HttpClient staff, object? input = null)
    {
        var created = await staff.PostAsJsonAsync("/claims", input ?? CompleteDraft());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    [Fact]
    public async Task Staff_submits_a_complete_draft_and_it_becomes_Submitted()
    {
        var staff = await Staff();
        var id = await CreateDraft(staff);

        var response = await staff.PostAsync($"/claims/{id}/submit", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var claim = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Submitted", claim.GetProperty("status").GetString());
        var fetched = await (await staff.GetAsync($"/claims/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Submitted", fetched.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Submitting_records_who_when_and_what_in_the_claim_history()
    {
        var staff = await Staff();
        var id = await CreateDraft(staff);
        var before = DateTime.UtcNow.AddSeconds(-1);

        await staff.PostAsync($"/claims/{id}/submit", null);

        var response = await staff.GetAsync($"/claims/{id}/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        var entry = Assert.Single(history);
        Assert.Equal("Draft", entry.GetProperty("fromStatus").GetString());
        Assert.Equal("Submitted", entry.GetProperty("toStatus").GetString());
        Assert.Equal("staff", entry.GetProperty("actorName").GetString());
        Assert.True(entry.GetProperty("occurredAt").GetDateTime() >= before);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("reason").ValueKind);
    }

    [Fact]
    public async Task Submitting_queues_one_thai_email_to_the_customer_naming_the_claim_number_and_new_status()
    {
        var staff = await Staff();
        var id = await CreateDraft(staff, CompleteDraft("customer-a@claim.test"));
        var claim = await (await staff.GetAsync($"/claims/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        var number = claim.GetProperty("claimNumber").GetString()!;

        await staff.PostAsync($"/claims/{id}/submit", null);

        var email = Assert.Single(_factory.Outbox.Messages, m => m.Message.ClaimId == id);
        Assert.Equal("customer-a@claim.test", email.Message.ToAddress);
        Assert.Contains(number, email.Message.Subject);
        Assert.Contains("ส่งแล้ว", email.Message.Subject);
        Assert.Contains(number, email.Message.Body);
        Assert.Contains("ส่งแล้ว", email.Message.Body);
        Assert.Contains("สมศักดิ์ ใจดี", email.Message.Body);
        Assert.DoesNotContain("เหตุผล", email.Message.Body);
    }

    [Fact]
    public async Task A_reason_given_on_submit_is_stored_in_history_and_stated_in_the_email()
    {
        var staff = await Staff();
        var id = await CreateDraft(staff, CompleteDraft("customer-b@claim.test"));

        var response = await staff.PostAsJsonAsync($"/claims/{id}/submit", new { reason = "ลูกค้ารีบใช้รถ" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entry = Assert.Single(_factory.History.All, e => e.ClaimId == id);
        Assert.Equal("ลูกค้ารีบใช้รถ", entry.Reason);
        var email = Assert.Single(_factory.Outbox.Messages, m => m.Message.ClaimId == id);
        Assert.Contains("เหตุผล: ลูกค้ารีบใช้รถ", email.Message.Body);
    }

    [Fact]
    public async Task The_queued_email_is_sent_in_the_background_and_marked_sent()
    {
        var staff = await Staff();
        var id = await CreateDraft(staff, CompleteDraft("customer-c@claim.test"));

        await staff.PostAsync($"/claims/{id}/submit", null);

        await _factory.DispatchEmailsAsync();

        var sent = Assert.Single(_factory.Mailbox.Sent, m => m.ClaimId == id);
        Assert.Equal("customer-c@claim.test", sent.ToAddress);
        Assert.NotNull(_factory.Outbox.Messages.Single(m => m.Message.ClaimId == id).SentAt);
    }

    [Fact]
    public async Task Submitting_succeeds_while_the_mail_server_is_down_and_the_email_goes_out_once_it_is_back()
    {
        var staff = await Staff();
        var id = await CreateDraft(staff, CompleteDraft("customer-d@claim.test"));
        _factory.Mailbox.IsDown = true;
        try
        {
            var response = await staff.PostAsync($"/claims/{id}/submit", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await _factory.DispatchEmailsAsync();
            _factory.Clock.Advance(TimeSpan.FromMinutes(1));   // past the retry back-off
            await _factory.DispatchEmailsAsync();
            var row = _factory.Outbox.Messages.Single(m => m.Message.ClaimId == id);
            Assert.Null(row.SentAt);
            Assert.Contains("unreachable", row.LastError);
            Assert.DoesNotContain(_factory.Mailbox.Sent, m => m.ClaimId == id);
        }
        finally
        {
            _factory.Mailbox.IsDown = false;
        }

        _factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await _factory.DispatchEmailsAsync();
        Assert.Contains(_factory.Mailbox.Sent, m => m.ClaimId == id);
        var fetched = await (await staff.GetAsync($"/claims/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Submitted", fetched.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Refused_submissions_leave_no_history_and_queue_no_email()
    {
        var staff = await Staff();
        var incomplete = await CreateDraft(staff, new { policyId = 1 });
        var alreadySubmitted = _factory.Claims.Seed(Domain.Claims.ClaimStatus.Approved, 1, 1);

        await staff.PostAsync($"/claims/{incomplete}/submit", null);
        await staff.PostAsync($"/claims/{alreadySubmitted.Id}/submit", null);

        Assert.DoesNotContain(_factory.History.All, e => e.ClaimId == incomplete || e.ClaimId == alreadySubmitted.Id);
        Assert.DoesNotContain(_factory.Outbox.Messages, m => m.Message.ClaimId == incomplete || m.Message.ClaimId == alreadySubmitted.Id);
    }

    [Theory]
    [InlineData("adjuster@claim.test")]
    [InlineData("admin@claim.test")]
    public async Task Only_garage_staff_may_submit(string email)
    {
        var id = await CreateDraft(await Staff());
        var other = await As(email);

        var response = await other.PostAsync($"/claims/{id}/submit", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("application/x-www-form-urlencoded")]
    [InlineData("application/json")]
    public async Task Submit_works_with_an_empty_body_whatever_content_type_the_client_sends(string contentType)
    {
        var staff = await Staff();
        var id = await CreateDraft(staff);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/claims/{id}/submit") { Content = new StringContent("") };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var response = await staff.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_reason_longer_than_2000_characters_is_a_validation_problem()
    {
        var staff = await Staff();
        var id = await CreateDraft(staff);

        var response = await staff.PostAsJsonAsync($"/claims/{id}/submit", new { reason = new string('ก', 2001) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("reason", out _));
    }

    [Fact]
    public async Task Submitting_an_unknown_claim_is_a_404_problem()
    {
        var response = await (await Staff()).PostAsync("/claims/99999/submit", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_adjuster_cannot_read_the_history_of_a_draft_but_can_once_it_is_submitted()
    {
        var staff = await Staff();
        var adjuster = await As("adjuster@claim.test");
        var id = await CreateDraft(staff);

        Assert.Equal(HttpStatusCode.NotFound, (await adjuster.GetAsync($"/claims/{id}/history")).StatusCode);

        await staff.PostAsync($"/claims/{id}/submit", null);
        var response = await adjuster.GetAsync($"/claims/{id}/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single((await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [Theory]
    [InlineData("Submitted")]
    [InlineData("UnderReview")]
    [InlineData("ChangesRequested")]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    [InlineData("InRepair")]
    [InlineData("Repaired")]
    [InlineData("InvoiceSubmitted")]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public async Task Submitting_a_claim_that_is_not_a_draft_is_refused_with_a_409_problem_and_nothing_changes(string status)
    {
        var staff = await Staff();
        var seeded = _factory.Claims.Seed(Enum.Parse<Domain.Claims.ClaimStatus>(status), 1, 1);

        var response = await staff.PostAsync($"/claims/{seeded.Id}/submit", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        Assert.Contains(status, problem.GetProperty("detail").GetString());
        var fetched = await (await staff.GetAsync($"/claims/{seeded.Id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(status, fetched.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Submitting_an_incomplete_draft_is_rejected_naming_what_is_missing_and_the_draft_stays_a_draft()
    {
        var staff = await Staff();
        var id = await CreateDraft(staff, new { policyId = 1, vehiclePlate = "1กก 1234", customerEmail = "somsak@customer.test" });

        var response = await staff.PostAsync($"/claims/{id}/submit", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("vehicleMake", out _));
        Assert.True(errors.TryGetProperty("vehicleModel", out _));
        Assert.True(errors.TryGetProperty("damageDescription", out _));
        Assert.False(errors.TryGetProperty("vehiclePlate", out _));
        Assert.False(errors.TryGetProperty("customerEmail", out _));
        var fetched = await (await staff.GetAsync($"/claims/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", fetched.GetProperty("status").GetString());
    }
}
