using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Fiadex;

public partial class MainViewModel : ObservableValidator
{
    private readonly IFiadexDatabase _database;
    private readonly ChatService _chatService;
    private readonly ToastService _toastService;
    private readonly Dictionary<string, int> _moduleCounters = new();
    private List<Person> _allHistoryClients = [];

    private List<Person> _allPeople = [];
    [ObservableProperty] private ObservableCollection<Person> _people = [];
    [ObservableProperty] private ObservableCollection<Person> _filteredPeople = [];
    [ObservableProperty] private ObservableCollection<LoanSummary> _loans = [];
    [ObservableProperty] private ObservableCollection<Payment> _payments = [];
    [ObservableProperty] private Person? _selectedPerson;
    [ObservableProperty] private LoanSummary? _selectedLoan;
    [ObservableProperty] private Payment? _selectedPayment;
    [ObservableProperty] private string _statusMessage = "Listo";
    [ObservableProperty] private decimal _pendingTotal;
    [ObservableProperty] private decimal _paymentsThisMonth;
    [ObservableProperty] private int _activeLoans;
    [ObservableProperty] private int _paymentCount;
    [ObservableProperty] private int _loanCount;
    [ObservableProperty] private int _missingFields;
    [ObservableProperty] private string _historyClientSearch = string.Empty;
    [ObservableProperty] private string _historyClientResultText = "0 clientes";
    [ObservableProperty] private ObservableCollection<Person> _historyClients = [];
    [ObservableProperty] private Person? _historySelectedPerson;
    [ObservableProperty] private bool _hasHistoryClient;
    [ObservableProperty] private PersonHistory? _historySummary;
    [ObservableProperty] private ObservableCollection<LoanSummary> _historyLoans = [];
    [ObservableProperty] private ObservableCollection<Payment> _historyPayments = [];
    [ObservableProperty] private ObservableCollection<MonthlyAmount> _monthlyAnalytics = [];
    [ObservableProperty] private decimal _medianLoanAmount;
    [ObservableProperty] private int _errorCount;
    [ObservableProperty] private ObservableCollection<MonthlyAmount>? _lineSeries;
    [ObservableProperty] private ObservableCollection<ModuleUsage> _moduleUsage = [];
    [ObservableProperty] private ObservableCollection<ChatMessage> _chatMessages = [];
    [ObservableProperty] private string _chatInput = string.Empty;
    [ObservableProperty] private bool _chatIsLoading;
    [ObservableProperty] private string _bannerMessage = string.Empty;
    [ObservableProperty] private bool _bannerIsError;
    [ObservableProperty] private bool _isSidebarCollapsed;
    [ObservableProperty] private bool _isSidebarPinned = true;
    [ObservableProperty] private ObservableCollection<string> _sections =
        new(["Dashboard", "Personas", "Préstamos", "Historial", "Abonos", "Asistente IA"]);

    [ObservableProperty]
    [Required(ErrorMessage = "El nombre de la persona es obligatorio.")]
    private string _personName = string.Empty;

    [ObservableProperty] private string _personPhone = string.Empty;

    [ObservableProperty] private string _personSearch = string.Empty;
    [ObservableProperty] private string _personResultText = "0 resultados";

    [ObservableProperty]
    [Range(0.01, 1000000000, ErrorMessage = "El monto debe ser mayor que cero.")]
    private decimal _loanAmount;

    [ObservableProperty] private DateTime _loanDate = DateTime.Today;
    [ObservableProperty] private DateTime _loanMaxDate = DateTime.Today;
    [ObservableProperty] private string _loanNotes = string.Empty;

    [ObservableProperty]
    [Range(0.01, 1000000000, ErrorMessage = "El abono debe ser mayor que cero.")]
    private decimal _paymentAmount;

    [ObservableProperty] private DateTime _paymentDate = DateTime.Today;
    [ObservableProperty] private DateTime? _paymentMinDate = DateTime.Today;
    [ObservableProperty] private DateTime _paymentMaxDate = DateTime.Today;
    [ObservableProperty] private string _paymentNotes = string.Empty;

    public ObservableCollection<ToastMessage> Toasts => _toastService.Messages;

    public MainViewModel(IFiadexDatabase database, ToastService? toastService = null)
    {
        _database = database;
        _database.Initialize();
        _toastService = toastService ?? new ToastService();
        _chatService = new ChatService(_database);
        Refresh();
        ChatMessages.Add(new ChatMessage
        {
            Role = "assistant",
            Text = "Hola. Puedes preguntarme por préstamos, abonos, saldos e historial. Solo consulto información local."
        });
    }

    [RelayCommand]
    private void AddPerson()
    {
        ValidateProperty(PersonName, nameof(PersonName));
        if (GetErrors(nameof(PersonName)).Cast<object>().Any())
        {
            Failure("El nombre de la persona es obligatorio.");
            return;
        }

        try
        {
            _database.AddPerson(PersonName, PersonPhone);
            PersonName = string.Empty;
            PersonPhone = string.Empty;
            ClearErrors(nameof(PersonName));
            Refresh();
            Success("Persona registrada.");
        }
        catch (Exception exception)
        {
            Failure(exception.Message);
        }
    }

    [RelayCommand]
    private void AddLoan()
    {
        ValidateProperty(LoanAmount, nameof(LoanAmount));
        if (GetErrors(nameof(LoanAmount)).Cast<object>().Any())
        {
            Failure("El monto del préstamo debe ser mayor que cero.");
            return;
        }

        if (SelectedPerson is null)
        {
            Failure("Seleccione una persona.");
            return;
        }

        if (LoanDate.Date > DateTime.Today)
        {
            Failure("La fecha del préstamo no puede ser futura.");
            return;
        }

        try
        {
            var personId = SelectedPerson.Id;
            var hadLoan = Loans.Any(x => x.Loan.PersonId == personId);
            var loanId = _database.AddLoan(personId, LoanAmount, LoanDate, LoanNotes);
            LoanAmount = 0;
            LoanNotes = string.Empty;
            ClearErrors(nameof(LoanAmount));
            Refresh();
            SelectedLoan = Loans.FirstOrDefault(x => x.Loan.Id == loanId);
            ReportLoanStatus(loanId, hadLoan
                ? "Préstamo acumulado a la cuenta existente."
                : "Préstamo registrado.");
        }
        catch (Exception exception)
        {
            Failure(exception.Message);
        }
    }

    [RelayCommand]
    private void AddPayment()
    {
        ValidateProperty(PaymentAmount, nameof(PaymentAmount));
        if (GetErrors(nameof(PaymentAmount)).Cast<object>().Any())
        {
            Failure("El abono debe ser mayor que cero.");
            return;
        }

        if (SelectedLoan is null)
        {
            Failure("Seleccione un préstamo.");
            return;
        }

        var loanId = SelectedLoan.Loan.Id;
        var loanDate = SelectedLoan.Loan.LoanDate.Date;
        var paymentDate = PaymentDate.Date;
        if (paymentDate < loanDate)
        {
            Failure($"La fecha del abono no puede ser anterior a la fecha del préstamo ({loanDate:dd/MM/yyyy}).");
            return;
        }

        if (paymentDate > DateTime.Today)
        {
            Failure("La fecha del abono no puede ser futura.");
            return;
        }

        try
        {
            _database.AddPayment(loanId, PaymentAmount, PaymentDate, PaymentNotes);
            PaymentAmount = 0;
            PaymentNotes = string.Empty;
            ClearErrors(nameof(PaymentAmount));
            Refresh();
            ReportLoanStatus(loanId, "Abono registrado.");
        }
        catch (Exception exception)
        {
            Failure(exception.Message);
        }
    }

    [RelayCommand]
    public void Refresh()
    {
        var selectedPersonId = SelectedPerson?.Id;
        var selectedLoanId = SelectedLoan?.Loan.Id;
        var selectedPaymentId = SelectedPayment?.Id;
        var historyClientId = HistorySelectedPerson?.Id;

        _allPeople = _database.GetPeople();
        _allHistoryClients = _allPeople;
        People = new ObservableCollection<Person>(_allPeople);
        Loans = new ObservableCollection<LoanSummary>(_database.GetLoans());
        Payments = new ObservableCollection<Payment>(_database.GetPayments());

        SelectedPerson = selectedPersonId.HasValue
            ? People.FirstOrDefault(x => x.Id == selectedPersonId.Value)
            : null;
        SelectedLoan = selectedLoanId.HasValue
            ? Loans.FirstOrDefault(x => x.Loan.Id == selectedLoanId.Value)
            : null;
        SelectedPayment = selectedPaymentId.HasValue
            ? Payments.FirstOrDefault(x => x.Id == selectedPaymentId.Value)
            : null;
        ApplyPersonFilter();

        HistorySelectedPerson = historyClientId.HasValue
            ? _allHistoryClients.FirstOrDefault(x => x.Id == historyClientId.Value)
            : _allHistoryClients.FirstOrDefault();
        ApplyHistoryClientFilter();
        LoadHistoryForSelectedClient();

        var dashboard = _database.GetDashboard();
        PendingTotal = dashboard.PendingTotal;
        PaymentsThisMonth = dashboard.PaymentsThisMonth;
        ActiveLoans = dashboard.ActiveLoans;
        PaymentCount = dashboard.PaymentCount;
        LoanCount = dashboard.LoanCount;
        MissingFields = dashboard.MissingFields;
        MonthlyAnalytics = new ObservableCollection<MonthlyAmount>(_database.GetMonthlyAnalytics());
        MedianLoanAmount = _database.GetMedianLoanAmount();
        LineSeries = MonthlyAnalytics;
    }

    partial void OnPersonSearchChanged(string value) => ApplyPersonFilter();

    partial void OnHistoryClientSearchChanged(string value) => ApplyHistoryClientFilter();

    partial void OnHistorySelectedPersonChanged(Person? value) => LoadHistoryForSelectedClient();

    partial void OnSelectedLoanChanged(LoanSummary? value)
    {
        var minDate = value?.Loan.LoanDate.Date ?? DateTime.Today;
        if (minDate > DateTime.Today)
        {
            minDate = DateTime.Today;
        }

        PaymentMinDate = minDate;
        PaymentMaxDate = DateTime.Today;

        if (PaymentDate.Date < minDate)
        {
            PaymentDate = minDate;
        }
        else if (PaymentDate.Date > DateTime.Today)
        {
            PaymentDate = DateTime.Today;
        }
    }

    [RelayCommand]
    private void ClearPersonSearch() => PersonSearch = string.Empty;

    [RelayCommand]
    private void ClearHistoryClientSearch() => HistoryClientSearch = string.Empty;

    private void ApplyPersonFilter()
    {
        var rows = PersonSearchFilter.Filter(_allPeople, PersonSearch).ToList();
        FilteredPeople = new ObservableCollection<Person>(rows);
        PersonResultText = rows.Count == 1 ? "1 resultado" : $"{rows.Count} resultados";

        if (SelectedPerson is not null && !rows.Any(x => x.Id == SelectedPerson.Id))
        {
            SelectedPerson = null;
        }
    }

    private void ApplyHistoryClientFilter()
    {
        var rows = PersonSearchFilter.Filter(_allHistoryClients, HistoryClientSearch).ToList();
        HistoryClients = new ObservableCollection<Person>(rows);
        HistoryClientResultText = rows.Count == 1 ? "1 cliente" : $"{rows.Count} clientes";

        if (HistorySelectedPerson is not null && !rows.Any(x => x.Id == HistorySelectedPerson.Id))
        {
            HistorySelectedPerson = null;
        }
    }

    private void LoadHistoryForSelectedClient()
    {
        HasHistoryClient = HistorySelectedPerson is not null;
        if (HistorySelectedPerson is null)
        {
            HistorySummary = null;
            HistoryLoans = [];
            HistoryPayments = [];
            return;
        }

        var personId = HistorySelectedPerson.Id;
        HistorySummary = _database.GetPersonHistory().FirstOrDefault(x => x.PersonId == personId);
        HistoryLoans = new ObservableCollection<LoanSummary>(
            _database.GetLoans().Where(x => x.Loan.PersonId == personId));
        HistoryPayments = new ObservableCollection<Payment>(
            _database.GetPaymentHistory(personId));
    }

    public void SetStatus(string message) => StatusMessage = message;

    public void ReportLoanStatus(long loanId, string action)
    {
        var loan = Loans.FirstOrDefault(x => x.Loan.Id == loanId);
        if (loan is null)
        {
            return;
        }

        var message = loan.Balance <= 0m
            ? $"{action} El préstamo ya está pagado."
            : $"{action} Aún debe {loan.Balance:C0}.";
        Success(message);
    }

    public void RegisterModuleUsage(string module)
    {
        _moduleCounters[module] = _moduleCounters.TryGetValue(module, out var count) ? count + 1 : 1;
        ModuleUsage = new ObservableCollection<ModuleUsage>(
            _moduleCounters.OrderByDescending(x => x.Value)
                .Select(x => new ModuleUsage(x.Key, x.Value)));
    }

    [RelayCommand]
    private async Task SendChatAsync()
    {
        if (string.IsNullOrWhiteSpace(ChatInput) || ChatIsLoading)
        {
            return;
        }

        var question = ChatInput.Trim();
        ChatInput = string.Empty;
        ChatMessages.Add(new ChatMessage { Role = "user", Text = question });
        ChatIsLoading = true;
        try
        {
            var answer = await _chatService.AskAsync(question);
            ChatMessages.Add(new ChatMessage { Role = "assistant", Text = answer });
        }
        catch (Exception exception)
        {
            Failure($"No fue posible consultar el asistente: {exception.Message}");
            ChatMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Text = $"No fue posible consultar el asistente: {exception.Message}"
            });
        }
        finally
        {
            ChatIsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GenerateSummaryAsync()
    {
        if (ChatIsLoading)
        {
            return;
        }

        ChatMessages.Add(new ChatMessage { Role = "user", Text = "Resumen general" });
        ChatIsLoading = true;
        try
        {
            var answer = await _chatService.SummarizeAsync();
            ChatMessages.Add(new ChatMessage { Role = "assistant", Text = answer });
        }
        catch (Exception exception)
        {
            Failure($"No fue posible generar el resumen: {exception.Message}");
        }
        finally
        {
            ChatIsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearChat()
    {
        _chatService.ClearHistory();
        ChatMessages.Clear();
        ChatMessages.Add(new ChatMessage
        {
            Role = "assistant",
            Text = "Conversación reiniciada."
        });
    }

    [RelayCommand]
    private void ClearBanner() => BannerMessage = string.Empty;

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    [RelayCommand]
    private void TogglePin() => IsSidebarPinned = !IsSidebarPinned;

    public void SetSidebarCollapsed(bool collapsed) => IsSidebarCollapsed = collapsed;

    private void Success(string message)
    {
        StatusMessage = message;
        BannerMessage = string.Empty;
        _toastService.Show(message);
    }

    private void Failure(string message)
    {
        ErrorCount++;
        StatusMessage = message;
        BannerMessage = message;
        BannerIsError = true;
        _toastService.Show(message, true);
    }
}
