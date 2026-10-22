using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Claim.Application.Claims;
using Claim.Domain.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    [Theory]
    [InlineData(ClaimStatus.Approved)]
    [InlineData(ClaimStatus.Rejected)]
    [InlineData(ClaimStatus.InRepair)]
    [InlineData(ClaimStatus.Repaired)]
    [InlineData(ClaimStatus.InvoiceSubmitted)]
    [InlineData(ClaimStatus.Paid)]
    [InlineData(ClaimStatus.Cancelled)]
    public async Task Deleting_is_locked_from_approval_on_with_409_and_the_photo_and_file_stay(ClaimStatus status)
    {
        var (claim, photo) = await SeedClaimWithPhoto(status);
        var historyBefore = _factory.History.All.Count(e => e.ClaimId == claim.Id);

        var response = await (await Staff()).SendAsync(Delete(claim.Id, photo.Id, VersionOf(claim)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(_factory.Photos.All, p => p.Id == photo.Id);
        Assert.Contains(photo.StoredName, _factory.Files.Names);
        Assert.Equal(historyBefore, _factory.History.All.Count(e => e.ClaimId == claim.Id));
    }

    [Theory]
    [InlineData(ClaimStatus.Draft)]
    [InlineData(ClaimStatus.Submitted)]
    [InlineData(ClaimStatus.UnderReview)]
    [InlineData(ClaimStatus.ChangesRequested)]
    public async Task Deleting_is_allowed_while_the_claim_is_still_open_for_changes(ClaimStatus status)
    {
        var (claim, photo) = await SeedClaimWithPhoto(status);

        var response = await (await Staff()).SendAsync(Delete(claim.Id, photo.Id, VersionOf(claim)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(_factory.Photos.All, p => p.Id == photo.Id);
    }

    [Theory]
    [InlineData("adjuster@claim.test")]
    [InlineData("admin@claim.test")]
    public async Task Adjuster_and_admin_cannot_delete_with_403_but_still_see_the_photo(string email)
    {
        var (claim, photo) = await SeedClaimWithPhoto(ClaimStatus.Submitted);
        var client = await As(email);

        var response = await client.SendAsync(Delete(claim.Id, photo.Id, VersionOf(claim)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(photo.StoredName, _factory.Files.Names);
        Assert.Single((await client.GetFromJsonAsync<JsonElement>($"/claims/{claim.Id}/photos")).EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/claims/{claim.Id}/photos/{photo.Id}/file")).StatusCode);
    }

    [Fact]
    public async Task Deleting_without_signing_in_is_401()
    {
        var (claim, photo) = await SeedClaimWithPhoto(ClaimStatus.Submitted);

        var response = await _factory.CreateClient().SendAsync(Delete(claim.Id, photo.Id, VersionOf(claim)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(_factory.Photos.All, p => p.Id == photo.Id);
    }

    [Fact]
    public async Task Deleting_without_if_match_is_428_and_with_a_stale_version_is_409_version_conflict_and_nothing_changes()
    {
        var (claim, photo) = await SeedClaimWithPhoto(ClaimStatus.Draft);
        var staff = await Staff();
        var historyBefore = _factory.History.All.Count(e => e.ClaimId == claim.Id);

        var missing = await staff.SendAsync(Delete(claim.Id, photo.Id, null));
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);

        var stale = await staff.SendAsync(Delete(claim.Id, photo.Id, "\"AAAAAAAAAAA=\""));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("urn:claim:version-conflict", (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());

        Assert.Contains(_factory.Photos.All, p => p.Id == photo.Id);
        Assert.Contains(photo.StoredName, _factory.Files.Names);
        Assert.Equal(historyBefore, _factory.History.All.Count(e => e.ClaimId == claim.Id));
    }

    [Fact]
    public async Task A_photo_of_another_claim_an_unknown_photo_or_an_unknown_claim_is_404_and_nothing_is_deleted()
    {
        var (claim, photo) = await SeedClaimWithPhoto(ClaimStatus.Draft);
        var other = _factory.Claims.Seed(ClaimStatus.Draft, 1, 1);
        var staff = await Staff();

        Assert.Equal(HttpStatusCode.NotFound, (await staff.SendAsync(Delete(other.Id, photo.Id, VersionOf(other)))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.SendAsync(Delete(claim.Id, 99999, VersionOf(claim)))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.SendAsync(Delete(99999, photo.Id, VersionOf(claim)))).StatusCode);
        Assert.Contains(_factory.Photos.All, p => p.Id == photo.Id);
        Assert.Contains(photo.StoredName, _factory.Files.Names);
    }

    [Fact]
    public async Task Deleting_writes_claim_history_without_changing_status_sends_no_email_and_refreshes_the_version()
    {
        var (claim, photo) = await SeedClaimWithPhoto(ClaimStatus.UnderReview);
        var outboxBefore = _factory.Outbox.Messages.Count;
        var staff = await Staff();

        var response = await staff.SendAsync(Delete(claim.Id, photo.Id, VersionOf(claim)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entry = Assert.Single(_factory.History.All, e => e.ClaimId == claim.Id);
        Assert.Equal(ClaimStatus.UnderReview, entry.FromStatus);
        Assert.Equal(ClaimStatus.UnderReview, entry.ToStatus);
        Assert.Equal(_factory.Users.Users.Single(u => u.Email == "staff@claim.test").Id, entry.ActorUserId);
        Assert.Contains("damage.jpg", entry.Reason);
        Assert.Equal(outboxBefore, _factory.Outbox.Messages.Count);
        var current = await staff.GetFromJsonAsync<JsonElement>($"/claims/{claim.Id}");
        Assert.Equal("UnderReview", current.GetProperty("status").GetString());
        Assert.Equal(current.GetProperty("version").GetString(), (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetString());
    }

    [Fact]
    public async Task When_the_database_fails_the_photo_row_and_its_file_both_stay()
    {
        var (claim, photo) = await SeedClaimWithPhoto(ClaimStatus.Draft);
        var staff = await Staff();
        _factory.Photos.FailNextRemove = true;

        var response = await staff.SendAsync(Delete(claim.Id, photo.Id, VersionOf(claim)));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(_factory.Photos.All, p => p.Id == photo.Id);
        Assert.Contains(photo.StoredName, _factory.Files.Names);
        Assert.DoesNotContain(_factory.History.All, e => e.ClaimId == claim.Id);
    }

    [Fact]
    public async Task When_removing_the_file_fails_after_the_row_is_gone_the_request_still_succeeds_and_the_failure_is_logged()
    {
        var (claim, photo) = await SeedClaimWithPhoto(ClaimStatus.Draft);
        var logs = new CapturingLog();
        using var factory = _factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<ILogger<Claim.Application.Claims.ClaimPhotoRemovalService>>(new CapturingLogger<Claim.Application.Claims.ClaimPhotoRemovalService>(logs))));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = (await Staff()).DefaultRequestHeaders.Authorization;
        _factory.Files.FailNextDelete = true;

        var response = await client.SendAsync(Delete(claim.Id, photo.Id, VersionOf(claim)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(_factory.Photos.All, p => p.Id == photo.Id);
        Assert.Contains(logs.Entries, e => e.Level >= LogLevel.Warning && e.Message.Contains(photo.StoredName) && e.Exception is IOException);
    }

    private sealed class CapturingLog
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
    }

    /// <summary>Serilog replaces the host's logger factory, so the log is captured by swapping the service's own logger.</summary>
    private sealed class CapturingLogger<T>(CapturingLog log) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (log.Entries) log.Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }

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
