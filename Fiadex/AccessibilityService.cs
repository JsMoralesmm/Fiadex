using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace Fiadex;

/// <summary>
/// Aplica las preferencias de accesibilidad a los recursos de la aplicación.
/// Las paletas son siempre monocromáticas. Los colores se cambian en los
/// recursos (no en los pinceles, que WPF congela al cargarlos).
/// </summary>
public static class AccessibilityService
{
    public static readonly DependencyProperty OriginalFontSizeProperty =
        DependencyProperty.RegisterAttached(
            "OriginalFontSize",
            typeof(double),
            typeof(AccessibilityService),
            new PropertyMetadata(0d));

    private static readonly DependencyProperty OriginalLayoutTransformProperty =
        DependencyProperty.RegisterAttached(
            "OriginalLayoutTransform",
            typeof(Transform),
            typeof(AccessibilityService),
            new PropertyMetadata(null));

    // Los Popup viven en otro árbol visual: se registra su contenido para
    // poder aplicarle también el zoom cuando cambia la preferencia.
    private static readonly List<WeakReference<FrameworkElement>> PopupRoots = [];

    private static readonly string[] ColorKeys =
    [
        "MonochromeBgColor",
        "MonochromeSurfaceColor",
        "MonochromeSurfaceAltColor",
        "MonochromeBorderColor",
        "MonochromeBorderLightColor",
        "MonochromeBorderStrongColor",
        "MonochromePrimaryColor",
        "MonochromeHoverColor",
        "MonochromeSecondaryColor",
        "MonochromeMutedColor",
        "MonochromeTextColor",
        "MonochromeDisabledColor",
        "MonochromeSidebarColor",
        "MonochromeSidebarBorderColor",
        "MonochromeSidebarTextColor",
        "MonochromeSidebarMutedColor",
        "MonochromeSidebarHoverColor",
        "MonochromeSidebarSelectedColor",
        "MonochromeOnPrimaryColor"
    ];

    // Los pinceles se declaran en App.xaml con Color="{DynamicResource ...}",
    // pero WPF no propaga el cambio cuando el Color se reemplaza en caliente.
    // Por eso se actualizan explícitamente sus colores aquí.
    private static readonly Dictionary<string, string> BrushToColorKeys = new()
    {
        ["MonochromeBackgroundBrush"] = "MonochromeBgColor",
        ["MonochromeSurfaceBrush"] = "MonochromeSurfaceColor",
        ["MonochromeSurfaceAltBrush"] = "MonochromeSurfaceAltColor",
        ["MonochromeBorderBrush"] = "MonochromeBorderColor",
        ["MonochromeBorderLightBrush"] = "MonochromeBorderLightColor",
        ["MonochromeBorderStrongBrush"] = "MonochromeBorderStrongColor",
        ["MonochromePrimaryBrush"] = "MonochromePrimaryColor",
        ["MonochromeHoverBrush"] = "MonochromeHoverColor",
        ["MonochromeSecondaryBrush"] = "MonochromeSecondaryColor",
        ["MonochromeMutedBrush"] = "MonochromeMutedColor",
        ["MonochromeTextBrush"] = "MonochromeTextColor",
        ["MonochromeDisabledBrush"] = "MonochromeDisabledColor",
        ["MonochromeOnPrimaryBrush"] = "MonochromeOnPrimaryColor",
        ["MonochromeSidebarBrush"] = "MonochromeSidebarColor",
        ["MonochromeSidebarBorderBrush"] = "MonochromeSidebarBorderColor",
        ["MonochromeSidebarTextBrush"] = "MonochromeSidebarTextColor",
        ["MonochromeSidebarMutedBrush"] = "MonochromeSidebarMutedColor",
        ["MonochromeSidebarHoverBrush"] = "MonochromeSidebarHoverColor",
        ["MonochromeSidebarSelectedBrush"] = "MonochromeSidebarSelectedColor"
    };

    private static double _textScale = 1d;
    private static double _interfaceScale = 1d;

    public static void Apply()
    {
        var preferences = AppPreferences.Current;
        ApplyPalette(preferences.Theme, preferences.HighContrast);
        ApplyMotion(preferences.ReduceMotion);
        ApplyFont(preferences.FontChoice);
        ApplyFocus(preferences.FocusLevel);
        ApplyTextScale(ScaleForText(preferences.TextSize));
        ApplyInterfaceScale(ScaleForInterface(preferences.InterfaceScale));

        foreach (Window window in Application.Current.Windows)
        {
            ApplyToWindow(window);
        }

        ApplyToRegisteredPopups();
    }

    /// <summary>
    /// Aplica todas las preferencias a una ventana recién creada. Las ventanas
    /// ya abiertas se actualizan desde <see cref="Apply"/>.
    /// </summary>
    public static void ApplyToWindow(Window window)
    {
        ApplyTextScaleToElement(window);
        ApplyZoomToElement(window.Content as FrameworkElement);
    }

    /// <summary>
    /// Registra el contenido de un Popup y le aplica el estado actual. Los
    /// Popup no heredan el transform de la ventana, así que necesitan su propio
    /// ScaleTransform.
    /// </summary>
    public static void ApplyToPopup(FrameworkElement popupRoot)
    {
        for (var index = PopupRoots.Count - 1; index >= 0; index--)
        {
            if (!PopupRoots[index].TryGetTarget(out var existing))
            {
                PopupRoots.RemoveAt(index);
            }
            else if (ReferenceEquals(existing, popupRoot))
            {
                ApplyTextScaleToElement(popupRoot);
                ApplyZoomToElement(popupRoot);
                return;
            }
        }

        PopupRoots.Add(new WeakReference<FrameworkElement>(popupRoot));
        ApplyTextScaleToElement(popupRoot);
        ApplyZoomToElement(popupRoot);
    }

    private static void ApplyToRegisteredPopups()
    {
        for (var index = PopupRoots.Count - 1; index >= 0; index--)
        {
            if (!PopupRoots[index].TryGetTarget(out var root))
            {
                PopupRoots.RemoveAt(index);
                continue;
            }

            ApplyTextScaleToElement(root);
            ApplyZoomToElement(root);
        }
    }

    private static double ScaleForText(string textSize) => textSize switch
    {
        "Pequeño" => 0.9,
        "Grande" => 1.12,
        _ => 1d
    };

    private static double ScaleForInterface(string interfaceScale) => interfaceScale switch
    {
        "125%" => 1.25,
        "150%" => 1.5,
        _ => 1d
    };

    private static void ApplyPalette(string theme, bool highContrast)
    {
        var palette = GetPalette(theme, highContrast);
        var resources = Application.Current.Resources;

        foreach (var key in ColorKeys)
        {
            if (palette.TryGetValue(key, out var color))
            {
                resources[key] = (Color)ColorConverter.ConvertFromString(color);
            }
        }

        foreach (var (brushKey, colorKey) in BrushToColorKeys)
        {
            if (resources[brushKey] is SolidColorBrush brush && resources[colorKey] is Color color)
            {
                // SetCurrentValue conserva la expresión DynamicResource de Color,
                // así WPF no congela el pincel y todas las referencias existentes
                // (incluidas las de ventanas y popups abiertos) ven el cambio.
                if (!brush.IsFrozen)
                {
                    brush.SetCurrentValue(SolidColorBrush.ColorProperty, color);
                }
                else
                {
                    resources[brushKey] = new SolidColorBrush(color);
                }
            }
        }
    }

    private static Dictionary<string, string> GetPalette(string theme, bool highContrast)
    {
        if (highContrast)
        {
            return theme == "Negro" ? HighContrastDark() : HighContrastLight();
        }

        return theme switch
        {
            "Gris medio" => MediumGray(),
            "Negro" => Black(),
            _ => LightGray()
        };
    }

    private static Dictionary<string, string> LightGray() => new()
    {
        ["MonochromeBgColor"] = "#F8F9FA",
        ["MonochromeSurfaceColor"] = "#FFFFFF",
        ["MonochromeSurfaceAltColor"] = "#F3F4F6",
        ["MonochromeBorderColor"] = "#E4E4E7",
        ["MonochromeBorderLightColor"] = "#F0F0F2",
        ["MonochromeBorderStrongColor"] = "#D1D5DB",
        ["MonochromePrimaryColor"] = "#18181B",
        ["MonochromeHoverColor"] = "#27272A",
        ["MonochromeSecondaryColor"] = "#62626A",
        ["MonochromeMutedColor"] = "#6C6C74",
        ["MonochromeTextColor"] = "#18181B",
        ["MonochromeDisabledColor"] = "#D4D4D8",
        ["MonochromeSidebarColor"] = "#111113",
        ["MonochromeSidebarBorderColor"] = "#222226",
        ["MonochromeSidebarTextColor"] = "#F4F4F5",
        ["MonochromeSidebarMutedColor"] = "#8E8E98",
        ["MonochromeSidebarHoverColor"] = "#1F1F24",
        ["MonochromeSidebarSelectedColor"] = "#27272E",
        ["MonochromeOnPrimaryColor"] = "#FFFFFF"
    };

    private static Dictionary<string, string> MediumGray() => new()
    {
        ["MonochromeBgColor"] = "#D9D9D9",
        ["MonochromeSurfaceColor"] = "#EEEEEE",
        ["MonochromeSurfaceAltColor"] = "#D1D1D1",
        ["MonochromeBorderColor"] = "#A8A8A8",
        ["MonochromeBorderLightColor"] = "#BFBFBF",
        ["MonochromeBorderStrongColor"] = "#8A8A8A",
        ["MonochromePrimaryColor"] = "#2B2B2B",
        ["MonochromeHoverColor"] = "#1F1F1F",
        ["MonochromeSecondaryColor"] = "#444444",
        ["MonochromeMutedColor"] = "#555555",
        ["MonochromeTextColor"] = "#1A1A1A",
        ["MonochromeDisabledColor"] = "#9A9A9A",
        ["MonochromeSidebarColor"] = "#242424",
        ["MonochromeSidebarBorderColor"] = "#3C3C3C",
        ["MonochromeSidebarTextColor"] = "#F5F5F5",
        ["MonochromeSidebarMutedColor"] = "#C9C9C9",
        ["MonochromeSidebarHoverColor"] = "#343434",
        ["MonochromeSidebarSelectedColor"] = "#454545",
        ["MonochromeOnPrimaryColor"] = "#FFFFFF"
    };

    private static Dictionary<string, string> Black() => new()
    {
        ["MonochromeBgColor"] = "#0B0B0F",
        ["MonochromeSurfaceColor"] = "#15151B",
        ["MonochromeSurfaceAltColor"] = "#1E1E26",
        ["MonochromeBorderColor"] = "#2B2B35",
        ["MonochromeBorderLightColor"] = "#22222B",
        ["MonochromeBorderStrongColor"] = "#3F3F4C",
        ["MonochromePrimaryColor"] = "#F5F5F7",
        ["MonochromeHoverColor"] = "#FFFFFF",
        ["MonochromeSecondaryColor"] = "#C7C7D1",
        ["MonochromeMutedColor"] = "#8E8E99",
        ["MonochromeTextColor"] = "#F7F7F9",
        ["MonochromeDisabledColor"] = "#5C5C67",
        ["MonochromeSidebarColor"] = "#08080B",
        ["MonochromeSidebarBorderColor"] = "#1D1D25",
        ["MonochromeSidebarTextColor"] = "#F7F7F9",
        ["MonochromeSidebarMutedColor"] = "#92929D",
        ["MonochromeSidebarHoverColor"] = "#1A1A22",
        ["MonochromeSidebarSelectedColor"] = "#272730",
        ["MonochromeOnPrimaryColor"] = "#0B0B0F"
    };

    private static Dictionary<string, string> HighContrastLight() => new()
    {
        ["MonochromeBgColor"] = "#FFFFFF",
        ["MonochromeSurfaceColor"] = "#FFFFFF",
        ["MonochromeSurfaceAltColor"] = "#EDEDED",
        ["MonochromeBorderColor"] = "#000000",
        ["MonochromeBorderLightColor"] = "#666666",
        ["MonochromeBorderStrongColor"] = "#000000",
        ["MonochromePrimaryColor"] = "#000000",
        ["MonochromeHoverColor"] = "#000000",
        ["MonochromeSecondaryColor"] = "#000000",
        ["MonochromeMutedColor"] = "#333333",
        ["MonochromeTextColor"] = "#000000",
        ["MonochromeDisabledColor"] = "#666666",
        ["MonochromeSidebarColor"] = "#000000",
        ["MonochromeSidebarBorderColor"] = "#FFFFFF",
        ["MonochromeSidebarTextColor"] = "#FFFFFF",
        ["MonochromeSidebarMutedColor"] = "#FFFFFF",
        ["MonochromeSidebarHoverColor"] = "#333333",
        ["MonochromeSidebarSelectedColor"] = "#555555",
        ["MonochromeOnPrimaryColor"] = "#FFFFFF"
    };

    private static Dictionary<string, string> HighContrastDark() => new()
    {
        ["MonochromeBgColor"] = "#000000",
        ["MonochromeSurfaceColor"] = "#000000",
        ["MonochromeSurfaceAltColor"] = "#1A1A1A",
        ["MonochromeBorderColor"] = "#FFFFFF",
        ["MonochromeBorderLightColor"] = "#999999",
        ["MonochromeBorderStrongColor"] = "#FFFFFF",
        ["MonochromePrimaryColor"] = "#FFFFFF",
        ["MonochromeHoverColor"] = "#FFFFFF",
        ["MonochromeSecondaryColor"] = "#FFFFFF",
        ["MonochromeMutedColor"] = "#DDDDDD",
        ["MonochromeTextColor"] = "#FFFFFF",
        ["MonochromeDisabledColor"] = "#888888",
        ["MonochromeSidebarColor"] = "#000000",
        ["MonochromeSidebarBorderColor"] = "#FFFFFF",
        ["MonochromeSidebarTextColor"] = "#FFFFFF",
        ["MonochromeSidebarMutedColor"] = "#FFFFFF",
        ["MonochromeSidebarHoverColor"] = "#333333",
        ["MonochromeSidebarSelectedColor"] = "#555555",
        ["MonochromeOnPrimaryColor"] = "#000000"
    };

    private static void ApplyMotion(bool reduceMotion)
    {
        var resources = Application.Current.Resources;
        resources["SpinnerAnimationVisibility"] = reduceMotion ? Visibility.Collapsed : Visibility.Visible;
        resources["SpinnerStaticVisibility"] = reduceMotion ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void ApplyFont(string fontChoice)
    {
        var family = fontChoice switch
        {
            "Monoespacio" => "Consolas, Courier New, monospace",
            "Serif" => "Georgia, Times New Roman, serif",
            _ => "Segoe UI Variable Text, Segoe UI, -apple-system, sans-serif"
        };

        Application.Current.Resources["AppFontFamily"] = new FontFamily(family);
    }

    private static void ApplyFocus(string focusLevel)
    {
        var resources = Application.Current.Resources;
        var styleKey = focusLevel switch
        {
            "Muy visible" => "VeryStrongFocusVisualStyle",
            "Visible" => "StrongFocusVisualStyle",
            _ => "DefaultFocusVisualStyle"
        };

        resources["AppFocusVisualStyle"] = resources[styleKey];
    }

    private static void ApplyTextScale(double scale)
    {
        // Los recursos Fs* se dejan intactos. El escalado se aplica una sola vez
        // por elemento desde OriginalFontSize, así no se acumula al cambiar la
        // preferencia de tamaño de texto.
        _textScale = scale;
    }

    private static void ApplyInterfaceScale(double scale) => _interfaceScale = scale;

    private static void ApplyZoomToElement(FrameworkElement? element)
    {
        if (element is null)
        {
            return;
        }

        if (element.ReadLocalValue(OriginalLayoutTransformProperty) == DependencyProperty.UnsetValue)
        {
            element.SetValue(OriginalLayoutTransformProperty, element.LayoutTransform);
        }

        var original = element.GetValue(OriginalLayoutTransformProperty) as Transform;
        if (Math.Abs(_interfaceScale - 1d) < 0.0001)
        {
            element.SetCurrentValue(FrameworkElement.LayoutTransformProperty, original);
            return;
        }

        var group = new TransformGroup();
        if (original is TransformGroup originalGroup)
        {
            foreach (var child in originalGroup.Children)
            {
                group.Children.Add(child.Clone());
            }
        }
        else if (original is not null)
        {
            group.Children.Add(original.Clone());
        }

        group.Children.Add(new ScaleTransform(_interfaceScale, _interfaceScale));
        element.SetCurrentValue(FrameworkElement.LayoutTransformProperty, group);
    }

    private static void ApplyTextScaleToElement(DependencyObject root)
    {
        var elements = new List<FrameworkElement>();
        CollectTextElements(root, elements);

        foreach (var element in elements)
        {
            if (element.ReadLocalValue(OriginalFontSizeProperty) == DependencyProperty.UnsetValue)
            {
                element.SetValue(OriginalFontSizeProperty, TextElement.GetFontSize(element));
            }

            var original = (double)element.GetValue(OriginalFontSizeProperty);
            TextElement.SetFontSize(element, original * _textScale);
        }
    }

    private static void CollectTextElements(DependencyObject root, List<FrameworkElement> elements)
    {
        if (root is TextBlock or TextBox or PasswordBox or Button or ComboBox or DatePicker
            or CheckBox or RadioButton or Label or ListBoxItem or TabItem or DataGridRow
            or DataGridColumnHeader or DataGridCell)
        {
            elements.Add((FrameworkElement)root);
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            CollectTextElements(VisualTreeHelper.GetChild(root, index), elements);
        }
    }
}
