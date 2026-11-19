namespace Fiadex;

public static class LoanCalculator
{
    public static decimal CalculateBalance(decimal principal, decimal payments)
    {
        if (principal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(principal), "El préstamo no puede ser negativo.");
        }

        if (payments < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payments), "Los abonos no pueden ser negativos.");
        }

        return Math.Max(0, principal - payments);
    }
}
