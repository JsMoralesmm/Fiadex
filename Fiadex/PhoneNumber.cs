using System.ComponentModel.DataAnnotations;

namespace Fiadex;

/// <summary>
/// Reglas del teléfono colombiano: exactamente 10 dígitos.
/// </summary>
public static class PhoneNumber
{
    public const int Length = 10;

    /// <summary>
    /// Deja solo los dígitos del texto. Si viene con el indicativo del país
    /// (+57 300 123 4567) se quita el 57 para quedarse con los 10 números.
    /// </summary>
    public static string DigitsOnly(string? text)
    {
        var digits = new string((text ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        return digits.Length == Length + 2 && digits.StartsWith("57", StringComparison.Ordinal)
            ? digits[2..]
            : digits;
    }

    public static bool IsValid(string? text) => DigitsOnly(text).Length == Length;
}

/// <summary>
/// Valida que el teléfono tenga los 10 dígitos de un número colombiano.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ColombianPhoneAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => PhoneNumber.IsValid(value as string);
}
