using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Domain.Claims;

namespace Claim.Api.Tests;

public class ClaimPhotoThumbnailTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ClaimPhotoThumbnailTests(ApiFactory factory)
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

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 2, 3, 4, 5];

    private static MultipartFormDataContent Upload(byte[] bytes, string fileName = "damage.jpg")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private async Task<long> UploadPhoto(HttpClient staff, long claimId)
    {
        var created = await staff.PostAsync($"/claims/{claimId}/photos", Upload(Jpeg));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    [Fact]
    public async Task An_uploaded_photo_has_a_thumbnail_served_with_the_photos_type_and_nosniff()
    {
        var claim = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var staff = await Staff();
        var photoId = await UploadPhoto(staff, claim.Id);

        var response = await staff.GetAsync($"/claims/{claim.Id}/photos/{photoId}/thumbnail");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(FakeThumbnailGenerator.ThumbnailOf(Jpeg.Length), await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task The_thumbnail_follows_the_full_photos_visibility_adjuster_404_on_draft_admin_read_only_401_without_sign_in()
    {
        var draft = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var photoId = await UploadPhoto(await Staff(), draft.Id);
        var url = $"/claims/{draft.Id}/photos/{photoId}/thumbnail";

        Assert.Equal(HttpStatusCode.NotFound, (await (await As("adjuster@claim.test")).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await As("admin@claim.test")).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync(url)).StatusCode);

        // Once submitted the Adjuster may see the Claim, and so its thumbnails.
        var submit = await (await Staff()).PostAsync($"/claims/{draft.Id}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await As("adjuster@claim.test")).GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task A_thumbnail_of_a_photo_of_another_claim_or_that_does_not_exist_is_404()
    {
        var one = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var other = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var staff = await Staff();
        var photoId = await UploadPhoto(staff, one.Id);

        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/claims/{other.Id}/photos/{photoId}/thumbnail")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/claims/{one.Id}/photos/9999/thumbnail")).StatusCode);
    }
}
