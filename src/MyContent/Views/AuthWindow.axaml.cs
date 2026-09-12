using System.Diagnostics;
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

#pragma warning disable CA2007

namespace MyContent.Views;

[SuppressMessage("Reliability", "CA2007:Do not directly await a Task", Justification = "Avalonia UI awaits must resume on the UI thread.")]
internal sealed partial class AuthWindow : Window
{
    private readonly AuthViewModel _viewModel;
    private readonly bool _openedAfterUpdate;

    internal AuthWindow(AuthViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        InitializeComponent();
        _openedAfterUpdate = Environment.GetCommandLineArgs()
            .Any(argument => string.Equals(argument, "/updated", StringComparison.OrdinalIgnoreCase));
        UpdateButton.Content = "v" + AppVersion.Current;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        await _viewModel.InitializeAsync().ConfigureAwait(true);
        _ = CheckForUpdatesAsync(userRequested: false);

        if (_openedAfterUpdate)
        {
            await ShowMessageAsync(
                "My Content is ready",
                "The latest version was installed and My Content has been reopened.").ConfigureAwait(true);
        }
    }

    private async void OnUpdateClicked(object? sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(userRequested: true).ConfigureAwait(true);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The desktop UI must keep running when an update provider fails unexpectedly.")]
    private async Task CheckForUpdatesAsync(bool userRequested)
    {
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "Checking updates...";

        try
        {
            var result = await AppUpdateService.CheckAsync().ConfigureAwait(true);

            if (result.Error is not null)
            {
                UpdateButton.Content = "v" + AppVersion.Current;
                if (userRequested)
                {
                    await ShowMessageAsync("Update check failed", "My Content could not check for updates right now.").ConfigureAwait(true);
                }

                return;
            }

            if (result.IsAvailable)
            {
                if (!userRequested && !AppUpdateService.CanApplyInPlace)
                {
                    UpdateButton.Content = "Update available";
                    return;
                }

                await ApplyUpdateAsync(result).ConfigureAwait(true);
                return;
            }

            if (result.IsUpToDate)
            {
                UpdateButton.Content = "Up to date  v" + AppVersion.Current;
                if (userRequested)
                {
                    await ShowMessageAsync("My Content is up to date", "You are already running the latest version, v" + AppVersion.Current + ".").ConfigureAwait(true);
                }

                return;
            }

            UpdateButton.Content = "v" + AppVersion.Current;
            if (userRequested)
            {
                await ShowMessageAsync("No update yet", "There is no newer My Content release to install.").ConfigureAwait(true);
            }
        }
        catch (Exception)
        {
            UpdateButton.Content = "v" + AppVersion.Current;
            if (userRequested)
            {
                await ShowMessageAsync("Update check failed", "My Content could not check for updates right now.").ConfigureAwait(true);
            }
        }
        finally
        {
            UpdateButton.IsEnabled = true;
        }
    }

    private async Task ApplyUpdateAsync(UpdateCheckResult result)
    {
        if (!AppUpdateService.CanApplyInPlace)
        {
            UpdateButton.Content = "Update available";
            OpenReleasePage();
            await ShowMessageAsync(
                "Update available",
                "A newer My Content release is ready. The download page was opened so you can install it.").ConfigureAwait(true);
            return;
        }

        UpdateButton.IsEnabled = false;
        ShowUpdateOverlay("Downloading " + result.TargetVersion + "...");

        try
        {
            await AppUpdateService.ApplyAsync(
                result,
                message => Dispatcher.UIThread.Post(() => ShowUpdateOverlay(message))).ConfigureAwait(true);
        }
        catch (HttpRequestException)
        {
            HideUpdateOverlay();
            await ShowMessageAsync("Update failed", "The update could not be downloaded.").ConfigureAwait(true);
        }
        catch (UriFormatException)
        {
            HideUpdateOverlay();
            await ShowMessageAsync("Update failed", "The update link was invalid.").ConfigureAwait(true);
        }
        catch (IOException)
        {
            HideUpdateOverlay();
            await ShowMessageAsync("Update failed", "The update could not be saved on this computer.").ConfigureAwait(true);
        }
        catch (UnauthorizedAccessException)
        {
            HideUpdateOverlay();
            await ShowMessageAsync("Update failed", "My Content does not have permission to install the update.").ConfigureAwait(true);
        }
        catch (InvalidOperationException)
        {
            HideUpdateOverlay();
            await ShowMessageAsync("Update failed", "The update could not be started.").ConfigureAwait(true);
        }
        finally
        {
            UpdateButton.Content = "v" + AppVersion.Current;
            UpdateButton.IsEnabled = true;
        }
    }

    private void ShowUpdateOverlay(string message)
    {
        UpdateStatusText.Text = message;
        UpdateOverlay.IsVisible = true;
        UpdateButton.Content = message;
    }

    private void HideUpdateOverlay()
    {
        UpdateOverlay.IsVisible = false;
    }

    private static void OpenReleasePage()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/" + AppVersion.Repository + "/releases/latest",
            UseShellExecute = true
        });
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var window = new Window
        {
            Title = title,
            Width = 440,
            Height = 220,
            Background = new SolidColorBrush(Color.Parse("#121624")),
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
                        Text = title,
                        Foreground = new SolidColorBrush(Color.Parse("#F2F4FA"))
                    },
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.Parse("#C9D0E3"))
                    },
                    new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right }
                }
            }
        };

        var button = ((StackPanel)window.Content!).Children.OfType<Button>().Single();
        button.Click += (_, _) => window.Close();
        await window.ShowDialog(this).ConfigureAwait(true);
    }

    private async void OnTermsClicked(object? sender, RoutedEventArgs e) =>
        await ShowLegalDocumentAsync("Terms of Service", "TermsOfService.txt").ConfigureAwait(true);

    private async void OnCookieClicked(object? sender, RoutedEventArgs e) =>
        await ShowLegalDocumentAsync("Cookie Notice", "CookieNotice.txt").ConfigureAwait(true);

    private async Task ShowLegalDocumentAsync(string title, string fileName)
    {
        var resourceUri = new Uri($"avares://MyContent/Legal/{fileName}");
        await using var stream = AssetLoader.Open(resourceUri);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync().ConfigureAwait(true);
        await new LegalDocumentWindow(title, content).ShowDialog(this).ConfigureAwait(true);
    }
}

#pragma warning restore CA2007
