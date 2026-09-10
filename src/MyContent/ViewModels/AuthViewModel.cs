using System.ComponentModel;
using System.Runtime.CompilerServices;
using MyContent.Commands;
using MyContent.Services;

namespace MyContent.ViewModels;

internal sealed class AuthViewModel : INotifyPropertyChanged
{
    private readonly SupabaseAuthService _authService;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _statusMessage = "Connecting to Supabase...";
    private string? _errorMessage;
    private string? _authenticatedEmail;
    private bool _isSignUpMode;
    private bool _termsAccepted;
    private bool _isBusy = true;
    private bool _isSupabaseReady;
    private bool _isAuthenticated;

    public AuthViewModel(SupabaseAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        SelectSignInCommand = new AsyncCommand(
            () => SetModeAsync(false),
            () => !IsBusy && !IsAuthenticated);

        SelectSignUpCommand = new AsyncCommand(
            () => SetModeAsync(true),
            () => !IsBusy && !IsAuthenticated);

        SubmitCommand = new AsyncCommand(
            SubmitAsync,
            CanSubmit);

        SignOutCommand = new AsyncCommand(
            SignOutAsync,
            () => !IsBusy && IsAuthenticated);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AsyncCommand SelectSignInCommand { get; }

    public AsyncCommand SelectSignUpCommand { get; }

    public AsyncCommand SubmitCommand { get; }

    public AsyncCommand SignOutCommand { get; }

    public string Email
    {
        get => _email;
        set
        {
            if (SetField(ref _email, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetField(ref _password, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetField(ref _confirmPassword, value);
    }

    public bool IsSignUpMode
    {
        get => _isSignUpMode;
        private set
        {
            if (SetField(ref _isSignUpMode, value))
            {
                OnPropertyChanged(nameof(SubmitButtonText));
                RaiseCommandStates();
            }
        }
    }

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
                RaiseCommandStates();
            }
        }
    }

    public bool CanUseAuthForm => IsSupabaseReady && !IsBusy && !IsAuthenticated;

    public string SubmitButtonText => IsSignUpMode ? "Create account" : "Sign in";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public string? AuthenticatedEmail
    {
        get => _authenticatedEmail;
        private set => SetField(ref _authenticatedEmail, value);
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _authService.InitializeAsync();
            IsSupabaseReady = true;

            var email = _authService.CurrentUserEmail;
            if (!string.IsNullOrWhiteSpace(email))
            {
                SetAuthenticated(email);
            }
            else
            {
                StatusMessage = "Ready";
            }
        }
        catch (Exception exception)
        {
            IsSupabaseReady = false;
            StatusMessage = "Supabase connection failed.";
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task SetModeAsync(bool signUp)
    {
        IsSignUpMode = signUp;
        ConfirmPassword = string.Empty;
        ErrorMessage = null;
        StatusMessage = signUp ? "Create your My Content account." : "Sign in to My Content.";
        return Task.CompletedTask;
    }

    private async Task SubmitAsync()
    {
        ErrorMessage = null;

        if (!ValidateInput())
        {
            return;
        }

        IsBusy = true;

        try
        {
            var email = Email.Trim();

            if (IsSignUpMode)
            {
                await _authService.SignUpAsync(email, Password);

                var authenticatedEmail = _authService.CurrentUserEmail;
                if (!string.IsNullOrWhiteSpace(authenticatedEmail))
                {
                    SetAuthenticated(authenticatedEmail);
                }
                else
                {
                    Password = string.Empty;
                    ConfirmPassword = string.Empty;
                    IsSignUpMode = false;
                    StatusMessage = "Account created. Check your email to confirm your address, then sign in.";
                }
            }
            else
            {
                await _authService.SignInAsync(email, Password);

                var authenticatedEmail = _authService.CurrentUserEmail;
                if (string.IsNullOrWhiteSpace(authenticatedEmail))
                {
                    throw new InvalidOperationException("Supabase did not return a signed-in user.");
                }

                SetAuthenticated(authenticatedEmail);
            }
        }
        catch (Exception exception)
        {
            StatusMessage = "Authentication failed.";
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SignOutAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            await _authService.SignOutAsync();
            IsAuthenticated = false;
            AuthenticatedEmail = null;
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            TermsAccepted = false;
            StatusMessage = "Signed out.";
        }
        catch (Exception exception)
        {
            StatusMessage = "Sign out failed.";
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Enter your email address.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter your password.";
            return false;
        }

        if (IsSignUpMode && Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return false;
        }

        if (!TermsAccepted)
        {
            ErrorMessage = "You must agree to the Terms of Service and acknowledge the Cookie Notice.";
            return false;
        }

        return true;
    }

    private bool CanSubmit() =>
        CanUseAuthForm && TermsAccepted;

    private void SetAuthenticated(string email)
    {
        IsAuthenticated = true;
        AuthenticatedEmail = email;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        StatusMessage = "Signed in successfully.";
    }

    private void RaiseCommandStates()
    {
        SelectSignInCommand.RaiseCanExecuteChanged();
        SelectSignUpCommand.RaiseCanExecuteChanged();
        SubmitCommand.RaiseCanExecuteChanged();
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
