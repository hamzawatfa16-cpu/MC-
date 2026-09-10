using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MyContent.Configuration;
using MyContent.Services;
using MyContent.ViewModels;
using MyContent.Views;

namespace MyContent;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var authService = new SupabaseAuthService(
                SupabaseSettings.ProjectUrl,
                SupabaseSettings.PublishableKey);

            desktop.MainWindow = new AuthWindow(new AuthViewModel(authService));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
