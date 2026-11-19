using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Fiadex;

public partial class PasswordField : UserControl
{
    private bool _syncing;

    public static readonly DependencyProperty PasswordProperty =
        DependencyProperty.Register(
            nameof(Password),
            typeof(string),
            typeof(PasswordField),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPasswordChanged));

    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    public static readonly DependencyProperty IsPasswordVisibleProperty =
        DependencyProperty.Register(
            nameof(IsPasswordVisible),
            typeof(bool),
            typeof(PasswordField),
            new PropertyMetadata(false));

    public bool IsPasswordVisible
    {
        get => (bool)GetValue(IsPasswordVisibleProperty);
        private set => SetValue(IsPasswordVisibleProperty, value);
    }

    public PasswordField()
    {
        InitializeComponent();
    }

    private static void OnPasswordChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var field = (PasswordField)sender;
        if (field._syncing)
        {
            return;
        }

        var value = args.NewValue as string ?? string.Empty;
        field._syncing = true;
        if (field.MaskedBox.Password != value)
        {
            field.MaskedBox.Password = value;
        }

        if (field.RevealedBox.Text != value)
        {
            field.RevealedBox.Text = value;
        }

        field._syncing = false;
    }

    private void MaskedBox_PasswordChanged(object sender, RoutedEventArgs args)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        Password = MaskedBox.Password;
        if (RevealedBox.Text != MaskedBox.Password)
        {
            RevealedBox.Text = MaskedBox.Password;
        }

        _syncing = false;
    }

    private void RevealedBox_TextChanged(object sender, TextChangedEventArgs args)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        Password = RevealedBox.Text;
        if (MaskedBox.Password != RevealedBox.Text)
        {
            MaskedBox.Password = RevealedBox.Text;
        }

        _syncing = false;
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs args)
    {
        IsPasswordVisible = !IsPasswordVisible;

        _syncing = true;
        if (IsPasswordVisible)
        {
            RevealedBox.Text = MaskedBox.Password;
            MaskedBox.Visibility = Visibility.Collapsed;
            RevealedBox.Visibility = Visibility.Visible;
            Slash.Visibility = Visibility.Visible;
            ToggleButton.ToolTip = "Ocultar contraseña";
        }
        else
        {
            MaskedBox.Password = RevealedBox.Text;
            RevealedBox.Visibility = Visibility.Collapsed;
            MaskedBox.Visibility = Visibility.Visible;
            Slash.Visibility = Visibility.Collapsed;
            ToggleButton.ToolTip = "Mostrar contraseña";
        }
        _syncing = false;

        AutomationProperties.SetName(
            ToggleButton,
            IsPasswordVisible ? "Ocultar contraseña" : "Mostrar contraseña");

        if (IsPasswordVisible)
        {
            RevealedBox.Focus();
            RevealedBox.CaretIndex = RevealedBox.Text.Length;
        }
        else
        {
            MaskedBox.Focus();
        }
    }
}
