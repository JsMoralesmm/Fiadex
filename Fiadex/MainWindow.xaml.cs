using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Fiadex;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly SessionService _session = new();
    private readonly IFiadexDatabase _database = new FiadexDatabase();
    private readonly string _username;
    private readonly DispatcherTimer _sessionTimer = new()
    {
        Interval = TimeSpan.FromSeconds(30)
    };
    private ObservableCollection<MonthlyAmount>? _subscribedAnalytics;

    public MainWindow(string username = "admin")
    {
        InitializeComponent();
        AccessibilityService.ApplyToWindow(this);
        AccessibilityService.ApplyToPopup(AccessibilityPopupBorder);
        AppPreferences.Current.PropertyChanged += Preferences_PropertyChanged;
        _username = username;
        _database.Initialize();
        DataContext = new MainViewModel(_database);
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            SubscribeAnalytics(viewModel);
        }
        _session.Start();

        PreviewMouseMove += (_, _) => _session.RegisterActivity();
        PreviewKeyDown += (_, _) => _session.RegisterActivity();
        PreviewKeyDown += AccessibilityShortcut_PreviewKeyDown;
        _sessionTimer.Tick += (_, _) =>
        {
            if (!_session.IsActive())
            {
                Logout();
            }
        };
        _sessionTimer.Start();
    }

    private void AccessibilityClick(object sender, RoutedEventArgs e) =>
        AccessibilityPopup.IsOpen = !AccessibilityPopup.IsOpen;

    private void AccessibilityPopup_Opened(object sender, EventArgs e) => RefreshAccessibilityPopup();

    private void AccessibilityPopup_PreviewKeyDown(object sender, KeyEventArgs e) => HandleAccessibilityShortcut(e);

    private void AccessibilityShortcut_PreviewKeyDown(object sender, KeyEventArgs e) => HandleAccessibilityShortcut(e);

    // El contenido del Popup está en otro árbol visual: se refresca al abrirlo
    // y cuando cambia cualquier preferencia para aplicar zoom, tamaño de texto
    // y bindings, sin sobrescribir los colores DynamicResource de la vista previa.
    private void Preferences_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (AccessibilityPopup.IsOpen)
        {
            RefreshAccessibilityPopup();
        }
    }

    private void RefreshAccessibilityPopup()
    {
        AccessibilityService.ApplyToPopup(AccessibilityPopupBorder);

        void RefreshBindings(DependencyObject element)
        {
            switch (element)
            {
                case ComboBox combo:
                    combo.GetBindingExpression(Selector.SelectedItemProperty)?.UpdateTarget();
                    break;
                case CheckBox check:
                    check.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
                    break;
            }

            var count = VisualTreeHelper.GetChildrenCount(element);
            for (var index = 0; index < count; index++)
            {
                RefreshBindings(VisualTreeHelper.GetChild(element, index));
            }
        }

        RefreshBindings(AccessibilityPopupBorder);
    }

    private void ResetAccessibilityClick(object sender, RoutedEventArgs e) =>
        AppPreferences.Current.Reset();

    private void HandleAccessibilityShortcut(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && AccessibilityPopup.IsOpen)
        {
            AccessibilityPopup.IsOpen = false;
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if ((modifiers & ModifierKeys.Control) == 0 || (modifiers & ModifierKeys.Shift) == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.A:
                AccessibilityPopup.IsOpen = !AccessibilityPopup.IsOpen;
                e.Handled = true;
                break;
            case Key.D1:
            case Key.NumPad1:
                AppPreferences.Current.SetTheme(0);
                e.Handled = true;
                break;
            case Key.D2:
            case Key.NumPad2:
                AppPreferences.Current.SetTheme(1);
                e.Handled = true;
                break;
            case Key.D3:
            case Key.NumPad3:
                AppPreferences.Current.SetTheme(2);
                e.Handled = true;
                break;
            case Key.OemPlus:
            case Key.Add:
                AppPreferences.Current.StepTextSize(1);
                e.Handled = true;
                break;
            case Key.OemMinus:
            case Key.Subtract:
                AppPreferences.Current.StepTextSize(-1);
                e.Handled = true;
                break;
        }
    }

    private void ChangePasswordClick(object sender, RoutedEventArgs e)
    {
        var window = new ChangePasswordWindow(_database, _username)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void LogoutClick(object sender, RoutedEventArgs e)
    {
        Logout();
    }

    private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox ||
            listBox.SelectedItem is not ListBoxItem item ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (HeaderModuleSelector is not null &&
            HeaderModuleSelector.SelectedIndex != listBox.SelectedIndex)
        {
            HeaderModuleSelector.SelectedIndex = listBox.SelectedIndex;
        }
        var moduleName = item.Tag?.ToString() ?? item.ToolTip?.ToString() ?? item.Content?.ToString() ?? "Desconocido";
        viewModel.RegisterModuleUsage(moduleName);
    }

    private void HeaderModuleSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Navigation is not null &&
            HeaderModuleSelector.SelectedIndex >= 0 &&
            Navigation.SelectedIndex != HeaderModuleSelector.SelectedIndex)
        {
            Navigation.SelectedIndex = HeaderModuleSelector.SelectedIndex;
        }
    }

    private void Sidebar_MouseEnter(object sender, MouseEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && !viewModel.IsSidebarPinned)
        {
            viewModel.SetSidebarCollapsed(false);
        }
    }

    private void Sidebar_MouseLeave(object sender, MouseEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && !viewModel.IsSidebarPinned)
        {
            viewModel.SetSidebarCollapsed(true);
        }
    }

    private void DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Column.SortMemberPath))
        {
            return;
        }

        var direction = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        var view = CollectionViewSource.GetDefaultView(((DataGrid)sender).ItemsSource);
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new SortDescription(e.Column.SortMemberPath, direction));
        foreach (var column in ((DataGrid)sender).Columns)
        {
            column.SortDirection = null;
        }
        e.Column.SortDirection = direction;
        e.Handled = true;
    }

    private void SubscribeAnalytics(MainViewModel viewModel)
    {
        if (ReferenceEquals(_subscribedAnalytics, viewModel.MonthlyAnalytics))
        {
            return;
        }

        if (_subscribedAnalytics is not null)
        {
            _subscribedAnalytics.CollectionChanged -= MonthlyAnalytics_CollectionChanged;
        }

        _subscribedAnalytics = viewModel.MonthlyAnalytics;
        _subscribedAnalytics.CollectionChanged += MonthlyAnalytics_CollectionChanged;
    }

    private void MonthlyAnalytics_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(DrawMonthlyLine);
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not MainViewModel viewModel ||
            e.PropertyName != nameof(MainViewModel.MonthlyAnalytics))
        {
            return;
        }

        SubscribeAnalytics(viewModel);
        Dispatcher.BeginInvoke(DrawMonthlyLine);
    }

    private void MonthlyLineChart_Loaded(object sender, RoutedEventArgs e)
    {
        DrawMonthlyLine();
    }

    private void MonthlyLineChart_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawMonthlyLine();
    }

    private void DrawMonthlyLine()
    {
        if (MonthlyLineChart is null ||
            DataContext is not MainViewModel viewModel)
        {
            return;
        }

        MonthlyLineChart.Children.Clear();
        var points = viewModel.MonthlyAnalytics.ToList();
        if (points.Count == 0)
        {
            return;
        }

        var width = Math.Max(MonthlyLineChart.ActualWidth, 200);
        var height = Math.Max(MonthlyLineChart.ActualHeight, 100);
        var max = points.Max(x => x.Payments);
        if (max <= 0)
        {
            max = 1;
        }

        var coordinates = new List<Point>();
        for (var index = 0; index < points.Count; index++)
        {
            var x = 20 + index * ((width - 40) / Math.Max(1, points.Count - 1));
            var y = height - 20 - (double)(points[index].Payments / max * (decimal)(height - 40));
            coordinates.Add(new Point(x, y));

            var ellipse = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = (Brush)Application.Current.Resources["MonochromePrimaryBrush"],
                ToolTip = $"{points[index].Label}: {points[index].Payments:C0}"
            };
            Canvas.SetLeft(ellipse, x - 3.5);
            Canvas.SetTop(ellipse, y - 3.5);
            MonthlyLineChart.Children.Add(ellipse);
        }

        for (var index = 1; index < coordinates.Count; index++)
        {
            MonthlyLineChart.Children.Add(new Line
            {
                X1 = coordinates[index - 1].X,
                Y1 = coordinates[index - 1].Y,
                X2 = coordinates[index].X,
                Y2 = coordinates[index].Y,
                Stroke = (Brush)Application.Current.Resources["MonochromePrimaryBrush"],
                StrokeThickness = 2
            });
        }
    }

    private void Logout()
    {
        _sessionTimer.Stop();
        new LoginWindow().Show();
        Close();
    }

    private void EditPersonClick(object sender, RoutedEventArgs e) => EditSelectedPerson();

    // El doble clic en una persona solo selecciona la fila; la edición queda
    // exclusivamente en el botón "Editar persona" para evitar cambios accidentales.
    private void EditSelectedPerson()
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedPerson is null)
        {
            (DataContext as MainViewModel)?.SetStatus("Seleccione una persona.");
            return;
        }

        var window = new EditPersonWindow(_database, viewModel.SelectedPerson)
        {
            Owner = this
        };
        if (window.ShowDialog() == true)
        {
            viewModel.Refresh();
            viewModel.SetStatus("Persona actualizada.");
        }
    }

    private void DeletePersonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedPerson is null)
        {
            (DataContext as MainViewModel)?.SetStatus("Seleccione una persona.");
            return;
        }

        var hasHistory = viewModel.Loans.Any(loan =>
            loan.Loan.PersonId == viewModel.SelectedPerson.Id);
        var confirmation = hasHistory
            ? $"¿Eliminar a {viewModel.SelectedPerson.Name}? También se eliminarán sus préstamos y abonos."
            : $"¿Eliminar a {viewModel.SelectedPerson.Name}?";

        if (ConfirmDelete(confirmation))
        {
            try
            {
                _database.DeletePerson(viewModel.SelectedPerson.Id);
                viewModel.Refresh();
                viewModel.SetStatus("Persona eliminada.");
            }
            catch (Exception exception)
            {
                viewModel.SetStatus(exception.Message);
            }
        }
    }

    private void EditLoanClick(object sender, RoutedEventArgs e) => EditSelectedLoan();

    // El doble clic nunca abre edición; solo el botón "Editar préstamo".
    private void EditSelectedLoan()
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedLoan is null)
        {
            (DataContext as MainViewModel)?.SetStatus("Seleccione un préstamo.");
            return;
        }

        var loanId = viewModel.SelectedLoan.Loan.Id;
        var window = new EditLoanWindow(_database, viewModel.SelectedLoan)
        {
            Owner = this
        };
        if (window.ShowDialog() == true)
        {
            viewModel.Refresh();
            viewModel.ReportLoanStatus(loanId, "Préstamo actualizado.");
        }
    }

    private void DeleteLoanClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedLoan is null)
        {
            (DataContext as MainViewModel)?.SetStatus("Seleccione un préstamo.");
            return;
        }

        var loan = viewModel.SelectedLoan;
        var confirmation = loan.Balance <= 0
            ? "Este préstamo ya está pagado. ¿Desea eliminarlo del historial?"
            : $"Este préstamo aún tiene un saldo pendiente de {loan.Balance:C0}. ¿Desea eliminarlo de todos modos?";

        if (ConfirmDelete(confirmation))
        {
            _database.DeleteLoan(loan.Loan.Id);
            viewModel.Refresh();
            viewModel.SetStatus("Préstamo eliminado.");
        }
    }

    private void EditPaymentClick(object sender, RoutedEventArgs e) => EditSelectedPayment();

    // El doble clic nunca abre edición; solo el botón "Editar abono".
    private void EditSelectedPayment()
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedPayment is null)
        {
            (DataContext as MainViewModel)?.SetStatus("Seleccione un abono.");
            return;
        }

        var loanId = viewModel.SelectedPayment.LoanId;
        var window = new EditPaymentWindow(_database, viewModel.SelectedPayment)
        {
            Owner = this
        };
        if (window.ShowDialog() == true)
        {
            viewModel.Refresh();
            viewModel.ReportLoanStatus(loanId, "Abono actualizado.");
        }
    }

    private void DeletePaymentClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedPayment is null)
        {
            (DataContext as MainViewModel)?.SetStatus("Seleccione un abono.");
            return;
        }

        if (ConfirmDelete("¿Eliminar el abono seleccionado?"))
        {
            _database.DeletePayment(viewModel.SelectedPayment.Id);
            viewModel.Refresh();
            viewModel.SetStatus("Abono eliminado.");
        }
    }

    private bool ConfirmDelete(string message)
    {
        return MessageBox.Show(
            message,
            "Confirmar eliminación",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }
}