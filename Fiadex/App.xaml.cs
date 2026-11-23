using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

        var login = new LoginWindow();
        login.Show();
    }

    // La plantilla interna de DatePicker fija el Foreground del botón del
    // calendario a #333333. Como el estilo global de Button le da un fondo
    // oscuro, el glifo queda invisible. Se corrige al cargar el control.
    private void DatePicker_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DatePicker picker) return;

        picker.ApplyTemplate();
        if (picker.Template.FindName("PART_Button", picker) is Button calendarButton &&
            Application.Current.Resources["MonochromeSurfaceBrush"] is Brush foreground)
        {
            calendarButton.Foreground = foreground;
        }
    }
}

