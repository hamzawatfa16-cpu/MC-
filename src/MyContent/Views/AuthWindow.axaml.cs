using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using MyContent.Configuration;
using MyContent.Services;
using MyContent.ViewModels;

namespace MyContent.Views;

internal partial class AuthWindow : Window
{
    private readonly AuthViewModel _viewModel;

    internal AuthWindow(AuthViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        InitializeComponent();
        UpdateButton.Content = $"Update · v{AppVersion.Current}";
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        EmailBox.Focus();
        await _viewModel.InitializeAsync().ConfigureAwait(true);
        _ = CheckForUpdatesAsync(false);
    }

    private async void OnUpdateClicked(object? sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(true).ConfigureAwait(true);
    }

    private async Task CheckForUpdatesAsync(bool showResult)
    {
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "Checking updates…";

        try
        {
            var result = await AppUpdateService.CheckAsync().ConfigureAwait(true);

            if (result.IsAvailable)
            {
                UpdateButton.Content = $"Update · v{result.TargetVersion}";

                if (showResult)
                {
                    await InstallUpdateAsync(result).ConfigureAwait(true);
                }
            }
            else if (result.IsUpToDate)
            {
                UpdateButton.Content = $"Up to date · v{AppVersion.Current}";
                if (showResult)
                {
                    await ShowMessageAsync("My Content is up to date", $"You are running v{AppVersion.Current}. The latest release is {result.TargetVersion}.").ConfigureAwait(true);
                }
            }
            else if (!result.HasRelease)
            {
                UpdateButton.Content = $"v{AppVersion.Current}";
                if (showResult)
                {
                    await ShowMessageAsync("No release yet", "There is no GitHub Release published for My Content yet. The updater is ready and will use the first Windows release package.").ConfigureAwait(true);
                }
            }
            else
            {
                UpdateButton.Content = $"v{AppVersion.Current}";
                if (showResult)
                {
                    await ShowMessageAsync("Update check failed", result.Error ?? "Unable to check for updates.").ConfigureAwait(true);
                }
            }
        }
        catch (Exception exception)
        {
            UpdateButton.Content = $"v{AppVersion.Current}";
            if (showResult)
            {
                await ShowMessageAsync("Update check failed", exception.Message).ConfigureAwait(true);
            }
        }
        finally
        {
            UpdateButton.IsEnabled = true;
        }
    }

    private async Task InstallUpdateAsync(UpdateCheckResult result)
    {
        var releaseTitle = string.IsNullOrWhiteSpace(result.ReleaseName)
            ? result.TargetVersion
            : result.ReleaseName;

        var confirm = new Window
        {
            Title = "Update available",
            Width = 460,
            Height = 280,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(24),
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        FontSize = 22,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold,
                        Text = $"My Content {result.TargetVersion}"
                    },
                    new TextBlock
                    {
                        Text = releaseTitle,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Opacity = 0.75,
                        Text = "The update will download, install, and restart My Content automatically."
                    },
                    new Button
                    {
                        Content = "Install update",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
                    }
                }
            }
        };

        var button = ((StackPanel)confirm.Content!).Children.OfType<Button>().Single();
        button.Click += async (_, _) =>
        {
            confirm.Close();
            UpdateButton.Content = "Installing…";

            try
            {
                await AppUpdateService.ApplyAsync(result, message => UpdateButton.Content = message).ConfigureAwait(true);
            }
            catch (HttpRequestException exception)
            {
                await ShowMessageAsync("Update failed", exception.Message).ConfigureAwait(true);
            }
            catch (UriFormatException exception)
            {
                await ShowMessageAsync("Update failed", exception.Message).ConfigureAwait(true);
            }
            catch (IOException exception)
            {
                await ShowMessageAsync("Update failed", exception.Message).ConfigureAwait(true);
            }
            catch (UnauthorizedAccessException exception)
            {
                await ShowMessageAsync("Update failed", exception.Message).ConfigureAwait(true);
            }
            catch (InvalidOperationException exception)
            {
                await ShowMessageAsync("Update failed", exception.Message).ConfigureAwait(true);
            }
            finally
            {
                UpdateButton.Content = $"v{AppVersion.Current}";
                UpdateButton.IsEnabled = true;
            }
        };

        await confirm.ShowDialog(this).ConfigureAwait(true);
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var window = new Window
        {
            Title = title,
            Width = 440,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(24),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        FontSize = 20,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold,
                        Text = title
                    },
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
                    }
                }
            }
        };

        var button = ((StackPanel)window.Content!).Children.OfType<Button>().Single();
        button.Click += (_, _) => window.Close();
        await window.ShowDialog(this).ConfigureAwait(true);
    }

    private async void OnTermsClicked(object? sender, RoutedEventArgs e)
    {
        await ShowLegalDocumentAsync("Terms of Service", "TermsOfService.txt").ConfigureAwait(true);
    }

    private async void OnCookieClicked(object? sender, RoutedEventArgs e)
    {
        await ShowLegalDocumentAsync("Cookie Notice", "CookieNotice.txt").ConfigureAwait(true);
    }

    private async Task ShowLegalDocumentAsync(string title, string fileName)
    {
        var resourceUri = new Uri($"avares://MyContent/Legal/{fileName}");
        await using var stream = AssetLoader.Open(resourceUri);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync().ConfigureAwait(true);

        var window = new LegalDocumentWindow(title, content);
        await window.ShowDialog(this).ConfigureAwait(true);
    }
}
