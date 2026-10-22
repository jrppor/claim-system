using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Domain.Claims;

namespace Claim.Api.Tests;

public class ClaimPhotoTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ClaimPhotoTests(ApiFactory factory)
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

    private Domain.Claims.Claim SeedClaim(ClaimStatus status) => _factory.Claims.Seed(status, 1, 1);

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 2, 3, 4, 5];

    private static MultipartFormDataContent Upload(byte[] bytes, string fileName = "damage.jpg", string contentType = "image/jpeg")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    [Fact]
    public async Task Garage_staff_can_upload_a_photo_then_see_it_in_the_list_and_fetch_the_same_bytes()
    {
        var claim = SeedClaim(ClaimStatus.Draft);
        var staff = await Staff();

        var created = await staff.PostAsync($"/claims/{claim.Id}/photos", Upload(Jpeg));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var photo = await created.Content.ReadFromJsonAsync<JsonElement>();
        var photoId = photo.GetProperty("id").GetInt64();

        var list = await (await Staff()).GetFromJsonAsync<JsonElement>($"/claims/{claim.Id}/photos");
        var item = Assert.Single(list.EnumerateArray());
        Assert.Equal(photoId, item.GetProperty("id").GetInt64());
        Assert.Equal("damage.jpg", item.GetProperty("originalFileName").GetString());
        Assert.Equal("image/jpeg", item.GetProperty("contentType").GetString());

        var fetched = await staff.GetAsync($"/claims/{claim.Id}/photos/{photoId}/file");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal("image/jpeg", fetched.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", fetched.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(Jpeg, await fetched.Content.ReadAsByteArrayAsync());
    }
}
