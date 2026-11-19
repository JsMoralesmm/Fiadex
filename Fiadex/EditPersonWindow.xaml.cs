using System.Windows;

namespace Fiadex;

public partial class EditPersonWindow : Window
{
    private readonly IFiadexDatabase _database;
    private readonly Person _person;

    public EditPersonWindow(IFiadexDatabase database, Person person)
    {
        InitializeComponent();
        _database = database;
        _person = person;
        NameBox.Text = person.Name;
        PhoneBox.Text = person.Phone;
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _database.UpdatePerson(_person.Id, NameBox.Text, PhoneBox.Text);
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
