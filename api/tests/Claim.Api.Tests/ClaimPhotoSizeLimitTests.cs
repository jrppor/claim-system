using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Domain.Claims;

namespace Claim.Api.Tests;

/// <summary>Requests bigger than any legitimate upload are answered 413 as a ProblemDetails, before the body is read.</summary>
public class ClaimPhotoSizeLimitTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ClaimPhotoSizeLimitTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> Staff()
    {
        var login = await _factory.CreateClient().PostAsJsonAsync("/auth/login", new { email = "staff@claim.test", password = ApiFactory.Password });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var client = _factory.CreateVersionedClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return client;
    }

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 2, 3, 4, 5];

    /// <summary>A body that announces <paramref name="length"/> bytes without sending them (a real 200 MB body is not needed to test the limit).</summary>
    private sealed class DeclaredLengthContent : HttpContent
    {
        private readonly long _length;

        public DeclaredLengthContent(long length)
        {
            _length = length;
            Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=x");
        }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = _length;
            return true;
        }
    }

    private const long MostALegitimateRequestCarries = 20L * 10 * 1024 * 1024 + 1024 * 1024;

    [Fact]
    public async Task A_request_declaring_more_than_20_full_files_plus_1_MB_is_413_problem_details_and_stores_nothing()
    {
        var claim = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var filesBefore = _factory.Files.Names.Count;

        var response = await (await Staff()).PostAsync($"/claims/{claim.Id}/photos", new DeclaredLengthContent(MostALegitimateRequestCarries + 1));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(413, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.Equal(filesBefore, _factory.Files.Names.Count);
    }

    [Fact]
    public async Task A_request_declaring_exactly_the_most_a_legitimate_upload_carries_is_not_refused_for_its_size()
    {
        var claim = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);

        var response = await (await Staff()).PostAsync($"/claims/{claim.Id}/photos", new DeclaredLengthContent(MostALegitimateRequestCarries));

        Assert.NotEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Twenty_one_files_in_one_request_are_refused_with_409_and_nothing_is_stored()
    {
        var claim = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var filesBefore = _factory.Files.Names.Count;
        var form = new MultipartFormDataContent();
        for (var i = 0; i < 21; i++)
        {
            var file = new ByteArrayContent(Jpeg);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(file, "file", $"{i}.jpg");
        }

        var response = await (await Staff()).PostAsync($"/claims/{claim.Id}/photos", form);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(filesBefore, _factory.Files.Names.Count);
        Assert.DoesNotContain(_factory.Photos.All, p => p.ClaimId == claim.Id);
    }
}
