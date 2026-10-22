using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Claim.Api.Tests;

/// <summary>Pins request-binding edge cases (odd bodies, content types, query values) so changing the web framework cannot silently change them.</summary>
public class BindingCharacterizationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public BindingCharacterizationTests(ApiFactory factory)
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

    private static HttpRequestMessage Req(HttpMethod m, string url, string? body, string? contentType = "application/json")
    {
        var r = new HttpRequestMessage(m, url);
        if (body is not null)
        {
            r.Content = new StringContent(body, Encoding.UTF8);
            r.Content.Headers.ContentType = contentType is null ? null : new MediaTypeHeaderValue(contentType);
        }
        return r;
    }

    private static readonly object Draft = new
    {
        policyId = 1, vehiclePlate = "1กก 1234", vehicleMake = "Toyota", vehicleModel = "Camry",
        customerEmail = "somsak@customer.test", damageDescription = "กันชนหน้าบุบ",
    };

    private async Task<(HttpClient Staff, long Id, string Version)> NewDraft()
    {
        var staff = await As("staff@claim.test");
        var created = await staff.PostAsJsonAsync("/claims", Draft);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        return (staff, body.GetProperty("id").GetInt64(), body.GetProperty("version").GetString()!);
    }

    private static async Task AssertProblem(HttpResponseMessage res, HttpStatusCode status)
    {
        Assert.Equal(status, res.StatusCode);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("staff@claim.test", "/claims")]
    [InlineData("admin@claim.test", "/users")]
    [InlineData("adjuster@claim.test", "/claims/1/reject")]
    public async Task A_body_that_is_not_JSON_text_is_415(string who, string url)
    {
        var res = await (await As(who)).SendAsync(Req(HttpMethod.Post, url, "{}", "text/plain"));
        await AssertProblem(res, HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Login_with_a_non_JSON_body_is_415()
    {
        var res = await _factory.CreateClient().SendAsync(Req(HttpMethod.Post, "/auth/login", "{}", "text/plain"));
        await AssertProblem(res, HttpStatusCode.UnsupportedMediaType);
    }

    // Deliberate change from the Minimal API host, where these were unhandled BadHttpRequestExceptions answered as 500.
    [Theory]
    [InlineData("staff@claim.test", "POST", "/claims", "{bad")]
    [InlineData("staff@claim.test", "POST", "/claims", "")]
    [InlineData("staff@claim.test", "POST", "/claims", "{\"policyId\":\"x\"}")]
    [InlineData("admin@claim.test", "POST", "/users", "{bad")]
    [InlineData("adjuster@claim.test", "POST", "/claims/1/reject", "{bad")]
    public async Task A_malformed_or_empty_JSON_body_is_a_400_problem(string who, string method, string url, string body)
    {
        var res = await (await As(who)).SendAsync(Req(new HttpMethod(method), url, body));
        await AssertProblem(res, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_malformed_login_body_is_a_400_problem()
    {
        var res = await _factory.CreateClient().SendAsync(Req(HttpMethod.Post, "/auth/login", "{bad"));
        await AssertProblem(res, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_non_numeric_page_is_a_400_problem()
    {
        var res = await (await As("staff@claim.test")).GetAsync("/claims?page=abc");
        await AssertProblem(res, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_non_numeric_id_in_the_route_is_404()
    {
        var staff = await As("staff@claim.test");
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync("/claims/abc")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await (await As("admin@claim.test")).PostAsync("/users/abc/disable", null)).StatusCode);
    }

    [Fact]
    public async Task Property_names_in_request_JSON_match_case_insensitively()
    {
        var res = await (await As("staff@claim.test")).SendAsync(Req(HttpMethod.Post, "/claims",
            "{\"POLICYID\":1,\"VehiclePlate\":\"1กก 1234\",\"vehicleMake\":\"T\",\"vehicleModel\":\"C\",\"customerEmail\":\"somsak@customer.test\",\"damageDescription\":\"d\"}"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var claim = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("1กก 1234", claim.GetProperty("vehiclePlate").GetString());
    }

    [Fact]
    public async Task An_empty_object_still_gets_the_services_own_validation_errors()
    {
        var res = await (await As("staff@claim.test")).SendAsync(Req(HttpMethod.Post, "/claims", "{}"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("Policy is required.", errors.GetProperty("policyId")[0].GetString());
    }

    [Theory]
    [InlineData("text/plain", "ignored")]
    [InlineData("application/json", "")]
    [InlineData("application/json", "null")]
    [InlineData(null, null)]
    public async Task Submit_accepts_a_missing_empty_or_non_JSON_body(string? contentType, string? body)
    {
        var (staff, id, version) = await NewDraft();
        var req = Req(HttpMethod.Post, $"/claims/{id}/submit", body, contentType);
        req.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");

        var res = await staff.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Submit_accepts_a_chunked_JSON_reason()
    {
        var (staff, id, version) = await NewDraft();
        var req = new HttpRequestMessage(HttpMethod.Post, $"/claims/{id}/submit")
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("{\"reason\":\"ส่งแล้ว\"}"))),
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        req.Headers.TransferEncodingChunked = true;
        req.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");

        var res = await staff.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var history = await (await staff.GetAsync($"/claims/{id}/history")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ส่งแล้ว", history[0].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A_missing_If_Match_is_428_even_with_an_odd_body()
    {
        var (staff, id, _) = await NewDraft();
        var res = await staff.SendAsync(Req(HttpMethod.Post, $"/claims/{id}/submit", "ignored", "text/plain"));
        await AssertProblem(res, HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task List_with_out_of_range_paging_is_clamped_not_rejected()
    {
        var res = await (await As("staff@claim.test")).GetAsync("/claims?page=-1&pageSize=0");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
