using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Claim.Api.Tests;

public class WalkingSkeletonTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public WalkingSkeletonTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_reports_healthy_when_database_is_up()
    {
        _factory.Database.IsUp = true;
        _factory.Database.Throws = false;

        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal("Up", body.GetProperty("database").GetString());
    }

    [Fact]
    public async Task Health_reports_owner_names_from_configuration()
    {
        var body = await _client.GetFromJsonAsync<JsonElement>("/health");

        Assert.Equal("Demo Garage", body.GetProperty("garageName").GetString());
        Assert.Equal("Demo Insurance", body.GetProperty("insurerName").GetString());
    }

    [Fact]
    public async Task Health_returns_503_problem_details_when_database_is_down()
    {
        _factory.Database.IsUp = false;
        _factory.Database.Throws = false;
        try
        {
            var response = await _client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
        finally { _factory.Database.IsUp = true; }
    }

    [Fact]
    public async Task Unhandled_exception_becomes_problem_details_without_leaking_detail()
    {
        _factory.Database.Throws = true;
        try
        {
            var response = await _client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("secret connection detail", text);
        }
        finally { _factory.Database.Throws = false; }
    }

    [Fact]
    public async Task Unknown_route_returns_404_problem_details()
    {
        var response = await _client.GetAsync("/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Swagger_document_and_ui_are_served()
    {
        var doc = await _client.GetAsync("/swagger/v1/swagger.json");
        var ui = await _client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, doc.StatusCode);
        Assert.Contains("/health", await doc.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
    }
}
