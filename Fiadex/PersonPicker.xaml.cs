using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Fiadex;

/// <summary>
/// Selector de personas con búsqueda por nombre o teléfono. Filtra en memoria
/// y limita la lista visible para que siga siendo ágil con miles de registros.
/// </summary>
public partial class PersonPicker : UserControl
{
    private const int MaxVisibleResults = 250;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<Person>),
            typeof(PersonPicker),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(Person),
            typeof(PersonPicker),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedItemChanged));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(
            nameof(PlaceholderText),
            typeof(string),
            typeof(PersonPicker),
            new PropertyMetadata("Buscar por nombre o teléfono"));

    private bool _isUpdatingText;
    private bool _isSelectingItem;
    private bool _suppressOpenOnFocus;

    public PersonPicker()
    {
        InitializeComponent();
        ResultsList.ItemsSource = Results;
        AccessibilityService.ApplyToPopup(ResultsBorder);
        Loaded += (_, _) => ReloadResults();
    }

    public IEnumerable<Person>? ItemsSource
    {
        get => (IEnumerable<Person>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public Person? SelectedItem
    {
        get => (Person?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public ObservableCollection<Person> Results { get; } = [];

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((PersonPicker)d).ReloadResults();

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((PersonPicker)d).UpdateTextFromSelection();

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingText || SearchBox is null || ResultsList is null)
        {
            return;
        }

        // Si el texto ya no coincide con la persona elegida, se limpia la selección:
        // así el préstamo nunca queda asociado a alguien distinto de lo que se ve.
        if (SelectedItem is not null &&
            !string.Equals(SelectedItem.Name, SearchBox.Text, StringComparison.Ordinal))
        {
            _isSelectingItem = true;
            SetCurrentValue(SelectedItemProperty, null);
            _isSelectingItem = false;
        }

        ReloadResults();
        ResultsPopup.IsOpen = Results.Count > 0 || !string.IsNullOrWhiteSpace(SearchBox.Text);
    }

    private void SearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_suppressOpenOnFocus)
        {
            return;
        }

        SearchBox.SelectAll();
        ReloadResults();
        ResultsPopup.IsOpen = true;
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                ResultsPopup.IsOpen = true;
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                ResultsPopup.IsOpen = true;
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter when ResultsPopup.IsOpen:
                CommitFirstOrSelected();
                e.Handled = true;
                break;
            case Key.Escape:
                ResultsPopup.IsOpen = false;
                UpdateTextFromSelection();
                e.Handled = true;
                break;
        }
    }

    private void DropDownButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Focus();
        ReloadResults();
        ResultsPopup.IsOpen = true;
    }

    private void ResultsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Solo se confirma si el clic cayó sobre una persona. Así, arrastrar la
        // barra de desplazamiento o pulsar una zona vacía no elige a nadie.
        var item = ItemsControl.ContainerFromElement(
            ResultsList,
            e.OriginalSource as DependencyObject) as ListBoxItem;

        if (item?.DataContext is Person person)
        {
            CommitSelection(person);
        }
    }

    private void ResultsPopup_Opened(object sender, EventArgs e) =>
        ResultsBorder.MinWidth = Math.Max(240, ActualWidth);

    private void ReloadResults()
    {
        if (SearchBox is null || ResultsList is null)
        {
            return;
        }

        Results.Clear();
        var total = 0;
        var people = ItemsSource ?? Enumerable.Empty<Person>();
        foreach (var person in PersonSearchFilter.Filter(people, SearchBox.Text))
        {
            total++;
            if (Results.Count < MaxVisibleResults)
            {
                Results.Add(person);
            }
        }

        ResultsList.SelectedIndex = Results.Count > 0 ? 0 : -1;
        Placeholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        ResultSummary.Text = total switch
        {
            0 => "Sin coincidencias",
            _ when total > Results.Count => $"Mostrando {Results.Count} de {total} personas",
            _ => total == 1 ? "1 persona" : $"{total} personas"
        };
    }

    private void UpdateTextFromSelection()
    {
        if (_isSelectingItem || SearchBox is null)
        {
            return;
        }

        _isUpdatingText = true;
        SearchBox.Text = SelectedItem?.Name ?? string.Empty;
        SearchBox.CaretIndex = SearchBox.Text.Length;
        _isUpdatingText = false;
        ReloadResults();
    }

    private void MoveSelection(int delta)
    {
        if (Results.Count == 0)
        {
            return;
        }

        var index = ResultsList.SelectedIndex;
        index = index < 0
            ? (delta > 0 ? 0 : Results.Count - 1)
            : Math.Clamp(index + delta, 0, Results.Count - 1);
        ResultsList.SelectedIndex = index;
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
    }

    private void CommitFirstOrSelected()
    {
        var person = ResultsList.SelectedItem as Person ??
                     (Results.Count == 1 ? Results[0] : null);
        if (person is not null)
        {
            CommitSelection(person);
        }
        else
        {
            ResultsPopup.IsOpen = false;
        }
    }

    private void CommitSelection(Person person)
    {
        _isSelectingItem = true;
        SetCurrentValue(SelectedItemProperty, person);
        _isSelectingItem = false;

        _isUpdatingText = true;
        SearchBox.Text = person.Name;
        SearchBox.CaretIndex = SearchBox.Text.Length;
        _isUpdatingText = false;

        Placeholder.Visibility = Visibility.Collapsed;
        _suppressOpenOnFocus = true;
        SearchBox.Focus();
        ResultsPopup.IsOpen = false;
        _suppressOpenOnFocus = false;
    }
}
