namespace Fiadex;

public sealed class Person
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class Loan
{
    public long Id { get; set; }
    public long PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime LoanDate { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class Payment
{
    public long Id { get; set; }
    public long LoanId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Notes { get; set; } = string.Empty;

    // Etiqueta legible calculada en la vista; no se persiste en la base.
    public string LoanLabel { get; set; } = string.Empty;
}

public sealed class LoanSummary
{
    public Loan Loan { get; init; } = new();
    public decimal Paid { get; init; }
    public decimal Balance => LoanCalculator.CalculateBalance(Loan.Amount, Paid);
}

public sealed record DashboardSummary(
    decimal PendingTotal,
    decimal PaymentsThisMonth,
    int ActiveLoans,
    int PaymentCount,
    int LoanCount,
    int MissingFields);

public sealed record PersonHistory(
    long PersonId,
    string PersonName,
    decimal LoanTotal,
    decimal PaidTotal,
    decimal Balance,
    int LoanCount,
    int PaymentCount);

public sealed record MonthlyAmount(
    string Label,
    decimal Loans,
    decimal Payments,
    double PaymentBarHeight);

public sealed class ChatMessage
{
    public required string Role { get; init; }
    public required string Text { get; init; }
}

public sealed record ToastMessage(string Text, bool IsError);

public sealed record ReceiptPayment(DateTime Date, decimal Amount, string Notes);

public sealed record ReceiptData(
    long LoanId,
    string ReceiptNumber,
    string ClientName,
    string ClientPhone,
    decimal LoanAmount,
    DateTime LoanDate,
    IReadOnlyList<ReceiptPayment> Payments,
    decimal TotalPaid,
    decimal Balance,
    string Notes,
    DateTime IssuedAt);
