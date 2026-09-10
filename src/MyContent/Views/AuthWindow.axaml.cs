using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using MyContent.Configuration;
using MyContent.Services;
using MyContent.ViewModels;

namespace MyContent.Views;

public partial class AuthWindow : Window
{
    private readonly AuthViewModel _viewModel;
    private readonly AppUpdateService _updateService = new();
    private UpdateCheckResult? _latestUpdate;

    public AuthWindow(AuthViewModel viewModel)
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
        await _viewModel.InitializeAsync();
        _ = CheckForUpdatesAsync(false);
    }

    private async void OnUpdateClicked(object? sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync(true);
    }

    private async Task CheckForUpdatesAsync(bool showResult)
    {
        UpdateButton.IsEnabled = false;
        UpdateButton.Content = "Checking updates…";

        try
        {
            var result = await _updateService.CheckAsync();
            _latestUpdate = result;

            if (result.IsAvailable)
            {
                UpdateButton.Content = $"Update · v{result.TargetVersion}";

                if (showResult)
                {
                    await InstallUpdateAsync(result);
                }
            }
            else if (result.IsUpToDate)
            {
                UpdateButton.Content = $"Up to date · v{AppVersion.Current}";
                if (showResult)
                {
                    await ShowMessageAsync("My Content is up to date", $"You are running v{AppVersion.Current}. The latest release is {result.TargetVersion}.");
                }
            }
            else if (!result.HasRelease)
            {
                UpdateButton.Content = $"v{AppVersion.Current}";
                if (showResult)
                {
                    await ShowMessageAsync("No release yet", "There is no GitHub Release published for My Content yet. The updater is ready and will use the first Windows release package.");
                }
            }
            else
            {
                UpdateButton.Content = $"v{AppVersion.Current}";
                if (showResult)
                {
                    await ShowMessageAsync("Update check failed", result.Error ?? "Unable to check for updates.");
                }
            }
        }
        catch (Exception exception)
        {
            UpdateButton.Content = $"v{AppVersion.Current}";
            if (showResult)
            {
                await ShowMessageAsync("Update check failed", exception.Message);
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
                        Text = "The update will download, install, and restart My Content automatically. Your current window will close during the update."
                    },
                    new Button
                    {
                        Content = "Install update",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                        Tag = result
                    }
                }
            }
        };

        var button = ((StackPanel)confirm.Content!).Children.OfType<Button>().Single();
        button.Click += async (_, _) =>
        {
            confirm.Close();
            UpdateButton.Content = "Installing…";
            await _updateService.ApplyAsync(result, message => UpdateButton.Content = message);
        };

        await confirm.ShowDialog(this);
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
        await window.ShowDialog(this);
    }

    private async void OnTermsClicked(object? sender, RoutedEventArgs e)
    {
        await ShowLegalDocumentAsync("Terms of Service", "TermsOfService.txt");
    }

    private async void OnCookieClicked(object? sender, RoutedEventArgs e)
    {
        await ShowLegalDocumentAsync("Cookie Notice", "CookieNotice.txt");
    }

    private async Task ShowLegalDocumentAsync(string title, string fileName)
    {
        var resourceUri = new Uri($"avares://MyContent/Legal/{fileName}");
        await using var stream = AssetLoader.Open(resourceUri);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();

        var window = new LegalDocumentWindow(title, content);
        await window.ShowDialog(this);
    }
}
