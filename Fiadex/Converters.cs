using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Fiadex;

public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseCountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool flag && flag ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool flag && flag ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not bool flag || !flag;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not bool flag || !flag;
}

public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Estado visible del préstamo: saldo en cero significa deuda saldada.
public sealed class BalanceStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is decimal balance && balance <= 0m ? "Pagado" : "Pendiente";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SidebarWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool collapsed && collapsed ? 64d : 230d;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// El menú colapsado debe mostrar solo iconos centrados y sin márgenes de texto.
public sealed class SidebarGridColumnConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool collapsed && collapsed ? 1 : 2;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SidebarColumnSpanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool collapsed && collapsed ? 2 : 1;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SidebarIconMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool collapsed && collapsed ? new Thickness(0) : new Thickness(2, 0, 12, 0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// En el menú colapsado el icono ocupa la columna flexible y se centra.
public sealed class SidebarNavIconColumnConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool collapsed && collapsed ? 1 : 0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SidebarNavPaddingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool collapsed && collapsed ? new Thickness(0) : new Thickness(10, 0, 10, 0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SidebarActionIconMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool collapsed && collapsed ? new Thickness(0) : new Thickness(0, 0, 8, 0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SidebarActionAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool collapsed && collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class SidebarActionPaddingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool collapsed && collapsed ? new Thickness(0, 8, 0, 8) : new Thickness(10, 4, 10, 4);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class RoleToAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase)
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Las burbujas toman los pinceles del tema en cada conversión: así siguen
// siendo monocromáticas y responden al cambio de tema/contraste en caliente.
public sealed class RoleToBubbleBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase)
            ? AppBrush("MonochromePrimaryBrush")
            : AppBrush("MonochromeSurfaceAltBrush");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    internal static object AppBrush(string key) =>
        Application.Current?.Resources[key] ?? Brushes.Transparent;
}

public sealed class RoleToBubbleForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase)
            ? RoleToBubbleBackgroundConverter.AppBrush("MonochromeOnPrimaryBrush")
            : RoleToBubbleBackgroundConverter.AppBrush("MonochromeTextBrush");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class RoleToBubbleBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase)
            ? RoleToBubbleBackgroundConverter.AppBrush("MonochromePrimaryBrush")
            : RoleToBubbleBackgroundConverter.AppBrush("MonochromeBorderBrush");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class RoleToSenderNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase)
            ? "Tú"
            : "Fiadex IA";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class RoleToCornerRadiusConverter : IValueConverter
{
    private static readonly CornerRadius UserRadius = new(16, 16, 3, 16);
    private static readonly CornerRadius AssistantRadius = new(16, 16, 16, 3);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase)
            ? UserRadius
            : AssistantRadius;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class RoleToMarginConverter : IValueConverter
{
    private static readonly Thickness UserMargin = new(40, 4, 0, 10);
    private static readonly Thickness AssistantMargin = new(0, 4, 40, 10);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string role && role.Equals("user", StringComparison.OrdinalIgnoreCase)
            ? UserMargin
            : AssistantMargin;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

