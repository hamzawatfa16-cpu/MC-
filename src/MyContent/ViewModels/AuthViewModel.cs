using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using MyContent.Commands;
using MyContent.Services;

namespace MyContent.ViewModels;

internal sealed class AuthViewModel : INotifyPropertyChanged
{
    private readonly SupabaseAuthService _authService;
    private string _statusMessage = "Connecting...";
    private string? _errorMessage;
    private string? _authenticatedEmail;
    private bool _termsAccepted;
    private bool _isBusy = true;
    private bool _isSupabaseReady;
    private bool _isAuthenticated;

    public AuthViewModel(SupabaseAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        SignInWithGoogleCommand = new AsyncCommand(
            SignInWithGoogleAsync,
            CanSignInWithGoogle);

        SignOutCommand = new AsyncCommand(
            SignOutAsync,
            () => !IsBusy && IsAuthenticated);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AsyncCommand SignInWithGoogleCommand { get; }

    public AsyncCommand SignOutCommand { get; }

    public bool TermsAccepted
    {
        get => _termsAccepted;
        set
        {
            if (SetField(ref _termsAccepted, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanUseAuthForm));
                OnPropertyChanged(nameof(SignInButtonText));
                RaiseCommandStates();
            }
        }
    }

    public bool IsSupabaseReady
    {
        get => _isSupabaseReady;
        private set
        {
            if (SetField(ref _isSupabaseReady, value))
            {
                OnPropertyChanged(nameof(CanUseAuthForm));
                RaiseCommandStates();
            }
        }
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set
        {
            if (SetField(ref _isAuthenticated, value))
            {
                OnPropertyChanged(nameof(CanUseAuthForm));
                OnPropertyChanged(nameof(Greeting));
                RaiseCommandStates();
            }
        }
    }

    public bool CanUseAuthForm => IsSupabaseReady && !IsBusy && !IsAuthenticated;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage) && !HasError;

    public string SignInButtonText => IsBusy ? "Opening Google..." : "Continue with Google";

    public string Greeting
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AuthenticatedEmail))
            {
                return "Welcome back";
            }

            var local = AuthenticatedEmail.Split('@')[0];
            return string.IsNullOrWhiteSpace(local) ? "Welcome back" : "Welcome, " + local;
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetField(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetField(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public string? AuthenticatedEmail
    {
        get => _authenticatedEmail;
        private set
        {
            if (SetField(ref _authenticatedEmail, value))
            {
                OnPropertyChanged(nameof(Greeting));
            }
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Authentication provider failures are shown to the user instead of crashing the desktop app.")]
    public async Task InitializeAsync()
    {
        try
        {
            await _authService.InitializeAsync().ConfigureAwait(true);
            IsSupabaseReady = true;

            var email = _authService.CurrentUserEmail;
            if (!string.IsNullOrWhiteSpace(email))
            {
                SetAuthenticated(email);
            }
            else
            {
                StatusMessage = string.Empty;
            }
        }
        catch (Exception)
        {
            IsSupabaseReady = false;
            StatusMessage = string.Empty;
            ErrorMessage = "My Content could not connect. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Authentication provider failures are shown to the user instead of crashing the desktop app.")]
    private async Task SignInWithGoogleAsync()
    {
        if (!CanSignInWithGoogle())
        {
            return;
        }

        ErrorMessage = null;
        IsBusy = true;
        StatusMessage = "Finish sign-in in your browser, then come back here.";

        try
        {
            await _authService.SignInWithGoogleAsync().ConfigureAwait(true);

            var email = _authService.CurrentUserEmail;
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new InvalidOperationException("No authenticated user was returned.");
            }

            SetAuthenticated(email);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Google sign-in was cancelled.";
        }
        catch (Exception)
        {
            StatusMessage = string.Empty;
            ErrorMessage = "Google sign-in did not finish. Try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Authentication provider failures are shown to the user instead of crashing the desktop app.")]
    private async Task SignOutAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            await _authService.SignOutAsync().ConfigureAwait(true);
            IsAuthenticated = false;
            AuthenticatedEmail = null;
            TermsAccepted = false;
            StatusMessage = "You have been signed out.";
        }
        catch (Exception)
        {
            StatusMessage = string.Empty;
            ErrorMessage = "Please try signing out again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSignInWithGoogle() =>
        CanUseAuthForm && TermsAccepted;

    private void SetAuthenticated(string email)
    {
        IsAuthenticated = true;
        AuthenticatedEmail = email;
        StatusMessage = string.Empty;
        ErrorMessage = null;
    }

    private void RaiseCommandStates()
    {
        SignInWithGoogleCommand.RaiseCanExecuteChanged();
        SignOutCommand.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
