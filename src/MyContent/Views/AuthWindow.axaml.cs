using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using MyContent.Configuration;
using MyContent.Services;
using MyContent.ViewModels;

#pragma warning disable CA2007 // Avalonia UI awaits must resume on the UI thread.

namespace MyContent.Views;

[SuppressMessage("Reliability", "CA2007:Do not directly await a Task", Justification = "Avalonia UI awaits must resume on the UI thread.")]
internal sealed partial class AuthWindow : Window
{
    private readonly AuthViewModel _viewModel;

    internal AuthWindow(AuthViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        InitializeComponent();
        UpdateButton.Content = $"v{AppVersion.Current}";
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        await _viewModel.InitializeAsync().ConfigureAwait(true);
        _ = CheckForUpdatesAsync(false);
    }

    private async void OnUpdateClicked(object? sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(true).ConfigureAwait(true);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The desktop UI must keep running when an update provider fails unexpectedly.")]
    private async Task CheckForUpdatesAsync(bool showResult)
    {
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "Checking updates\u2026";

        try
        {
            var result = await AppUpdateService.CheckAsync().ConfigureAwait(true);

            if (result.Error is not null)
            {
                UpdateButton.Content = $"v{AppVersion.Current}";
                if (showResult)
                {
                    await ShowMessageAsync("Update check failed", "My Content could not check for updates right now.").ConfigureAwait(true);
                }
            }
            else if (result.IsAvailable)
            {
                UpdateButton.Content = $"Update \u00b7 v{result.TargetVersion}";

                if (showResult)
                {
                    await InstallUpdateAsync(result).ConfigureAwait(true);
                }
            }
            else if (result.IsUpToDate)
            {
                UpdateButton.Content = $"Up to date \u00b7 v{AppVersion.Current}";
                if (showResult)
                {
                    await ShowMessageAsync("My Content is up to date", $"You are running v{AppVersion.Current}. The latest release is {result.TargetVersion}.").ConfigureAwait(true);
                }
            }
            else
            {
                UpdateButton.Content = $"v{AppVersion.Current}";
                if (showResult)
                {
                    await ShowMessageAsync("No release yet", "There is no GitHub Release published for My Content yet. The updater is ready for the first Windows release package.").ConfigureAwait(true);
                }
            }
        }
        catch (Exception)
        {
            UpdateButton.Content = $"v{AppVersion.Current}";
            if (showResult)
            {
                await ShowMessageAsync("Update check failed", "My Content could not check for updates right now.").ConfigureAwait(true);
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

        var installButton = new Button
        {
            Content = "Install update",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var cancelButton = new Button
        {
            Content = "Not now",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var confirm = new Window
        {
            Title = "Update available",
            Width = 460,
            Height = 320,
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
                        FontWeight = FontWeight.SemiBold,
                        Text = $"My Content {result.TargetVersion}"
                    },
                    new TextBlock
                    {
                        Text = releaseTitle,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Opacity = 0.75,
                        Text = "The update will download, install, and restart My Content automatically."
                    },
                    installButton,
                    cancelButton
                }
            }
        };

        var shouldInstall = false;
        installButton.Click += (_, _) =>
        {
            shouldInstall = true;
            confirm.Close();
        };
        cancelButton.Click += (_, _) => confirm.Close();

        await confirm.ShowDialog(this).ConfigureAwait(true);

        if (!shouldInstall)
        {
            return;
        }

        UpdateButton.Content = "Installing\u2026";

        try
        {
            await AppUpdateService.ApplyAsync(
                result,
                message => Dispatcher.UIThread.Post(() => UpdateButton.Content = message)).ConfigureAwait(true);
        }
        catch (HttpRequestException)
        {
            await ShowMessageAsync("Update failed", "The update could not be downloaded.").ConfigureAwait(true);
        }
        catch (UriFormatException)
        {
            await ShowMessageAsync("Update failed", "The update link was invalid.").ConfigureAwait(true);
        }
        catch (IOException)
        {
            await ShowMessageAsync("Update failed", "The update could not be saved on this computer.").ConfigureAwait(true);
        }
        catch (UnauthorizedAccessException)
        {
            await ShowMessageAsync("Update failed", "My Content does not have permission to install the update.").ConfigureAwait(true);
        }
        catch (InvalidOperationException)
        {
            await ShowMessageAsync("Update failed", "The update could not be started.").ConfigureAwait(true);
        }
        finally
        {
            UpdateButton.Content = $"Up to date \u00b7 v{AppVersion.Current}";
            UpdateButton.IsEnabled = true;
        }
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
                        FontWeight = FontWeight.SemiBold,
                        Text = title
                    },
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = HorizontalAlignment.Right
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

#pragma warning restore CA2007
