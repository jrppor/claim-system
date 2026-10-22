using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Application.Claims;
using Claim.Domain.Claims;

namespace Claim.Api.Tests;

/// <summary>Deleting a Claim Photo (ticket 03): Garage Staff only, before approval, with If-Match, file and row gone together.</summary>
public class ClaimPhotoDeleteTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ClaimPhotoDeleteTests(ApiFactory factory) => _factory = factory;

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

    /// <summary>Seeds a Claim in <paramref name="status"/> that already holds one photo (as if uploaded while it was still open).</summary>
    private async Task<(Domain.Claims.Claim Claim, ClaimPhoto Photo)> SeedClaimWithPhoto(ClaimStatus status)
    {
        var claim = _factory.Claims.Seed(status, 1, 1);
        var storedName = Guid.NewGuid().ToString("N") + ".jpg";
        await _factory.Files.SaveAsync(storedName, new MemoryStream(Jpeg), default);
        var photo = _factory.Photos.Append(new ClaimPhoto(claim.Id, "damage.jpg", storedName, "image/jpeg", Jpeg.Length, null, 1, DateTime.UtcNow));
        return (claim, photo);
    }

    private static HttpRequestMessage Delete(long claimId, long photoId, string? version)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/claims/{claimId}/photos/{photoId}");
        if (version is not null) request.Headers.TryAddWithoutValidation("If-Match", version);
        return request;
    }

    private static string VersionOf(Domain.Claims.Claim claim) => ClaimVersion.Format(claim.RowVersion);

    [Fact]
    public async Task Garage_staff_deletes_a_photo_and_it_leaves_the_list_and_the_stored_file()
    {
        var (claim, photo) = await SeedClaimWithPhoto(ClaimStatus.Submitted);
        var staff = await Staff();

        var response = await staff.SendAsync(Delete(claim.Id, photo.Id, VersionOf(claim)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await staff.GetFromJsonAsync<JsonElement>($"/claims/{claim.Id}/photos")).EnumerateArray());
        Assert.DoesNotContain(photo.StoredName, _factory.Files.Names);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/claims/{claim.Id}/photos/{photo.Id}/file")).StatusCode);
    }
}
