namespace Fiadex;

public interface IFiadexDatabase
{
    void Initialize();
    List<Person> GetPeople();
    long AddPerson(string name, string phone);
    void UpdatePerson(long personId, string name, string phone);
    void DeletePerson(long personId);
    long AddLoan(long personId, decimal amount, DateTime loanDate, string notes);
    void UpdateLoan(long loanId, long personId, decimal amount, DateTime loanDate, string notes);
    void DeleteLoan(long loanId);
    long AddPayment(long loanId, decimal amount, DateTime paymentDate, string notes);
    void UpdatePayment(long paymentId, long loanId, decimal amount, DateTime paymentDate, string notes);
    void DeletePayment(long paymentId);
    List<LoanSummary> GetLoans();
    LoanSummary GetLoanSummary(long loanId);
    List<Payment> GetPayments(long? loanId = null);
    List<PersonHistory> GetPersonHistory();
    List<Payment> GetPaymentHistory(long personId);
    DashboardSummary GetDashboard(DateTime? month = null);
    List<MonthlyAmount> GetMonthlyAnalytics(int monthCount = 12);
    decimal GetMedianLoanAmount();
    int GetMissingFieldsCount();
    bool HasUser();
    void CreateAdmin(string username, string password);
    bool Authenticate(string username, string password);
    bool VerifyCredentials(string username, string password);
    bool IsAccountLocked(string username);
    void ChangePassword(string username, string currentPassword, string newPassword);
    bool UnlockAccount(string username, string password);
}
