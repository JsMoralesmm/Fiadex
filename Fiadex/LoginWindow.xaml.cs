using System.Windows;

namespace Fiadex;

public partial class LoginWindow : Window
{
    private readonly IFiadexDatabase _database = new FiadexDatabase();

    public LoginWindow()
    {
        InitializeComponent();
        _database.Initialize();

        if (!_database.HasUser())
        {
            Subtitle.Text = "Configure la cuenta administradora";
            SubmitButton.Content = "Crear cuenta";
            UsernameBox.Text = "admin";
            PasswordHelp.Visibility = Visibility.Visible;
        }
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
                OpenMain();
            }
            else
            {
                MessageText.Text = "Usuario o contraseña incorrectos.";
                UnlockButton.Visibility = Visibility.Visible;
            }
        }
        catch (Exception exception)
        {
            MessageText.Text = exception.Message;
            UnlockButton.Visibility = Visibility.Visible;
        }
    }

    private void UnlockClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "¿Confirmar recuperación como administrador y desbloquear la cuenta?",
            "Recuperación de acceso",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _database.UnlockAccount(UsernameBox.Text);
            MessageText.Text = "Cuenta desbloqueada.";
            UnlockButton.Visibility = Visibility.Collapsed;
        }
    }

    private void OpenMain()
    {
        var main = new MainWindow(UsernameBox.Text);
        main.Show();
        Close();
    }
}
