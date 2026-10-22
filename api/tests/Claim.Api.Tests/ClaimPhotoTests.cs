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

    private static MultipartFormDataContent UploadMany(params (byte[] Bytes, string Name, string? Caption)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (bytes, name, caption) in files)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(file, "file", name);
            form.Add(new StringContent(caption ?? ""), "caption");
        }
        return form;
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

    [Theory]
    [InlineData("adjuster@claim.test")]
    [InlineData("admin@claim.test")]
    public async Task Only_garage_staff_may_upload_so_other_roles_get_403_and_nothing_is_stored(string email)
    {
        var claim = SeedClaim(ClaimStatus.Submitted);

        var response = await (await As(email)).PostAsync($"/claims/{claim.Id}/photos", Upload(Jpeg));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_factory.Photos.All.Where(p => p.ClaimId == claim.Id));
    }

    [Fact]
    public async Task Uploading_without_signing_in_is_401()
    {
        var claim = SeedClaim(ClaimStatus.Draft);

        var response = await _factory.CreateClient().PostAsync($"/claims/{claim.Id}/photos", Upload(Jpeg));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Uploading_without_if_match_is_428_and_with_a_stale_version_is_409_version_conflict()
    {
        var claim = SeedClaim(ClaimStatus.Draft);
        var plain = _factory.CreateClient();
        plain.DefaultRequestHeaders.Authorization = (await Staff()).DefaultRequestHeaders.Authorization;

        var missing = await plain.PostAsync($"/claims/{claim.Id}/photos", Upload(Jpeg));
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/claims/{claim.Id}/photos") { Content = Upload(Jpeg) };
        request.Headers.TryAddWithoutValidation("If-Match", "\"AAAAAAAAAAA=\"");
        var stale = await plain.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("urn:claim:version-conflict", (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
        Assert.Empty(_factory.Photos.All.Where(p => p.ClaimId == claim.Id));
    }

    [Fact]
    public async Task An_adjuster_gets_404_on_the_photos_of_a_draft_but_sees_them_once_submitted()
    {
        var claim = SeedClaim(ClaimStatus.Draft);
        var staff = await Staff();
        var created = await staff.PostAsync($"/claims/{claim.Id}/photos", Upload(Jpeg));
        var photoId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        var adjuster = await As("adjuster@claim.test");

        Assert.Equal(HttpStatusCode.NotFound, (await adjuster.GetAsync($"/claims/{claim.Id}/photos")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await adjuster.GetAsync($"/claims/{claim.Id}/photos/{photoId}/file")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync($"/claims/{claim.Id}/submit", new { })).StatusCode);

        Assert.Single((await adjuster.GetFromJsonAsync<JsonElement>($"/claims/{claim.Id}/photos")).EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await adjuster.GetAsync($"/claims/{claim.Id}/photos/{photoId}/file")).StatusCode);
    }

    [Fact]
    public async Task Admin_can_view_the_list_and_the_file_but_not_without_signing_in()
    {
        var claim = SeedClaim(ClaimStatus.Draft);
        var created = await (await Staff()).PostAsync($"/claims/{claim.Id}/photos", Upload(Jpeg));
        var photoId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        var admin = await As("admin@claim.test");

        Assert.Single((await admin.GetFromJsonAsync<JsonElement>($"/claims/{claim.Id}/photos")).EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/claims/{claim.Id}/photos/{photoId}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync($"/claims/{claim.Id}/photos/{photoId}/file")).StatusCode);
    }

    [Fact]
    public async Task A_photo_of_another_claim_or_that_does_not_exist_is_404()
    {
        var mine = SeedClaim(ClaimStatus.Draft);
        var other = SeedClaim(ClaimStatus.Draft);
        var staff = await Staff();
        var created = await staff.PostAsync($"/claims/{mine.Id}/photos", Upload(Jpeg));
        var photoId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/claims/{other.Id}/photos/{photoId}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/claims/{mine.Id}/photos/99999/file")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync("/claims/99999/photos")).StatusCode);
    }

    [Fact]
    public async Task Uploading_writes_claim_history_without_changing_status_and_sends_no_email()
    {
        var claim = SeedClaim(ClaimStatus.UnderReview);
        var outboxBefore = _factory.Outbox.Messages.Count;
        var staff = await Staff();

        var response = await staff.PostAsync($"/claims/{claim.Id}/photos", Upload(Jpeg, "front.jpg"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var entry = Assert.Single(_factory.History.All, e => e.ClaimId == claim.Id);
        Assert.Equal(ClaimStatus.UnderReview, entry.FromStatus);
        Assert.Equal(ClaimStatus.UnderReview, entry.ToStatus);
        Assert.Equal(_factory.Users.Users.Single(u => u.Email == "staff@claim.test").Id, entry.ActorUserId);
        Assert.Contains("front.jpg", entry.Reason);
        Assert.Equal(outboxBefore, _factory.Outbox.Messages.Count);
        var current = await staff.GetFromJsonAsync<JsonElement>($"/claims/{claim.Id}");
        Assert.Equal("UnderReview", current.GetProperty("status").GetString());
        // The upload response carries the Claim's new version so the next change can chain from it.
        Assert.Equal(current.GetProperty("version").GetString(), (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetString());
    }

    [Fact]
    public async Task When_saving_the_row_fails_the_stored_file_is_removed_and_no_row_is_left()
    {
        var claim = SeedClaim(ClaimStatus.Draft);
        var staff = await Staff();
        var filesBefore = _factory.Files.Names.ToHashSet();
        _factory.Photos.FailNextAppend = true;

        var response = await staff.PostAsync($"/claims/{claim.Id}/photos", Upload(Jpeg));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(_factory.Files.Names.Except(filesBefore));
        Assert.Empty(_factory.Photos.All.Where(p => p.ClaimId == claim.Id));
        Assert.Empty(_factory.History.All.Where(e => e.ClaimId == claim.Id));
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

    [Fact]
    public async Task Several_files_in_one_request_are_all_stored_listed_and_recorded_in_history()
    {
        var claim = SeedClaim(ClaimStatus.Draft);
        var staff = await Staff();

        var response = await staff.PostAsync($"/claims/{claim.Id}/photos", UploadMany((Jpeg, "front.jpg", null), (Jpeg, "rear.jpg", null), (Jpeg, "left.jpg", null)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(["front.jpg", "rear.jpg", "left.jpg"], created.EnumerateArray().Select(p => p.GetProperty("originalFileName").GetString()));
        var list = await staff.GetFromJsonAsync<JsonElement>($"/claims/{claim.Id}/photos");
        Assert.Equal(3, list.GetArrayLength());
        Assert.Equal(3, _factory.History.All.Count(e => e.ClaimId == claim.Id));
        var current = await staff.GetFromJsonAsync<JsonElement>($"/claims/{claim.Id}");
        Assert.All(created.EnumerateArray(), p => Assert.Equal(current.GetProperty("version").GetString(), p.GetProperty("version").GetString()));
    }

    [Fact]
    public async Task One_invalid_file_among_several_rejects_the_whole_request_naming_that_file()
    {
        var claim = SeedClaim(ClaimStatus.Draft);
        var filesBefore = _factory.Files.Names.Count;
        var notAnImage = "%PDF-1.7 not a picture"u8.ToArray();

        var response = await (await Staff()).PostAsync($"/claims/{claim.Id}/photos",
            UploadMany((Jpeg, "good1.jpg", null), (notAnImage, "bad.jpg", null), (Jpeg, "good2.jpg", null)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var reasons = problem.GetProperty("errors").GetProperty("file").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(reasons, r => r!.Contains("bad.jpg"));
        Assert.DoesNotContain(_factory.Photos.All, p => p.ClaimId == claim.Id);
        Assert.Equal(filesBefore, _factory.Files.Names.Count);
        Assert.DoesNotContain(_factory.History.All, e => e.ClaimId == claim.Id);
    }

    private async Task<HttpClient> StaffWithPhotos(Domain.Claims.Claim claim, int count)
    {
        var staff = await Staff();
        var seeded = await staff.PostAsync($"/claims/{claim.Id}/photos", UploadMany(Enumerable.Range(1, count).Select(i => (Jpeg, $"seed{i}.jpg", (string?)null)).ToArray()));
        Assert.Equal(HttpStatusCode.Created, seeded.StatusCode);
        return staff;
    }

    [Fact]
    public async Task A_claim_with_20_photos_refuses_another_with_409_and_stores_nothing()
    {
        var claim = SeedClaim(ClaimStatus.Draft);
        var staff = await StaffWithPhotos(claim, 20);
        var filesBefore = _factory.Files.Names.Count;

        var response = await staff.PostAsync($"/claims/{claim.Id}/photos", Upload(Jpeg, "21st.jpg"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(20, _factory.Photos.All.Count(p => p.ClaimId == claim.Id));
        Assert.Equal(filesBefore, _factory.Files.Names.Count);
    }

    [Fact]
    public async Task A_request_that_would_go_over_20_is_refused_as_a_whole_but_one_that_reaches_exactly_20_is_accepted()
    {
        var claim = SeedClaim(ClaimStatus.Draft);
        var staff = await StaffWithPhotos(claim, 18);
        var filesBefore = _factory.Files.Names.Count;

        var over = await staff.PostAsync($"/claims/{claim.Id}/photos", UploadMany((Jpeg, "a.jpg", null), (Jpeg, "b.jpg", null), (Jpeg, "c.jpg", null)));
        Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
        Assert.Equal(18, _factory.Photos.All.Count(p => p.ClaimId == claim.Id));
        Assert.Equal(filesBefore, _factory.Files.Names.Count);

        var exact = await staff.PostAsync($"/claims/{claim.Id}/photos", UploadMany((Jpeg, "a.jpg", null), (Jpeg, "b.jpg", null)));
        Assert.Equal(HttpStatusCode.Created, exact.StatusCode);
        Assert.Equal(20, _factory.Photos.All.Count(p => p.ClaimId == claim.Id));
    }

    [Fact]
    public async Task An_optional_caption_is_saved_trimmed_and_shown_in_the_list_and_a_blank_one_means_none()
    {
        var claim = SeedClaim(ClaimStatus.Draft);
        var staff = await Staff();

        var response = await staff.PostAsync($"/claims/{claim.Id}/photos",
            UploadMany((Jpeg, "front.jpg", "  กันชนหน้าบุบ  "), (Jpeg, "rear.jpg", "   "), (Jpeg, "left.jpg", null)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var list = (await staff.GetFromJsonAsync<JsonElement>($"/claims/{claim.Id}/photos")).EnumerateArray().ToList();
        Assert.Equal("กันชนหน้าบุบ", list[0].GetProperty("caption").GetString());
        Assert.Equal(JsonValueKind.Null, list[1].GetProperty("caption").ValueKind);
        Assert.Equal(JsonValueKind.Null, list[2].GetProperty("caption").ValueKind);
    }

    [Fact]
    public async Task A_caption_over_200_characters_rejects_the_whole_request_naming_the_file_but_exactly_200_is_accepted()
    {
        var claim = SeedClaim(ClaimStatus.Draft);
        var staff = await Staff();
        var filesBefore = _factory.Files.Names.Count;

        var tooLong = await staff.PostAsync($"/claims/{claim.Id}/photos",
            UploadMany((Jpeg, "ok.jpg", "short"), (Jpeg, "wordy.jpg", new string('ก', 201))));

        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        var problem = await tooLong.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("wordy.jpg", problem.GetProperty("errors").GetProperty("caption")[0].GetString());
        Assert.DoesNotContain(_factory.Photos.All, p => p.ClaimId == claim.Id);
        Assert.Equal(filesBefore, _factory.Files.Names.Count);

        var exact = await staff.PostAsync($"/claims/{claim.Id}/photos", UploadMany((Jpeg, "wordy.jpg", new string('ก', 200))));
        Assert.Equal(HttpStatusCode.Created, exact.StatusCode);
    }
}
