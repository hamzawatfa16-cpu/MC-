using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MyContent.Views;

internal partial class LegalDocumentWindow : Window
{
    public LegalDocumentWindow(string title, string content)
    {
        InitializeComponent();
        Title = $"My Content — {title}";
        TitleText.Text = title;
        DocumentText.Text = content;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
