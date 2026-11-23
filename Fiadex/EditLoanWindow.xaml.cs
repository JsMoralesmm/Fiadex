using System.Windows;

namespace Fiadex;

public partial class EditLoanWindow : Window
{
    private readonly IFiadexDatabase _database;
    private readonly LoanSummary _summary;
    private readonly DateTime? _earliestPaymentDate;

    public EditLoanWindow(IFiadexDatabase database, LoanSummary summary)
    {
        InitializeComponent();
        AccessibilityService.ApplyToWindow(this);
        _database = database;
        _summary = summary;
        AmountBox.Text = summary.Loan.Amount.ToString("0.##");

        var payments = _database.GetPayments(summary.Loan.Id);
        _earliestPaymentDate = payments.Count == 0
            ? null
            : payments.Min(x => x.PaymentDate.Date);
        DateBox.DisplayDateStart = _earliestPaymentDate;
        DateBox.DisplayDateEnd = DateTime.Today;
        DateBox.SelectedDate = summary.Loan.LoanDate;
        NotesBox.Text = summary.Loan.Notes;
    }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!decimal.TryParse(AmountBox.Text, out var amount) ||
                DateBox.SelectedDate is null)
            {
                Message.Text = "Ingrese un monto y una fecha válidos.";
                return;
            }

            var loanDate = DateBox.SelectedDate.Value.Date;
            if (loanDate > DateTime.Today)
            {
                Message.Text = "La fecha del préstamo no puede ser futura.";
                return;
            }

            if (_earliestPaymentDate.HasValue && loanDate > _earliestPaymentDate.Value)
            {
                Message.Text = $"La fecha del préstamo no puede ser posterior al abono más antiguo ({_earliestPaymentDate.Value:dd/MM/yyyy}).";
                return;
            }

            _database.UpdateLoan(
                _summary.Loan.Id,
                _summary.Loan.PersonId,
                amount,
                loanDate,
                NotesBox.Text);
            DialogResult = true;
        }
        catch (Exception exception)
        {
            Message.Text = exception.Message;
        }
    }

    private void CancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
