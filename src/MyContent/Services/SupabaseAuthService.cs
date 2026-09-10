using Supabase;
using Supabase.Gotrue;
using static Supabase.Gotrue.Constants;

namespace MyContent.Services;

internal sealed class SupabaseAuthService
{
    private readonly Client _client;
    private bool _initialized;

    public SupabaseAuthService(string projectUrl, string publishableKey)
    {
        if (string.IsNullOrWhiteSpace(projectUrl))
        {
            throw new ArgumentException("Supabase project URL is required.", nameof(projectUrl));
        }

        if (string.IsNullOrWhiteSpace(publishableKey))
        {
            throw new ArgumentException("Supabase publishable key is required.", nameof(publishableKey));
        }

        _client = new Client(
            projectUrl,
            publishableKey,
            new SupabaseOptions
            {
                AutoRefreshToken = true
            });
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _client.InitializeAsync().ConfigureAwait(false);
        _initialized = true;
    }

    public string? CurrentUserEmail => _client.Auth.CurrentUser?.Email;

    public async Task SignInWithGoogleAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        using var callback = new GoogleOAuthCallback();
        var authState = await _client.Auth.SignIn(
            Provider.Google,
            new SignInOptions
            {
                FlowType = OAuthFlowType.PKCE,
                RedirectTo = callback.RedirectUri
            }).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(authState.PKCEVerifier))
        {
            throw new InvalidOperationException("Google sign-in could not start securely.");
        }

        Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = authState.Uri.ToString(),
            UseShellExecute = true
        });

        var callbackUri = await callback.WaitForCallbackAsync(cancellationToken).ConfigureAwait(false);
        var parameters = ParseQuery(callbackUri.Query);

        if (parameters.TryGetValue("error_description", out var errorDescription) &&
            !string.IsNullOrWhiteSpace(errorDescription))
        {
            throw new InvalidOperationException("Google sign-in was not completed.");
        }

        if (!parameters.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Google sign-in did not return a valid authorization code.");
        }

        var session = await _client.Auth.ExchangeCodeForSession(
            authState.PKCEVerifier,
            code).ConfigureAwait(false);

        if (session is null || string.IsNullOrWhiteSpace(_client.Auth.CurrentUser?.Email))
        {
            throw new InvalidOperationException("Google sign-in did not create a valid My Content session.");
        }
    }

    public async Task SignOutAsync()
    {
        EnsureInitialized();
        await _client.Auth.SignOut().ConfigureAwait(false);
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Supabase has not been initialized.");
        }
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var rawQuery = query.TrimStart('?');

        if (string.IsNullOrEmpty(rawQuery))
        {
            return values;
        }

        foreach (var pair in rawQuery.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0].Replace('+', ' '));
            var value = parts.Length == 2
                ? Uri.UnescapeDataString(parts[1].Replace('+', ' '))
                : string.Empty;

            values[key] = value;
        }

        return values;
    }
}
