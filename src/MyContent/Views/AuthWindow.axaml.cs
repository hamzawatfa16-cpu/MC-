using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using MyContent.ViewModels;

namespace MyContent.Views;

public partial class AuthWindow : Window
{
    private readonly AuthViewModel _viewModel;

    public AuthWindow(AuthViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        InitializeComponent();
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        EmailBox.Focus();
        await _viewModel.InitializeAsync();
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
