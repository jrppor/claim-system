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
    public async Task A_file_named_jpg_whose_bytes_are_not_an_image_is_rejected_with_400_and_stores_nothing()
    {
        var claim = SeedClaim(ClaimStatus.Draft);
        var notAnImage = "%PDF-1.7 not a picture"u8.ToArray();

        var response = await (await Staff()).PostAsync($"/claims/{claim.Id}/photos", Upload(notAnImage, "evil.jpg", "image/jpeg"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var reasons = problem.GetProperty("errors").GetProperty("file").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(reasons, r => r!.Contains("evil.jpg") && r.Contains("JPEG"));
        Assert.Empty(_factory.Photos.All.Where(p => p.ClaimId == claim.Id));
        Assert.Empty(_factory.History.All.Where(e => e.ClaimId == claim.Id));
    }

    [Fact]
    public async Task A_file_over_10_MB_is_rejected_with_400_naming_the_file_and_a_file_of_exactly_10_MB_is_accepted()
    {
        var claim = SeedClaim(ClaimStatus.Draft);
        var staff = await Staff();
        byte[] Sized(int size) { var b = new byte[size]; Jpeg.CopyTo(b, 0); return b; }
        var filesBefore = _factory.Files.Names.Count;

        var tooBig = await staff.PostAsync($"/claims/{claim.Id}/photos", Upload(Sized(10 * 1024 * 1024 + 1), "huge.jpg"));
        Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
        var problem = await tooBig.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("huge.jpg", problem.GetProperty("errors").GetProperty("file")[0].GetString());
        Assert.Empty(_factory.Photos.All.Where(p => p.ClaimId == claim.Id));
        Assert.Equal(filesBefore, _factory.Files.Names.Count);

        var exact = await staff.PostAsync($"/claims/{claim.Id}/photos", Upload(Sized(10 * 1024 * 1024), "limit.jpg"));
        Assert.Equal(HttpStatusCode.Created, exact.StatusCode);
    }

    [Fact]
    public async Task The_original_file_name_never_becomes_a_path_and_duplicate_or_odd_names_do_not_collide()
    {
        var claim = SeedClaim(ClaimStatus.Draft);
        var staff = await Staff();
        var filesBefore = _factory.Files.Names.ToHashSet();

        foreach (var name in new[] { "same.jpg", "same.jpg", "..\\..\\windows\\evil.jpg", "../../etc/passwd.jpg", "ภาพ รถ #1 (หน้า).jpg", new string('x', 400) + ".jpg" })
            Assert.Equal(HttpStatusCode.Created, (await staff.PostAsync($"/claims/{claim.Id}/photos", Upload(Jpeg, name))).StatusCode);

        var stored = _factory.Files.Names.Except(filesBefore).ToList();
        Assert.Equal(6, stored.Count);
        Assert.All(stored, n => Assert.Matches(@"^[0-9a-f]{32}\.jpg$", n));

        var shown = (await staff.GetFromJsonAsync<JsonElement>($"/claims/{claim.Id}/photos")).EnumerateArray()
            .Select(p => p.GetProperty("originalFileName").GetString()).ToList();
        Assert.Equal(["same.jpg", "same.jpg", "evil.jpg", "passwd.jpg", "ภาพ รถ #1 (หน้า).jpg"], shown.Take(5));
        Assert.Equal(255, shown[5]!.Length);
    }

    [Theory]
    [InlineData(ClaimStatus.Draft)]
    [InlineData(ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.ChangesRequested)]
    public async Task Photos_can_be_added_while_the_claim_is_still_open_for_changes(ClaimStatus status)
    {
        var claim = SeedClaim(status);

        var response = await (await Staff()).PostAsync($"/claims/{claim.Id}/photos", Upload(Jpeg));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData(ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Rejected)]
    [InlineData(ClaimStatus.InRepair)]
    [InlineData(ClaimStatus.Repaired)]
    [InlineData(ClaimStatus.InvoiceSubmitted)]
    [InlineData(ClaimStatus.Paid)]
    [InlineData(ClaimStatus.Cancelled)]
    public async Task Photos_are_locked_from_approval_on_with_409_and_nothing_is_stored(ClaimStatus status)
    {
        var claim = SeedClaim(status);
        var filesBefore = _factory.Files.Names.Count;

        var response = await (await Staff()).PostAsync($"/claims/{claim.Id}/photos", Upload(Jpeg));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(_factory.Photos.All.Where(p => p.ClaimId == claim.Id));
        Assert.Empty(_factory.History.All.Where(e => e.ClaimId == claim.Id));
        Assert.Equal(filesBefore, _factory.Files.Names.Count);
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
