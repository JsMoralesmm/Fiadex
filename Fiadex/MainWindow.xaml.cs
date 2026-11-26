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
    private bool _sessionWarningShown;

    public MainWindow(string username = "admin")
    {
        InitializeComponent();
        AccessibilityService.ApplyToWindow(this);
        AccessibilityService.ApplyToPopup(AccessibilityPopupBorder);
        AppPreferences.Current.PropertyChanged += Preferences_PropertyChanged;
        _username = username;
        DataContext = new MainViewModel(_database);
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            SubscribeAnalytics(viewModel);
        }
        _session.Start();

        PreviewMouseMove += (_, _) => RegisterActivity();
        PreviewKeyDown += (_, _) => RegisterActivity();
        PreviewKeyDown += AccessibilityShortcut_PreviewKeyDown;
        _sessionTimer.Tick += SessionTimer_Tick;
        _sessionTimer.Start();
    }

    private void RegisterActivity()
    {
        _session.RegisterActivity();
        _sessionWarningShown = false;
    }

    // Avisa un minuto antes de cerrar la sesión por inactividad y permite continuar.
    private void SessionTimer_Tick(object? sender, EventArgs e)
    {
        if (!_sessionWarningShown &&
            _session.InactiveFor >= SessionService.InactivityLimit - TimeSpan.FromMinutes(1))
        {
            _sessionWarningShown = true;
            var keepWorking = ModalDialogWindow.Confirm(
                this,
                "Sesión por expirar",
                "Su sesión se cerrará en un minuto por inactividad. ¿Desea continuar?",
                "Continuar",
                "Cerrar sesión");
            if (keepWorking)
            {
                _sessionWarningShown = false;
                _session.RegisterActivity();
            }
        }

        if (!_session.IsActive())
        {
            Logout();
        }
    }

    private void AccessibilityClick(object sender, RoutedEventArgs e) =>
        AccessibilityPopup.IsOpen = !AccessibilityPopup.IsOpen;

    private void AccessibilityPopup_Opened(object sender, EventArgs e)
    {
        RefreshAccessibilityPopup();
        FindFirstFocusable(AccessibilityPopupBorder)?.Focus();
    }

    private void AccessibilityPopup_Closed(object sender, EventArgs e) => AccessibilityButton.Focus();

    private static UIElement? FindFirstFocusable(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is UIElement element && element.Focusable && element.IsEnabled)
            {
                return element;
            }
            var nested = FindFirstFocusable(child);
            if (nested is not null) return nested;
        }
        return null;
    }

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

    // Enter envía la consulta; Shift+Enter inserta un salto de línea.
    private void ChatInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }

        if (DataContext is MainViewModel viewModel && !string.IsNullOrWhiteSpace(viewModel.ChatInput))
        {
            e.Handled = true;
            viewModel.SendChatCommand.Execute(null);
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
            DataContext is not MainViewModel)
        {
            return;
        }

        if (HeaderModuleSelector is not null &&
            HeaderModuleSelector.SelectedIndex != listBox.SelectedIndex)
        {
            HeaderModuleSelector.SelectedIndex = listBox.SelectedIndex;
        }
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

        var width = Math.Max(MonthlyLineChart.ActualWidth, 240);
        var height = Math.Max(MonthlyLineChart.ActualHeight, 140);
        var max = points.Max(x => x.Payments);
        if (max <= 0)
        {
            max = 1;
        }

        var primary = (Brush)Application.Current.Resources["MonochromePrimaryBrush"];
        var grid = (Brush)(Application.Current.Resources["MonochromeBorderBrush"] ??
                           Application.Current.Resources["MonochromePrimaryBrush"]);
        var muted = (Brush)(Application.Current.Resources["MonochromeSidebarMutedBrush"] ??
                            Application.Current.Resources["MonochromePrimaryBrush"]);

        var left = 44.0;
        var right = 16.0;
        var top = 12.0;
        var bottom = 34.0;
        var plotWidth = Math.Max(10, width - left - right);
        var plotHeight = Math.Max(10, height - top - bottom);

        // Cuadrícula horizontal y escala: 0, 50 % y 100 % del máximo.
        for (var step = 0; step <= 2; step++)
        {
            var y = top + plotHeight * step / 2.0;
            MonthlyLineChart.Children.Add(new Line
            {
                X1 = left,
                Y1 = y,
                X2 = left + plotWidth,
                Y2 = y,
                Stroke = grid,
                StrokeThickness = 1
            });

            MonthlyLineChart.Children.Add(new TextBlock
            {
                Text = Math.Round(max * (2 - step) / 2m, 0).ToString("C0"),
                FontSize = 10,
                Foreground = muted,
                TextAlignment = TextAlignment.Right,
                Width = left - 8,
                ToolTip = "Escala de abonos del periodo"
            });
            Canvas.SetLeft(MonthlyLineChart.Children[MonthlyLineChart.Children.Count - 1], 0);
            Canvas.SetTop(MonthlyLineChart.Children[MonthlyLineChart.Children.Count - 1], y - 8);
        }

        var coordinates = new List<Point>();
        for (var index = 0; index < points.Count; index++)
        {
            // Con un solo mes, el punto queda centrado en lugar de pegado al borde.
            var x = points.Count == 1
                ? left + plotWidth / 2.0
                : left + index * (plotWidth / (points.Count - 1));
            var y = top + plotHeight - (double)(points[index].Payments / max) * plotHeight;
            coordinates.Add(new Point(x, y));

            var label = new TextBlock
            {
                Text = points[index].Label,
                FontSize = 10,
                Foreground = muted,
                TextAlignment = TextAlignment.Center,
                Width = Math.Min(46, plotWidth / points.Count + 12),
                ToolTip = points[index].Label
            };
            Canvas.SetLeft(label, x - label.Width / 2.0);
            Canvas.SetTop(label, height - bottom + 10);
            MonthlyLineChart.Children.Add(label);
        }

        for (var index = 1; index < coordinates.Count; index++)
        {
            MonthlyLineChart.Children.Add(new Line
            {
                X1 = coordinates[index - 1].X,
                Y1 = coordinates[index - 1].Y,
                X2 = coordinates[index].X,
                Y2 = coordinates[index].Y,
                Stroke = primary,
                StrokeThickness = 2
            });
        }

        foreach (var coordinate in coordinates)
        {
            var point = points[coordinates.IndexOf(coordinate)];
            var ellipse = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = primary,
                ToolTip = $"{point.Label}: {point.Payments:C0}"
            };
            Canvas.SetLeft(ellipse, coordinate.X - 3.5);
            Canvas.SetTop(ellipse, coordinate.Y - 3.5);
            MonthlyLineChart.Children.Add(ellipse);
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

    private void DeletePersonClick(object sender, RoutedEventArgs e) => DeleteSelectedPerson();

    private void DeleteSelectedPerson()
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

    private void DeleteLoanClick(object sender, RoutedEventArgs e) => DeleteSelectedLoan();

    private void DeleteSelectedLoan()
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
            try
            {
                _database.DeleteLoan(loan.Loan.Id);
                viewModel.Refresh();
                viewModel.SetStatus("Préstamo eliminado.");
            }
            catch (Exception exception)
            {
                viewModel.SetStatus($"No se pudo eliminar el préstamo: {exception.Message}");
            }
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

    private void DeletePaymentClick(object sender, RoutedEventArgs e) => DeleteSelectedPayment();

    private void DeleteSelectedPayment()
    {
        if (DataContext is not MainViewModel viewModel || viewModel.SelectedPayment is null)
        {
            (DataContext as MainViewModel)?.SetStatus("Seleccione un abono.");
            return;
        }

        if (ConfirmDelete("¿Eliminar el abono seleccionado?"))
        {
            try
            {
                _database.DeletePayment(viewModel.SelectedPayment.Id);
                viewModel.Refresh();
                viewModel.SetStatus("Abono eliminado.");
            }
            catch (Exception exception)
            {
                viewModel.SetStatus($"No se pudo eliminar el abono: {exception.Message}");
            }
        }
    }

    // Supr elimina la fila seleccionada en las tablas editables.
    private void PeopleGrid_PreviewKeyDown(object sender, KeyEventArgs e) =>
        DeleteFromGrid(e, DeleteSelectedPerson);

    private void LoansGrid_PreviewKeyDown(object sender, KeyEventArgs e) =>
        DeleteFromGrid(e, DeleteSelectedLoan);

    private void PaymentsGrid_PreviewKeyDown(object sender, KeyEventArgs e) =>
        DeleteFromGrid(e, DeleteSelectedPayment);

    private static void DeleteFromGrid(KeyEventArgs e, Action delete)
    {
        if (e.Key != Key.Delete || Keyboard.Modifiers != ModifierKeys.None) return;

        // Si la celda está en edición, Supr debe borrar texto, no la fila.
        if (e.OriginalSource is TextBox || e.OriginalSource is DatePicker ||
            e.OriginalSource is ComboBox || e.OriginalSource is PasswordBox)
        {
            return;
        }

        e.Handled = true;
        delete();
    }

    private bool ConfirmDelete(string message)
    {
        return ModalDialogWindow.Confirm(
            this,
            "Confirmar eliminación",
            message,
            "Eliminar",
            "Cancelar");
    }
}