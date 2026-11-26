using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Fiadex.Tests;

public class SessionServiceTests
{
    [Fact]
    public void RegisterActivity_RenewsTheEightHourWindow()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var session = new SessionService(() => now);
        session.Start();

        for (var activity = 0; activity < 40; activity++)
        {
            now = now.AddMinutes(14);
            session.RegisterActivity();
        }

        Assert.True(session.IsActive());

        now = now.AddMinutes(16);
        Assert.False(session.IsActive());
    }

    [Fact]
    public void IsActive_ExpiresAfterFifteenMinutesOfInactivity()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var session = new SessionService(() => now);
        session.Start();

        now = now.AddMinutes(16);
        Assert.False(session.IsActive());
    }
}

public class AggregateStorageTests
{
    [Fact]
    public void MutationsKeepAggregateTablesAndAnalyticsInSync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            database.Initialize();
            var personId = database.AddPerson("María", "3001234567");
            var loanId = database.AddLoan(personId, 100000, DateTime.Today, "Préstamo");
            database.AddPayment(loanId, 30000, DateTime.Today, "Abono");

            var dashboard = database.GetDashboard();
            Assert.Equal(70000m, dashboard.PendingTotal);
            Assert.Equal(30000m, dashboard.PaymentsThisMonth);
            Assert.Equal(1, dashboard.ActiveLoans);
            Assert.Equal(1, dashboard.PaymentCount);
            Assert.Equal(1, dashboard.LoanCount);
            Assert.Equal(0, dashboard.MissingFields);
            Assert.Contains(database.GetMonthlyAnalytics(), point => point.Payments == 30000m);

            using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM LoanAggregates";
                Assert.Equal(1L, (long)command.ExecuteScalar()!);
                command.CommandText = "SELECT BalanceCents FROM LoanAggregates WHERE LoanId = $id";
                command.Parameters.AddWithValue("$id", loanId);
                Assert.Equal(7_000_000L, (long)command.ExecuteScalar()!);
            }

            database.AddPerson("Sin teléfono", "");
            Assert.Equal(1, database.GetMissingFieldsCount());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}

public class ChatToolTests
{
    [Fact]
    public async Task AskAsync_ExecutesReadOnlyToolsAndUsesTheirResults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            database.Initialize();
            var personId = database.AddPerson("María", "");
            var loanId = database.AddLoan(personId, 80000, DateTime.Today, "");
            database.AddPayment(loanId, 30000, DateTime.Today, "");

            var handler = new QueueHandler(
                "{\"message\":{\"role\":\"assistant\",\"content\":\"\",\"tool_calls\":[{\"function\":{\"name\":\"get_dashboard\",\"arguments\":{}}}]}}",
                "{\"message\":{\"role\":\"assistant\",\"content\":\"El saldo pendiente es 50000.\"}}");
            using var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:11434/")
            };
            var chat = new ChatService(database, httpClient);

            var answer = await chat.AskAsync("¿Cuál es el saldo?");

            Assert.Equal("El saldo pendiente es 50000.", answer);
            Assert.Equal(2, handler.Payloads.Count);
            Assert.Contains("\"tools\"", handler.Payloads[0]);
            Assert.Contains("\"role\":\"tool\"", handler.Payloads[1]);
            Assert.Contains("saldoPendiente", handler.Payloads[1]);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AskAsync_RejectsNumbersNotPresentInLocalData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            database.Initialize();
            var personId = database.AddPerson("María", "");
            database.AddLoan(personId, 80000, DateTime.Today, "");

            var handler = new QueueHandler(
                "{\"message\":{\"role\":\"assistant\",\"content\":\"El saldo es 999999.\"}}");
            using var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:11434/")
            };
            var chat = new ChatService(database, httpClient);

            var answer = await chat.AskAsync("¿Cuál es el saldo?");

            Assert.Equal("No pude verificar esas cifras con los datos locales.", answer);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AskAsync_WhenOllamaIsUnreachable_ExplainsHowToStartIt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            database.Initialize();
            using var httpClient = new HttpClient(new ThrowingHandler())
            {
                BaseAddress = new Uri("http://localhost:11434/")
            };
            var chat = new ChatService(database, httpClient);

            var answer = await chat.AskAsync("¿Cuál es el saldo?");

            Assert.Contains("no está disponible", answer);
            Assert.Contains("ollama serve", answer);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AskAsync_WhenModelIsMissing_ExplainsHowToPullIt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            database.Initialize();
            using var httpClient = new HttpClient(new StatusHandler(HttpStatusCode.NotFound))
            {
                BaseAddress = new Uri("http://localhost:11434/")
            };
            var chat = new ChatService(database, httpClient, model: "qwen2.5:3b");

            var answer = await chat.AskAsync("¿Cuál es el saldo?");

            Assert.Contains("no está instalado", answer);
            Assert.Contains("ollama pull qwen2.5:3b", answer);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("No se pudo conectar.");
    }

    private sealed class StatusHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public StatusHandler(HttpStatusCode statusCode) => _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent("model not found", Encoding.UTF8, "application/json")
            });
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<string> _responses;

        public QueueHandler(params string[] responses) => _responses = new Queue<string>(responses);

        public List<string> Payloads { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Payloads.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responses.Dequeue(), Encoding.UTF8, "application/json")
            };
        }
    }
}

public class InlineValidationTests
{
    [Fact]
    public void Commands_ValidateFieldsBeforeSaving()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            var viewModel = new MainViewModel(database, new ToastService());

            viewModel.PersonName = string.Empty;
            viewModel.AddPersonCommand.Execute(null);
            Assert.NotEmpty(viewModel.GetErrors(nameof(MainViewModel.PersonName)));
            Assert.Empty(database.GetPeople());

            viewModel.PersonName = "Ana";
            viewModel.PersonPhone = "3001234567";
            viewModel.AddPersonCommand.Execute(null);
            Assert.Single(database.GetPeople());

            viewModel.LoanAmount = 0;
            viewModel.AddLoanCommand.Execute(null);
            Assert.NotEmpty(viewModel.GetErrors(nameof(MainViewModel.LoanAmount)));
            Assert.Empty(database.GetLoans());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}

public class DateConsistencyTests
{
    [Fact]
    public void AddLoan_RejectsFutureDates()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            database.Initialize();
            var personId = database.AddPerson("María", "");

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                database.AddLoan(personId, 50000, DateTime.Today.AddDays(1), ""));
            Assert.Empty(database.GetLoans());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public void AddPayment_RejectsDatesBeforeTheLoanAndFutureDates()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            database.Initialize();
            var personId = database.AddPerson("María", "");
            var loanId = database.AddLoan(personId, 100000, DateTime.Today, "");

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                database.AddPayment(loanId, 10000, DateTime.Today.AddDays(-3), ""));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                database.AddPayment(loanId, 10000, DateTime.Today.AddDays(1), ""));
            Assert.Empty(database.GetPayments());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public void UpdateLoan_RejectsDatesAfterTheOldestPayment()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            database.Initialize();
            var personId = database.AddPerson("María", "");
            var loanId = database.AddLoan(personId, 100000, DateTime.Today.AddDays(-5), "");
            database.AddPayment(loanId, 10000, DateTime.Today.AddDays(-3), "");

            Assert.Throws<InvalidOperationException>(() =>
                database.UpdateLoan(loanId, personId, 100000, DateTime.Today.AddDays(-1), ""));
            Assert.Equal(DateTime.Today.AddDays(-5).Date,
                database.GetLoanSummary(loanId).Loan.LoanDate.Date);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public void UpdatePayment_RejectsDatesBeforeTheLoan()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            database.Initialize();
            var personId = database.AddPerson("María", "");
            var loanId = database.AddLoan(personId, 100000, DateTime.Today, "");
            var paymentId = database.AddPayment(loanId, 10000, DateTime.Today, "");

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                database.UpdatePayment(paymentId, loanId, 10000, DateTime.Today.AddDays(-1), ""));
            Assert.Equal(DateTime.Today.Date, database.GetPayments().Single().PaymentDate.Date);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}

public class PaymentFeedbackTests
{
    [Fact]
    public void AddPayment_ReportsRemainingBalanceAndPaidOffStatus()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            var viewModel = new MainViewModel(database, new ToastService());

            viewModel.PersonName = "María";
            viewModel.PersonPhone = "3001234567";
            viewModel.AddPersonCommand.Execute(null);
            viewModel.SelectedPerson = viewModel.People.Single();
            viewModel.LoanAmount = 100000;
            viewModel.AddLoanCommand.Execute(null);
            viewModel.SelectedLoan = viewModel.Loans.Single();

            viewModel.PaymentAmount = 40000;
            viewModel.AddPaymentCommand.Execute(null);
            Assert.Contains("Aún debe", viewModel.StatusMessage);
            Assert.Contains("60", viewModel.StatusMessage);

            viewModel.PaymentAmount = 60000;
            viewModel.AddPaymentCommand.Execute(null);
            Assert.Contains("ya está pagado", viewModel.StatusMessage);
            Assert.Equal(0m, viewModel.SelectedLoan.Balance);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}

public class SingleAccountTests
{
    [Fact]
    public void AddLoan_AccumulatesAmountIntoTheExistingAccount()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            database.Initialize();
            var personId = database.AddPerson("María", "300111");
            var firstId = database.AddLoan(personId, 20000, DateTime.Today.AddDays(-2), "Préstamo 1");
            database.AddPayment(firstId, 5000, DateTime.Today.AddDays(-1), "Abono 1");

            var secondId = database.AddLoan(personId, 10000, DateTime.Today, "Préstamo 2");

            Assert.Equal(firstId, secondId);
            var loan = Assert.Single(database.GetLoans());
            Assert.Equal(30000m, loan.Loan.Amount);
            Assert.Equal(5000m, loan.Paid);
            Assert.Equal(25000m, loan.Balance);
            Assert.Contains("Préstamo 1", loan.Loan.Notes, StringComparison.Ordinal);
            Assert.Contains("Préstamo adicional", loan.Loan.Notes, StringComparison.Ordinal);
            Assert.Equal(DateTime.Today.AddDays(-2).Date, loan.Loan.LoanDate.Date);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public void AddLoan_ViewModelKeepsOneLoanAndReportsAccumulation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            var viewModel = new MainViewModel(database, new ToastService());

            viewModel.PersonName = "María";
            viewModel.PersonPhone = "3001234567";
            viewModel.AddPersonCommand.Execute(null);
            viewModel.SelectedPerson = viewModel.People.Single();

            viewModel.LoanAmount = 100000;
            viewModel.AddLoanCommand.Execute(null);
            viewModel.LoanAmount = 50000;
            viewModel.AddLoanCommand.Execute(null);

            var loan = Assert.Single(viewModel.Loans);
            Assert.Equal(150000m, loan.Loan.Amount);
            Assert.Contains("acumulado", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("150", viewModel.StatusMessage, StringComparison.Ordinal);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public void Initialize_ConsolidatesDuplicateLoansFromAnOlderDatabase()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var bootstrap = new FiadexDatabase(path);
            bootstrap.Initialize();
            var personId = bootstrap.AddPerson("María", "300111");

            using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                connection.Open();
                using var insertLoan = connection.CreateCommand();
                insertLoan.CommandText = """
                    INSERT INTO Loans (PersonId, AmountCents, LoanDate, Notes)
                    VALUES ($personId, $cents, $date, $notes);
                    SELECT last_insert_rowid();
                    """;
                insertLoan.Parameters.AddWithValue("$personId", personId);
                insertLoan.Parameters.AddWithValue("$cents", 1_000_000L);
                insertLoan.Parameters.AddWithValue("$date", DateTime.Today.AddDays(-3).ToString("O"));
                insertLoan.Parameters.AddWithValue("$notes", "Préstamo antiguo 1");
                var firstId = (long)insertLoan.ExecuteScalar()!;

                insertLoan.Parameters["$cents"].Value = 500_000L;
                insertLoan.Parameters["$date"].Value = DateTime.Today.AddDays(-2).ToString("O");
                insertLoan.Parameters["$notes"].Value = "Préstamo antiguo 2";
                var secondId = (long)insertLoan.ExecuteScalar()!;

                using var insertPayment = connection.CreateCommand();
                insertPayment.CommandText = """
                    INSERT INTO Payments (LoanId, AmountCents, PaymentDate, Notes)
                    VALUES ($loanId, $cents, $date, $notes)
                    """;
                insertPayment.Parameters.AddWithValue("$loanId", secondId);
                insertPayment.Parameters.AddWithValue("$cents", 200_000L);
                insertPayment.Parameters.AddWithValue("$date", DateTime.Today.AddDays(-1).ToString("O"));
                insertPayment.Parameters.AddWithValue("$notes", "Abono antiguo");
                insertPayment.ExecuteNonQuery();
            }

            var database = new FiadexDatabase(path);
            database.Initialize();

            var loan = Assert.Single(database.GetLoans());
            Assert.Equal(15000m, loan.Loan.Amount);
            Assert.Equal(2000m, loan.Paid);
            Assert.Equal(13000m, loan.Balance);
            Assert.Contains("Préstamo antiguo 1", loan.Loan.Notes, StringComparison.Ordinal);
            Assert.Contains("Préstamo antiguo 2", loan.Loan.Notes, StringComparison.Ordinal);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}

public class PersonSearchTests
{
    [Fact]
    public void PersonSearch_FiltersByNameAndPhoneAndCanBeCleared()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            database.Initialize();
            database.AddPerson("María López", "300111");
            database.AddPerson("Juan Pérez", "300222");

            var viewModel = new MainViewModel(database, new ToastService());
            Assert.Equal(2, viewModel.FilteredPeople.Count);

            viewModel.PersonSearch = "juan";
            Assert.Single(viewModel.FilteredPeople);
            Assert.Equal("Juan Pérez", viewModel.FilteredPeople[0].Name);

            viewModel.PersonSearch = "300111";
            Assert.Single(viewModel.FilteredPeople);
            Assert.Equal("María López", viewModel.FilteredPeople[0].Name);

            viewModel.ClearPersonSearchCommand.Execute(null);
            Assert.Equal(2, viewModel.FilteredPeople.Count);
            Assert.Equal(string.Empty, viewModel.PersonSearch);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}

public class ClientHistoryTests
{
    [Fact]
    public void History_ShowsOnlyTheSelectedClientsLoansAndPayments()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            database.Initialize();
            var mariaId = database.AddPerson("María", "300111");
            var juanId = database.AddPerson("Juan", "300222");

            var mariaLoan = database.AddLoan(mariaId, 100000, DateTime.Today.AddDays(-2), "Préstamo María");
            database.AddPayment(mariaLoan, 20000, DateTime.Today.AddDays(-1), "Abono María");
            var juanLoan = database.AddLoan(juanId, 50000, DateTime.Today.AddDays(-2), "Préstamo Juan");
            database.AddPayment(juanLoan, 10000, DateTime.Today.AddDays(-1), "Abono Juan");

            var viewModel = new MainViewModel(database, new ToastService());
            Assert.Equal(2, viewModel.HistoryClients.Count);

            viewModel.HistorySelectedPerson = viewModel.HistoryClients.First(x => x.Id == mariaId);
            Assert.NotNull(viewModel.HistorySummary);
            Assert.Equal(100000m, viewModel.HistorySummary.LoanTotal);
            Assert.Equal(20000m, viewModel.HistorySummary.PaidTotal);
            Assert.Equal(80000m, viewModel.HistorySummary.Balance);
            Assert.Single(viewModel.HistoryLoans);
            Assert.Equal("Préstamo María", viewModel.HistoryLoans[0].Loan.Notes);
            Assert.Single(viewModel.HistoryPayments);
            Assert.Equal("Abono María", viewModel.HistoryPayments[0].Notes);

            viewModel.HistorySelectedPerson = viewModel.HistoryClients.First(x => x.Id == juanId);
            Assert.Equal(50000m, viewModel.HistorySummary.LoanTotal);
            Assert.Single(viewModel.HistoryLoans);
            Assert.Equal("Préstamo Juan", viewModel.HistoryLoans[0].Loan.Notes);
            Assert.Single(viewModel.HistoryPayments);
            Assert.Equal("Abono Juan", viewModel.HistoryPayments[0].Notes);

            viewModel.HistoryClientSearch = "maría";
            Assert.Single(viewModel.HistoryClients);
            Assert.Null(viewModel.HistorySelectedPerson);
            Assert.Empty(viewModel.HistoryLoans);
            Assert.Empty(viewModel.HistoryPayments);

            viewModel.ClearHistoryClientSearchCommand.Execute(null);
            Assert.Equal(2, viewModel.HistoryClients.Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
