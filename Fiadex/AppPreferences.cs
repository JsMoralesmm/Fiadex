using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fiadex;

/// <summary>
/// Preferencias de accesibilidad. Se guardan en un archivo JSON dentro de
/// %LOCALAPPDATA%\Fiadex, sin tocar la base de datos.
/// </summary>
public sealed class AppPreferences : INotifyPropertyChanged
{
    private static readonly string PreferencesDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Fiadex");
    private static readonly string PreferencesFile = Path.Combine(
        PreferencesDirectory, "accessibility.json");

    private string _textSize = "Normal";
    private bool _highContrast;
    private bool _reduceMotion;
    private string _focusLevel = "Visible";
    private string _theme = "Negro";
    private string _interfaceScale = "100%";
    private string _fontChoice = "Estándar";

    public static AppPreferences Current { get; private set; } = new();

    public string[] TextSizes { get; } = ["Pequeño", "Normal", "Grande"];
    public string[] Themes { get; } = ["Gris claro", "Gris medio", "Negro"];
    public string[] InterfaceScales { get; } = ["100%", "125%", "150%"];
    public string[] FontChoices { get; } = ["Estándar", "Monoespacio", "Serif"];
    public string[] FocusLevels { get; } = ["Normal", "Visible", "Muy visible"];

    public string TextSize
    {
        get => _textSize;
        set => Set(ref _textSize, value);
    }

    public bool HighContrast
    {
        get => _highContrast;
        set => Set(ref _highContrast, value);
    }

    public bool ReduceMotion
    {
        get => _reduceMotion;
        set => Set(ref _reduceMotion, value);
    }

    /// <summary>Nivel de foco: Normal, Visible o Muy visible.</summary>
    public string FocusLevel
    {
        get => _focusLevel;
        set => Set(ref _focusLevel, value);
    }

    /// <summary>Compatibilidad con preferencias guardadas por versiones anteriores.</summary>
    public bool FocusVisible
    {
        get => _focusLevel != "Normal";
        set => FocusLevel = value ? "Visible" : "Normal";
    }

    /// <summary>Escala de toda la interfaz: 100 %, 125 % o 150 %.</summary>
    public string InterfaceScale
    {
        get => _interfaceScale;
        set => Set(ref _interfaceScale, value);
    }

    /// <summary>Tipografía: Estándar, Monoespacio o Serif.</summary>
    public string FontChoice
    {
        get => _fontChoice;
        set => Set(ref _fontChoice, value);
    }

    public string Theme
    {
        get => _theme;
        set => Set(ref _theme, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static void Load()
    {
        try
        {
            if (!File.Exists(PreferencesFile))
            {
                Current = new AppPreferences();
                return;
            }

            var data = JsonSerializer.Deserialize<PreferencesData>(File.ReadAllText(PreferencesFile));
            Current = new AppPreferences
            {
                _textSize = data?.TextSize ?? "Normal",
                _highContrast = data?.HighContrast ?? false,
                _reduceMotion = data?.ReduceMotion ?? false,
                _focusLevel = data?.FocusLevel ?? (data?.FocusVisible == true ? "Visible" : "Normal"),
                _theme = data?.Theme ?? "Negro",
                _interfaceScale = data?.InterfaceScale ?? "100%",
                _fontChoice = data?.FontChoice ?? "Estándar"
            };
        }
        catch
        {
            // Un archivo dañado no debe impedir el inicio: se usan los valores por defecto.
            Current = new AppPreferences();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(PreferencesDirectory);
            var data = new PreferencesData
            {
                TextSize = _textSize,
                HighContrast = _highContrast,
                ReduceMotion = _reduceMotion,
                FocusVisible = _focusLevel != "Normal",
                FocusLevel = _focusLevel,
                Theme = _theme,
                InterfaceScale = _interfaceScale,
                FontChoice = _fontChoice
            };
            File.WriteAllText(PreferencesFile, JsonSerializer.Serialize(data));
        }
        catch
        {
            // Las preferencias son auxiliares: no se interrumpe la aplicación si no se pueden guardar.
        }
    }

    /// <summary>Vuelve a los valores de fábrica y notifica todos los cambios de una vez.</summary>
    public void Reset()
    {
        _textSize = "Normal";
        _highContrast = false;
        _reduceMotion = false;
        _focusLevel = "Visible";
        _theme = "Negro";
        _interfaceScale = "100%";
        _fontChoice = "Estándar";

        Save();
        AccessibilityService.Apply();

        OnPropertyChanged(nameof(TextSize));
        OnPropertyChanged(nameof(HighContrast));
        OnPropertyChanged(nameof(ReduceMotion));
        OnPropertyChanged(nameof(FocusLevel));
        OnPropertyChanged(nameof(FocusVisible));
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(InterfaceScale));
        OnPropertyChanged(nameof(FontChoice));
    }

    /// <summary>Cambia el tema por índice basado en 0 (0, 1 o 2) desde los atajos.</summary>
    public void SetTheme(int index)
    {
        if (index >= 0 && index < Themes.Length)
        {
            Theme = Themes[index];
        }
    }

    /// <summary>Sube (+) o baja (-) el tamaño de texto desde los atajos de teclado.</summary>
    public void StepTextSize(int direction)
    {
        var index = Array.IndexOf(TextSizes, _textSize);
        var next = Math.Clamp(index + direction, 0, TextSizes.Length - 1);
        if (next != index)
        {
            TextSize = TextSizes[next];
        }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Save();
        // Primero se actualizan los recursos; así los suscriptores a
        // PropertyChanged ya ven la paleta nueva.
        AccessibilityService.Apply();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class PreferencesData
    {
        [JsonPropertyName("textSize")]
        public string? TextSize { get; set; }

        [JsonPropertyName("highContrast")]
        public bool HighContrast { get; set; }

        [JsonPropertyName("reduceMotion")]
        public bool ReduceMotion { get; set; }

        [JsonPropertyName("focusVisible")]
        public bool FocusVisible { get; set; }

        [JsonPropertyName("focusLevel")]
        public string? FocusLevel { get; set; }

        [JsonPropertyName("theme")]
        public string? Theme { get; set; }

        [JsonPropertyName("interfaceScale")]
        public string? InterfaceScale { get; set; }

        [JsonPropertyName("fontChoice")]
        public string? FontChoice { get; set; }
    }
}
