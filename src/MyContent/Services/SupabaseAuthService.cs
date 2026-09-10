using Supabase;

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

        await _client.InitializeAsync().ConfigureAwait(true);
        _initialized = true;
    }

    public string? CurrentUserEmail => _client.Auth.CurrentUser?.Email;

    public async Task SignInAsync(string email, string password)
    {
        EnsureInitialized();
        await _client.Auth.SignIn(email, password).ConfigureAwait(true);
    }

    public async Task SignUpAsync(string email, string password)
    {
        EnsureInitialized();
        await _client.Auth.SignUp(email, password).ConfigureAwait(true);
    }

    public async Task SignOutAsync()
    {
        EnsureInitialized();
        await _client.Auth.SignOut().ConfigureAwait(true);
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Supabase has not been initialized.");
        }
    }
}
