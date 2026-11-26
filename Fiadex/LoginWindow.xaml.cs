using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Fiadex;

public partial class LoginWindow : Window
{
    private readonly FiadexDatabase _database = new();

    public LoginWindow()
    {
        InitializeComponent();
        AccessibilityService.ApplyToWindow(this);

        if (!_database.HasUser())
        {
            Subtitle.Text = "Configure la cuenta administradora";
            SubmitButton.Content = "Crear cuenta";
            UsernameBox.Text = "admin";
            PasswordHelp.Visibility = Visibility.Visible;
        }
    }

    // Enter en usuario o contraseña equivale a pulsar "Iniciar sesión".
    private void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.OriginalSource is not (TextBox or PasswordBox))
        {
            return;
        }

        e.Handled = true;
        SubmitClick(sender, new RoutedEventArgs());
    }

    private void SubmitClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(UsernameBox.Text) ||
                string.IsNullOrEmpty(PasswordField.Password))
            {
                MessageText.Text = "Complete el usuario y la contraseña.";
                return;
            }

            if (!_database.HasUser())
            {
                _database.CreateAdmin(UsernameBox.Text, PasswordField.Password);
                OpenMain();
                return;
            }

            if (_database.Authenticate(UsernameBox.Text, PasswordField.Password))
            {
                UnlockButton.Visibility = Visibility.Collapsed;
                OpenMain();
            }
            else
            {
                MessageText.Text = "Usuario o contraseña incorrectos.";
                UpdateUnlockVisibility();
            }
        }
        catch (Exception exception)
        {
            MessageText.Text = exception.Message;
            UpdateUnlockVisibility();
        }
    }

    private void UpdateUnlockVisibility()
    {
        try
        {
            UnlockButton.Visibility = _database.IsAccountLocked(UsernameBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch
        {
            UnlockButton.Visibility = Visibility.Collapsed;
        }
    }

    private void UnlockClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PasswordField.Password))
        {
            MessageText.Text = "Ingrese la contraseña actual para desbloquear la cuenta.";
            return;
        }

        if (!_database.UnlockAccount(UsernameBox.Text, PasswordField.Password))
        {
            MessageText.Text = "No se pudo desbloquear. Verifique el usuario y la contraseña.";
            return;
        }

        PasswordField.Password = string.Empty;
        UnlockButton.Visibility = Visibility.Collapsed;
        MessageText.Text = "Cuenta desbloqueada.";
    }

    private void OpenMain()
    {
        PasswordField.Password = string.Empty;

        var main = new MainWindow(UsernameBox.Text);
        main.Show();
        Close();
    }
}
