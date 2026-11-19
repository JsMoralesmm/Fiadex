using Microsoft.Data.Sqlite;
using System.Net;
using System.Text;

namespace Fiadex.Tests;

public class LoanCalculatorTests
{
    [Theory]
    [InlineData(100000, 25000, 75000)]
    [InlineData(50000, 50000, 0)]
    [InlineData(50000, 60000, 0)]
    [InlineData(50000, 0, 50000)]
    public void CalculateBalance_ReturnsExpectedBalance(decimal principal, decimal payments, decimal expected)
    {
        Assert.Equal(expected, LoanCalculator.CalculateBalance(principal, payments));
    }

    [Fact]
    public void CalculateBalance_RejectsNegativePrincipal()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoanCalculator.CalculateBalance(-1, 0));
    }
}

public class ChatServiceTests
{
    [Fact]
    public async Task AskAsync_UsesOnlyReadOnlyDatabaseFacts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            database.Initialize();
            var personId = database.AddPerson("María", "");
            var loanId = database.AddLoan(personId, 80000, DateTime.Today, "");
            database.AddPayment(loanId, 30000, DateTime.Today, "");

            var handler = new StubOllamaHandler();
            using var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:11434/")
            };
            var chat = new ChatService(database, httpClient);

            var answer = await chat.AskAsync("¿Cuál es el saldo de María?");

            Assert.Equal("Respuesta de prueba", answer);
            Assert.Contains("saldo pendiente", chat.BuildReadOnlyFacts("María"));
            Assert.Contains("Persona encontrada: María", chat.BuildReadOnlyFacts("María"));
            Assert.Equal("api/chat", handler.RequestPath);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private sealed class StubOllamaHandler : System.Net.Http.HttpMessageHandler
    {
        public string? RequestPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestPath = request.RequestUri?.PathAndQuery.TrimStart('/');
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"message\":{\"content\":\"Respuesta de prueba\"}}",
                    Encoding.UTF8,
                    "application/json")
            };
            return Task.FromResult(response);
        }
    }
}

public class PasswordSecurityTests
{
    [Fact]
    public void HashPassword_VerifiesCorrectPassword()
    {
        var (hash, salt) = PasswordSecurity.HashPassword("FiadexSeguro1!");

        Assert.True(PasswordSecurity.VerifyPassword("FiadexSeguro1!", salt, hash));
        Assert.False(PasswordSecurity.VerifyPassword("OtraClave1!", salt, hash));
    }

    [Fact]
    public void ValidatePassword_RejectsWeakPassword()
    {
        Assert.Throws<ArgumentException>(() =>
            PasswordSecurity.ValidatePassword("debil"));
    }

    [Fact]
    public void Database_LocksAfterFiveFailedAttempts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
        try
        {
            var database = new FiadexDatabase(path);
            database.Initialize();
            database.CreateAdmin("admin", "FiadexSeguro1!");

            for (var attempt = 0; attempt < 5; attempt++)
            {
                Assert.False(database.Authenticate("admin", "Incorrecta1!"));
            }

            Assert.Throws<InvalidOperationException>(() =>
                database.Authenticate("admin", "FiadexSeguro1!"));

            database.UnlockAccount("admin");
            Assert.True(database.Authenticate("admin", "FiadexSeguro1!"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    public class RecordCrudTests
    {
        [Fact]
        public void MonthlyAnalytics_ReturnsTwelvePointsAndMedian()
        {
            var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
            try
            {
                var database = new FiadexDatabase(path);
                database.Initialize();
                var personId = database.AddPerson("Analítica", "");
                var loanId = database.AddLoan(personId, 75000, DateTime.Today, "");
                database.AddPayment(loanId, 25000, DateTime.Today, "");

                var analytics = database.GetMonthlyAnalytics();
                Assert.Equal(12, analytics.Count);
                Assert.Contains(analytics, point => point.Payments == 25000m);
                Assert.Equal(75000m, database.GetMedianLoanAmount());
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                File.Delete(path);
            }
        }

        [Fact]
        public void PersonHistory_AccumulatesLoansAndSeparatesPayments()
        {
            var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
            try
            {
                var database = new FiadexDatabase(path);
                database.Initialize();
                var personId = database.AddPerson("María", "");
                var firstLoan = database.AddLoan(personId, 50000, DateTime.Today, "");
                var secondLoan = database.AddLoan(personId, 50000, DateTime.Today, "");
                Assert.Equal(firstLoan, secondLoan);
                database.AddPayment(firstLoan, 10000, DateTime.Today, "");
                database.AddPayment(secondLoan, 15000, DateTime.Today, "");

                var history = Assert.Single(database.GetPersonHistory());
                Assert.Equal(100000m, history.LoanTotal);
                Assert.Equal(25000m, history.PaidTotal);
                Assert.Equal(75000m, history.Balance);
                Assert.Equal(1, history.LoanCount);
                Assert.Equal(2, history.PaymentCount);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                File.Delete(path);
            }
        }

        [Fact]
        public void UpdateAndDeletePayment_RecalculatesBalance()
        {
            var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
            try
            {
                var database = new FiadexDatabase(path);
                database.Initialize();
                var personId = database.AddPerson("Juan", "3001234567");
                var loanId = database.AddLoan(personId, 100000, DateTime.Today, "Prueba");
                var paymentId = database.AddPayment(loanId, 25000, DateTime.Today, "Inicial");

                database.UpdatePayment(paymentId, loanId, 30000, DateTime.Today, "Corregido");
                Assert.Equal(30000m, database.GetLoanSummary(loanId).Paid);
                Assert.Equal(70000m, database.GetLoanSummary(loanId).Balance);

                database.DeletePayment(paymentId);
                Assert.Equal(0m, database.GetLoanSummary(loanId).Paid);
                Assert.Equal(100000m, database.GetLoanSummary(loanId).Balance);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                File.Delete(path);
            }
        }

        [Fact]
        public void UpdateLoan_RejectsAmountBelowRegisteredPayments()
        {
            var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
            try
            {
                var database = new FiadexDatabase(path);
                database.Initialize();
                var personId = database.AddPerson("Ana", "");
                var loanId = database.AddLoan(personId, 100000, DateTime.Today, "");
                database.AddPayment(loanId, 40000, DateTime.Today, "");

                Assert.Throws<InvalidOperationException>(() =>
                    database.UpdateLoan(loanId, personId, 30000, DateTime.Today, ""));
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                File.Delete(path);
            }
        }

        [Fact]
        public void DeletePerson_WithLoan_RemovesPersonAndHistory()
        {
            var path = Path.Combine(Path.GetTempPath(), $"fiadex-{Guid.NewGuid():N}.db");
            try
            {
                var database = new FiadexDatabase(path);
                database.Initialize();
                var personId = database.AddPerson("Carlos", "");
                var loanId = database.AddLoan(personId, 50000, DateTime.Today, "");
                database.AddPayment(loanId, 10000, DateTime.Today, "");

                database.DeletePerson(personId);

                Assert.DoesNotContain(database.GetPeople(), person => person.Id == personId);
                Assert.DoesNotContain(database.GetLoans(), loan => loan.Loan.Id == loanId);
                Assert.DoesNotContain(database.GetPayments(), payment => payment.LoanId == loanId);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                File.Delete(path);
            }
        }
    }
}
