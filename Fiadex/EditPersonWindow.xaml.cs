using System.Windows;

namespace Fiadex;

public partial class EditPersonWindow : Window
{
    private readonly IFiadexDatabase _database;
    private readonly Person _person;

    public EditPersonWindow(IFiadexDatabase database, Person person)
    {
        InitializeComponent();
        AccessibilityService.ApplyToWindow(this);
        _database = database;
        _person = person;
        NameBox.Text = person.Name;
        PhoneBox.Text = person.Phone;
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            Message.Text = "El nombre de la persona es obligatorio.";
            NameBox.Focus();
            return;
        }

        var phone = PhoneNumber.DigitsOnly(PhoneBox.Text);
        if (phone.Length != PhoneNumber.Length)
        {
            Message.Text = "El teléfono debe tener 10 dígitos.";
            PhoneBox.Focus();
            return;
        }

        try
        {
            _database.UpdatePerson(_person.Id, name, phone);
            DialogResult = true;
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
