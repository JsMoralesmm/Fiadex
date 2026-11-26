using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Fiadex;

/// <summary>
/// Añade al calendario del DatePicker una cabecera con selectores de mes y año
/// y una barra inferior con Hoy, Aceptar y Cancelar, conservando la navegación original.
/// </summary>
public static class CalendarPopupEnhancer
{
    public static readonly DependencyProperty EnhanceProperty =
        DependencyProperty.RegisterAttached(
            "Enhance",
            typeof(bool),
            typeof(CalendarPopupEnhancer),
            new PropertyMetadata(false, OnEnhanceChanged));

    private static readonly DependencyProperty IsEnhancedProperty =
        DependencyProperty.RegisterAttached("IsEnhanced", typeof(bool), typeof(CalendarPopupEnhancer));

    public static void SetEnhance(DependencyObject element, bool value) => element.SetValue(EnhanceProperty, value);

    public static bool GetEnhance(DependencyObject element) => (bool)element.GetValue(EnhanceProperty);

    private static void OnEnhanceChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not DatePicker picker || !GetEnhance(picker)) return;
        if (DesignerProperties.GetIsInDesignMode(picker)) return;

        picker.Loaded -= Picker_Loaded;
        picker.Loaded += Picker_Loaded;
    }

    private static void Picker_Loaded(object sender, RoutedEventArgs e)
    {
        var picker = (DatePicker)sender;
        picker.Loaded -= Picker_Loaded;
        picker.Dispatcher.BeginInvoke(new Action(() => Enhance(picker)));
    }

    private static void Enhance(DatePicker picker)
    {
        picker.ApplyTemplate();
        if (picker.Template.FindName("PART_Popup", picker) is not Popup popup) return;
        if (popup.GetValue(IsEnhancedProperty) is true) return;
        if (popup.Child is not Calendar calendar) return;

        popup.SetValue(IsEnhancedProperty, true);
        _ = new CalendarPopupState(picker, popup, calendar);
    }

    private sealed class CalendarPopupState
    {
        private readonly DatePicker _picker;
        private readonly Popup _popup;
        private readonly Calendar _calendar;
        private readonly ComboBox _monthBox;
        private readonly ComboBox _yearBox;
        private DateTime? _dateOnOpen;
        private bool _syncing;
        private bool _reopenAfterSelection;
        private bool _reopening;
        private bool _closingIntentionally;
        private bool _accepted;
        private bool _restoring;

        public CalendarPopupState(DatePicker picker, Popup popup, Calendar calendar)
        {
            _picker = picker;
            _popup = popup;
            _calendar = calendar;
            _calendar.HorizontalAlignment = HorizontalAlignment.Center;

            var root = new Grid { SnapsToDevicePixels = true };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = BuildHeader(out _monthBox, out _yearBox);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // El Calendar ya es hijo lógico del Popup; hay que desconectarlo antes de reubicarlo.
            popup.Child = null;
            Grid.SetRow(_calendar, 1);
            root.Children.Add(_calendar);

            var footer = BuildFooter();
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            popup.Child = root;
            AccessibilityService.ApplyToPopup(root);

            root.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(Popup_PreviewKeyDown));
            _popup.Opened += Popup_Opened;
            _popup.Closed += Picker_DropDownClosed;
            _calendar.SelectedDatesChanged += Calendar_SelectedDatesChanged;
            _calendar.DisplayDateChanged += Calendar_DisplayDateChanged;
            _monthBox.SelectionChanged += MonthYear_SelectionChanged;
            _yearBox.SelectionChanged += MonthYear_SelectionChanged;

            SyncMonthYear();
        }

        private Border BuildHeader(out ComboBox monthBox, out ComboBox yearBox)
        {
            var border = CreateSurface("MonochromeSurfaceAltBrush", new Thickness(10, 8, 10, 4),
                new CornerRadius(10, 10, 0, 0));

            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            panel.Children.Add(CreateLabel("Mes", new Thickness(0, 0, 6, 0)));

            monthBox = new ComboBox
            {
                Width = 116,
                Margin = new Thickness(0, 0, 12, 0)
            };
            AutomationProperties.SetName(monthBox, "Seleccionar mes");
            var monthNames = System.Globalization.DateTimeFormatInfo.CurrentInfo?.MonthNames
                ?? System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.MonthNames;
            for (var i = 0; i < 12; i++)
            {
                monthBox.Items.Add(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(monthNames[i]));
            }
            panel.Children.Add(monthBox);

            panel.Children.Add(CreateLabel("Año", new Thickness(0, 0, 6, 0)));

            yearBox = new ComboBox { Width = 78 };
            AutomationProperties.SetName(yearBox, "Seleccionar año");
            panel.Children.Add(yearBox);

            border.Child = panel;
            return border;
        }

        private static TextBlock CreateLabel(string text, Thickness margin)
        {
            var label = new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = margin
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "MonochromeTextBrush");
            return label;
        }

        private Border BuildFooter()
        {
            var border = CreateSurface("MonochromeSurfaceAltBrush", new Thickness(10, 6, 10, 8),
                new CornerRadius(0, 0, 10, 10));

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var today = CreateButton("Hoy", "Ir a hoy", "SecondaryButtonStyle");
            today.Click += Today_Click;
            panel.Children.Add(today);

            var cancel = CreateButton("Cancelar", "Cancelar fecha", "GhostButtonStyle");
            cancel.Margin = new Thickness(8, 0, 0, 0);
            cancel.Click += Cancel_Click;
            panel.Children.Add(cancel);

            var accept = CreateButton("Aceptar", "Aceptar fecha", null);
            accept.Margin = new Thickness(8, 0, 0, 0);
            accept.Click += Accept_Click;
            panel.Children.Add(accept);

            border.Child = panel;
            return border;
        }

        private static Border CreateSurface(string brushKey, Thickness padding, CornerRadius corners)
        {
            var border = new Border { Padding = padding, CornerRadius = corners };
            border.SetResourceReference(Border.BackgroundProperty, brushKey);
            return border;
        }

        private static Button CreateButton(string content, string automationName, string? styleKey)
        {
            var button = new Button
            {
                Content = content,
                MinHeight = 32,
                Margin = new Thickness(0),
                Padding = new Thickness(12, 4, 12, 4)
            };
            AutomationProperties.SetName(button, automationName);
            if (styleKey is not null && Application.Current.Resources[styleKey] is Style style)
            {
                button.Style = style;
            }
            return button;
        }

        private void Popup_Opened(object? sender, EventArgs e)
        {
            if (!_reopening)
            {
                _dateOnOpen = _picker.SelectedDate;
                _accepted = false;
                _restoring = false;
            }
            _reopenAfterSelection = false;
            _syncing = true;
            try
            {
                RefreshYearItems();
                SyncMonthYearCore();
            }
            finally
            {
                _syncing = false;
            }
        }

        // WPF cierra el desplegable al elegir un día; lo reabrimos para que la barra
        // Hoy/Aceptar/Cancelar siga disponible y Cancelar pueda restaurar la fecha.
        private void Calendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_closingIntentionally && !_restoring) _reopenAfterSelection = true;
        }

        private void Picker_DropDownClosed(object? sender, EventArgs e)
        {
            if (_closingIntentionally)
            {
                _reopenAfterSelection = false;
                return;
            }

            // WPF cierra el desplegable al elegir un día: lo reabrimos para que
            // la barra Hoy/Aceptar/Cancelar siga disponible.
            if (_reopenAfterSelection)
            {
                _reopenAfterSelection = false;
                _reopening = true;
                _picker.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { _picker.IsDropDownOpen = true; }
                    finally { _reopening = false; }
                }));
                return;
            }

            // Escape o clic fuera se tratan como Cancelar si la fecha cambió.
            if (_accepted || _restoring || _picker.SelectedDate == _dateOnOpen) return;

            _restoring = true;
            try { _picker.SelectedDate = _dateOnOpen; }
            finally { _restoring = false; }
        }

        private void Calendar_DisplayDateChanged(object? sender, CalendarDateChangedEventArgs e) => SyncMonthYear();

        private void MonthYear_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_syncing || _monthBox.SelectedIndex < 0 || _yearBox.SelectedItem is not int year) return;

            var desired = new DateTime(year, _monthBox.SelectedIndex + 1, 1);
            var start = _picker.DisplayDateStart;
            var end = _picker.DisplayDateEnd;
            if (start.HasValue)
            {
                var first = new DateTime(start.Value.Year, start.Value.Month, 1);
                if (desired < first) desired = first;
            }
            if (end.HasValue)
            {
                var first = new DateTime(end.Value.Year, end.Value.Month, 1);
                if (desired > first) desired = first;
            }
            _calendar.DisplayDate = desired;
        }

        private void SyncMonthYear()
        {
            _syncing = true;
            try
            {
                SyncMonthYearCore();
            }
            finally
            {
                _syncing = false;
            }
        }

        private void SyncMonthYearCore()
        {
            var date = _calendar.DisplayDate;
            _monthBox.SelectedIndex = date.Month - 1;
            if (!_yearBox.Items.Contains(date.Year))
            {
                RefreshYearItems(date.Year);
            }
            _yearBox.SelectedItem = date.Year;
        }

        private void RefreshYearItems(int? preferredYear = null)
        {
            var startYear = _picker.DisplayDateStart?.Year ?? DateTime.Today.Year - 20;
            var endYear = _picker.DisplayDateEnd?.Year ?? DateTime.Today.Year + 20;
            if (preferredYear.HasValue && (preferredYear.Value < startYear || preferredYear.Value > endYear))
            {
                startYear = preferredYear.Value;
                endYear = preferredYear.Value;
            }

            var selected = _yearBox.SelectedItem as int?;
            _yearBox.Items.Clear();
            for (var year = startYear; year <= endYear; year++)
            {
                _yearBox.Items.Add(year);
            }
            if (selected.HasValue && _yearBox.Items.Contains(selected.Value))
            {
                _yearBox.SelectedItem = selected.Value;
            }
        }

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            var today = DateTime.Today;
            var start = _picker.DisplayDateStart;
            var end = _picker.DisplayDateEnd;
            if (start.HasValue && today < start.Value) today = start.Value.Date;
            if (end.HasValue && today > end.Value) today = end.Value.Date;
            _picker.SelectedDate = today;
            _reopenAfterSelection = false;
        }

        private void Popup_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;

            // Se restaura antes de que WPF procese Escape y cierre el popup.
            e.Handled = true;
            Cancel_Click(sender, e);
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            _accepted = true;
            CloseIntentionally();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _closingIntentionally = true;
            _accepted = false;
            _reopenAfterSelection = false;
            RestoreDate();
            CloseIntentionally();
        }

        private void RestoreDate()
        {
            if (_restoring) return;

            _restoring = true;
            try { _picker.SelectedDate = _dateOnOpen; }
            finally { _restoring = false; }
        }

        private void CloseIntentionally()
        {
            _closingIntentionally = true;
            _picker.IsDropDownOpen = false;
            _picker.Dispatcher.BeginInvoke(new Action(() => _closingIntentionally = false));
        }
    }
}
