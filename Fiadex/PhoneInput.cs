using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Fiadex;

/// <summary>
/// Comportamiento adjunto para los campos de teléfono: solo admite dígitos,
/// recorta a 10 caracteres y limpia lo que se pegue desde el portapapeles.
/// </summary>
public static class PhoneInput
{
    public static readonly DependencyProperty DigitsOnlyProperty =
        DependencyProperty.RegisterAttached(
            "DigitsOnly",
            typeof(bool),
            typeof(PhoneInput),
            new PropertyMetadata(false, OnDigitsOnlyChanged));

    public static bool GetDigitsOnly(DependencyObject element) =>
        (bool)element.GetValue(DigitsOnlyProperty);

    public static void SetDigitsOnly(DependencyObject element, bool value) =>
        element.SetValue(DigitsOnlyProperty, value);

    private static void OnDigitsOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox box)
        {
            return;
        }

        if (e.NewValue is true)
        {
            box.MaxLength = PhoneNumber.Length;
            box.PreviewTextInput += OnPreviewTextInput;
            DataObject.AddPastingHandler(box, OnPaste);
        }
        else
        {
            box.PreviewTextInput -= OnPreviewTextInput;
            DataObject.RemovePastingHandler(box, OnPaste);
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(char.IsAsciiDigit);

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText) is false)
        {
            e.CancelCommand();
            return;
        }

        var box = (TextBox)sender;
        var pasted = PhoneNumber.DigitsOnly(e.SourceDataObject.GetData(DataFormats.UnicodeText) as string);
        var available = Math.Max(0, PhoneNumber.Length - (box.Text.Length - box.SelectionLength));
        var digits = pasted[..Math.Min(pasted.Length, available)];

        if (digits.Length == 0)
        {
            e.CancelCommand();
            return;
        }

        e.DataObject = new DataObject(DataFormats.UnicodeText, digits);
    }
}
