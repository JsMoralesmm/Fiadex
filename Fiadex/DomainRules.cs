namespace Fiadex;

// Reglas de negocio compartidas por la interfaz, las ventanas de edición y la base de datos.
public static class DomainRules
{
    public const decimal MaxAmount = 1_000_000_000m;

    public static void ValidateAmount(decimal amount, string label)
    {
        if (amount <= 0m || amount > MaxAmount)
        {
            throw new InvalidOperationException(
                $"El {label} debe ser mayor que cero y no puede superar 1.000.000.000.");
        }
    }

    public static void ValidateLoanDate(DateTime date)
    {
        if (date.Date > DateTime.Today)
        {
            throw new InvalidOperationException("La fecha del préstamo no puede ser futura.");
        }
    }

    public static void ValidatePaymentDate(DateTime loanDate, DateTime paymentDate)
    {
        if (paymentDate.Date > DateTime.Today)
        {
            throw new InvalidOperationException("La fecha del abono no puede ser futura.");
        }

        if (paymentDate.Date < loanDate.Date)
        {
            throw new InvalidOperationException(
                $"La fecha del abono no puede ser anterior a la fecha del préstamo ({loanDate:dd/MM/yyyy}).");
        }
    }

    public static void ValidateRequiredText(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"El {label} es obligatorio.");
        }
    }
}
