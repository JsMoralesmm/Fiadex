using System.Windows;
using System.Windows.Input;

namespace Fiadex;

/// <summary>
/// Diálogo modal oscuro que reemplaza a los MessageBox del sistema.
/// </summary>
public partial class ModalDialogWindow : Window
{
    private ModalDialogWindow(
        Window owner,
        string title,
        string message,
        string confirmText,
        string cancelText,
        bool showCancel,
        string heading)
    {
        InitializeComponent();
        AccessibilityService.ApplyToWindow(this);

        Owner = owner;
        Title = title;
        HeadingText.Text = heading;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;

        if (showCancel)
        {
            CancelButton.Content = cancelText;
        }
        else
        {
            CancelButton.Visibility = Visibility.Collapsed;
        }

        Loaded += (_, _) => ConfirmButton.Focus();
    }

    public static bool Confirm(
        Window owner,
        string title,
        string message,
        string confirmText = "Confirmar",
        string cancelText = "Cancelar")
    {
        var dialog = new ModalDialogWindow(
            owner, title, message, confirmText, cancelText,
            showCancel: true, heading: "Confirme la acción");
        return dialog.ShowDialog() == true;
    }

    public static void ShowInfo(Window owner, string title, string message, string acceptText = "Aceptar")
    {
        var dialog = new ModalDialogWindow(
            owner, title, message, acceptText, "Cancelar",
            showCancel: false, heading: "Operación completada");
        dialog.ShowDialog();
    }

    private void ConfirmClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            DialogResult = false;
        }
    }
}
