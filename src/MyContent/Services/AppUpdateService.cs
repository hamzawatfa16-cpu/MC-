using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyContent.Configuration;

namespace MyContent.Services;

internal static class AppUpdateService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.github.com/repos/{AppVersion.Repository}/releases/latest");
        request.Headers.UserAgent.ParseAdd("MyContent-Updater/1.0");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        try
        {
            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return UpdateCheckResult.NoRelease();
            }

            response.EnsureSuccessStatusCode();
            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken).ConfigureAwait(false);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return UpdateCheckResult.NoRelease();
            }

            var remoteVersion = ParseVersion(release.TagName);
            if (remoteVersion is null)
            {
                return UpdateCheckResult.Failure("The latest GitHub release has an invalid version tag.");
            }

            var current = Version.Parse(AppVersion.Current);
            if (remoteVersion <= current)
            {
                return UpdateCheckResult.UpToDate(AppVersion.Current, release.TagName);
            }

            var asset = (release.Assets ?? []).FirstOrDefault(x =>
                string.Equals(x.Name, AppVersion.ReleaseAssetName, StringComparison.OrdinalIgnoreCase));

            if (asset is null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            {
                return UpdateCheckResult.Failure(
                    $"Version {release.TagName} is available, but its Windows installer is missing ({AppVersion.ReleaseAssetName}).");
            }

            return UpdateCheckResult.Available(
                AppVersion.Current,
                release.TagName,
                release.Name ?? release.TagName,
                release.Body ?? string.Empty,
                asset.BrowserDownloadUrl);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            return UpdateCheckResult.Failure(exception.Message);
        }
        catch (JsonException exception)
        {
            return UpdateCheckResult.Failure(exception.Message);
        }
        catch (FormatException exception)
        {
            return UpdateCheckResult.Failure(exception.Message);
        }
    }

    public static async Task ApplyAsync(UpdateCheckResult update, Action<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!update.IsAvailable || string.IsNullOrWhiteSpace(update.DownloadUrl))
        {
            throw new InvalidOperationException("There is no valid update to install.");
        }

        var installerPath = Path.Combine(
            Path.GetTempPath(),
            $"MyContent-Setup-{update.TargetVersion}-{Guid.NewGuid():N}.exe");

        progress?.Invoke("Downloading update…");

        var downloadUri = new Uri(update.DownloadUrl, UriKind.Absolute);
        using (var source = await Http.GetStreamAsync(downloadUri, cancellationToken).ConfigureAwait(false))
        using (var destination = File.Create(installerPath))
        {
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        var appDirectory = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        progress?.Invoke("Installing update…");

        var startInfo = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = $"/VERYSILENT /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS /DIR=\"{appDirectory}\"",
            UseShellExecute = true,
            WorkingDirectory = Path.GetTempPath(),
        };

        if (Process.Start(startInfo) is null)
        {
            throw new InvalidOperationException("Windows could not start the downloaded installer.");
        }

        Environment.Exit(0);
    }

    private static Version? ParseVersion(string tag)
    {
        var normalized = tag.Trim();
        while (normalized.Length > 0 && (normalized[0] == 'v' || normalized[0] == 'V'))
        {
            normalized = normalized[1..];
        }

        return Version.TryParse(normalized, out var version) ? version : null;
    }

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "System.Text.Json materializes this record during release-response deserialization.")]
    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("assets")] List<GitHubAsset>? Assets);

    [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "System.Text.Json materializes this record during release-response deserialization.")]
    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}

internal sealed record UpdateCheckResult(
    string CurrentVersion,
    string TargetVersion,
    string ReleaseName,
    string ReleaseNotes,
    string? DownloadUrl,
    bool IsAvailable,
    bool HasRelease,
    string? Error)
{
    public bool IsUpToDate => HasRelease && !IsAvailable && Error is null;

    public static UpdateCheckResult NoRelease() =>
        new(AppVersion.Current, AppVersion.Current, "", "", null, false, false, null);

    public static UpdateCheckResult UpToDate(string current, string target) =>
        new(current, target, target, "", null, false, true, null);

    public static UpdateCheckResult Available(string current, string target, string name, string notes, string url) =>
        new(current, target, name, notes, url, true, true, null);

    public static UpdateCheckResult Failure(string error) =>
        new(AppVersion.Current, AppVersion.Current, "", "", null, false, false, error);
}
