using System.Reflection;

namespace MyContent.Configuration;

internal static class AppVersion
{
    public static string Current =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.1.0";

    public const string Repository = "hamzawatfa16-cpu/MC-";
    public const string ReleaseAssetName = "MyContent-Setup.exe";
}
