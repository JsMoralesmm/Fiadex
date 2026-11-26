using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Fiadex;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppPreferences.Load();
        AccessibilityService.Apply();

        var database = new FiadexDatabase();
        database.Initialize();

        var login = new LoginWindow();
        login.Show();
    }

    // La plantilla de ModalWindowStyle usa un chrome sin botones del sistema.
    private void ModalDragClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is Button) return;
        Window.GetWindow((DependencyObject)sender)?.DragMove();
    }

    private void CloseModalClick(object sender, RoutedEventArgs e)
    {
        Window.GetWindow((DependencyObject)sender)?.Close();
    }
}
