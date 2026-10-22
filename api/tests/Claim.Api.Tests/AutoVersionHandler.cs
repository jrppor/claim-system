using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Claim.Api.Tests;

/// <summary>
/// Behaves like the UI: before it changes a Claim it reads the Claim and sends the `version` it got as If-Match.
/// A request that already carries If-Match is left alone, so tests about stale or missing versions stay in control.
/// When the Claim cannot be read (missing, or hidden from the role) a placeholder version is sent so the API still answers 404.
/// </summary>
public sealed partial class AutoVersionHandler : DelegatingHandler
{
    private const string PlaceholderVersion = "AAAAAAAAAAA=";

    [GeneratedRegex(@"^/claims/(\d+)(/(submit|review|approve|request-changes|reject|cancel|estimates|start-repair|mark-repaired|invoice|pay|photos))?$")]
    private static partial Regex ClaimMutation();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri?.AbsolutePath ?? "";
        var isMutation = (request.Method == HttpMethod.Post || request.Method == HttpMethod.Put) && ClaimMutation().IsMatch(path);
        if (isMutation && !request.Headers.Contains("If-Match"))
        {
            var id = ClaimMutation().Match(path).Groups[1].Value;
            var read = new HttpRequestMessage(HttpMethod.Get, new Uri(request.RequestUri!, $"/claims/{id}")) { Headers = { Authorization = request.Headers.Authorization } };
            var response = await base.SendAsync(read, ct);
            var version = PlaceholderVersion;
            if (response.IsSuccessStatusCode)
                version = (await response.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("version").GetString()!;
            request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        }
        return await base.SendAsync(request, ct);
    }
}
