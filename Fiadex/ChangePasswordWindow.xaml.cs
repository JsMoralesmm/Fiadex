using System.Windows;

namespace Fiadex;

public partial class ChangePasswordWindow : Window
{
    private readonly IFiadexDatabase _database;
    private readonly string _username;

    public ChangePasswordWindow(IFiadexDatabase database, string username)
    {
        InitializeComponent();
        AccessibilityService.ApplyToWindow(this);
        _database = database;
        _username = username;
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (NewPasswordField.Password != RepeatPasswordField.Password)
        {
            Message.Text = "Las contraseñas nuevas no coinciden.";
            return;
        }

        try
        {
            _database.ChangePassword(
                _username,
                CurrentPasswordField.Password,
                NewPasswordField.Password);
            ModalDialogWindow.ShowInfo(
                this,
                "Contraseña actualizada",
                "Su clave de acceso se actualizó correctamente.",
                "Aceptar");
            Close();
        }
        catch (Exception exception)
        {
            Message.Text = exception.Message;
        }
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
