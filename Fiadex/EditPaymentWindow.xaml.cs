using System.Windows;

namespace Fiadex;

public partial class EditPaymentWindow : Window
{
    private readonly IFiadexDatabase _database;
    private readonly Payment _payment;
    private readonly DateTime _loanDate;

    public EditPaymentWindow(IFiadexDatabase database, Payment payment)
    {
        InitializeComponent();
        AccessibilityService.ApplyToWindow(this);
        _database = database;
        _payment = payment;
        AmountBox.Text = payment.Amount.ToString("0.##");

        _loanDate = _database.GetLoanSummary(payment.LoanId).Loan.LoanDate.Date;
        DateBox.DisplayDateStart = _loanDate;
        DateBox.DisplayDateEnd = DateTime.Today;
        DateBox.SelectedDate = ClampToRange(payment.PaymentDate.Date, _loanDate, DateTime.Today);
        NotesBox.Text = payment.Notes;
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

            var paymentDate = DateBox.SelectedDate.Value.Date;
            DomainRules.ValidateAmount(amount, "abono");
            DomainRules.ValidatePaymentDate(_loanDate, paymentDate);

            _database.UpdatePayment(
                _payment.Id,
                _payment.LoanId,
                amount,
                paymentDate,
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

    private static DateTime ClampToRange(DateTime value, DateTime min, DateTime max)
    {
        if (min > max)
        {
            return max;
        }

        return value < min ? min : value > max ? max : value;
    }
}
