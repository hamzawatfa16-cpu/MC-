using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MyContent.Configuration;

namespace MyContent.Services;

internal sealed class AppUpdateService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.github.com/repos/{AppVersion.Repository}/releases/latest");
        request.Headers.UserAgent.ParseAdd("MyContent-Updater/1.0");

        try
        {
            using var response = await Http.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return UpdateCheckResult.NoRelease();
            }

            response.EnsureSuccessStatusCode();
            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return UpdateCheckResult.NoRelease();
            }

            var remoteVersion = ParseVersion(release.TagName);
            if (remoteVersion is null)
            {
                return UpdateCheckResult.Error("The latest GitHub release has an invalid version tag.");
            }

            var current = Version.Parse(AppVersion.Current);
            if (remoteVersion <= current)
            {
                return UpdateCheckResult.UpToDate(AppVersion.Current, release.TagName);
            }

            var asset = release.Assets.FirstOrDefault(x =>
                string.Equals(x.Name, AppVersion.ReleaseAssetName, StringComparison.OrdinalIgnoreCase));

            if (asset is null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            {
                return UpdateCheckResult.Error(
                    $"Version {release.TagName} is available, but the Windows update package is missing ({AppVersion.ReleaseAssetName}).");
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
        catch (Exception exception)
        {
            return UpdateCheckResult.Error(exception.Message);
        }
    }

    public async Task ApplyAsync(UpdateCheckResult update, Action<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!update.IsAvailable || string.IsNullOrWhiteSpace(update.DownloadUrl))
        {
            throw new InvalidOperationException("There is no valid update to install.");
        }

        var downloadPath = Path.Combine(Path.GetTempPath(), $"MyContent-{update.TargetVersion}-{Guid.NewGuid():N}.zip");
        var extractPath = Path.Combine(Path.GetTempPath(), $"MyContent-update-{Guid.NewGuid():N}");

        progress?.Invoke("Downloading update...");
        await using (var source = await Http.GetStreamAsync(update.DownloadUrl, cancellationToken))
        await using (var destination = File.Create(downloadPath))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        Directory.CreateDirectory(extractPath);
        ZipFile.ExtractToDirectory(downloadPath, extractPath, overwriteFiles: true);

        var appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var executableName = Path.GetFileName(Environment.ProcessPath ?? "MyContent.exe");
        var scriptPath = Path.Combine(Path.GetTempPath(), $"MyContent-updater-{Guid.NewGuid():N}.ps1");

        progress?.Invoke("Installing update...");

        var script = $$"""
param(
    [int]$ProcessId,
    [string]$Source,
    [string]$Target,
    [string]$Executable,
    [string]$ZipFile,
    [string]$ExtractDir,
    [string]$ScriptFile
)

while (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
    Start-Sleep -Milliseconds 300
}

Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $Target -Recurse -Force
}

Remove-Item -LiteralPath $ZipFile -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $ExtractDir -Recurse -Force -ErrorAction SilentlyContinue
Start-Process -FilePath (Join-Path $Target $Executable)
Start-Sleep -Milliseconds 500
Remove-Item -LiteralPath $ScriptFile -Force -ErrorAction SilentlyContinue
""";

        await File.WriteAllTextAsync(scriptPath, script, cancellationToken);

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -ProcessId {Environment.ProcessId} -Source \"{extractPath}\" -Target \"{appDirectory}\" -Executable \"{executableName}\" -ZipFile \"{downloadPath}\" -ExtractDir \"{extractPath}\" -ScriptFile \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        Process.Start(startInfo);
        Environment.Exit(0);
    }

    private static Version? ParseVersion(string tag)
    {
        var normalized = tag.Trim();
        while (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        return Version.TryParse(normalized, out var version) ? version : null;
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("assets")] List<GitHubAsset> Assets);

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

    public static UpdateCheckResult Error(string error) =>
        new(AppVersion.Current, AppVersion.Current, "", "", null, false, false, error);
}
