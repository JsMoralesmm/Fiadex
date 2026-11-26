using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Fiadex.Tests;

public sealed class DatabaseTests : IDisposable
{
    private readonly string _path;
    private readonly FiadexDatabase _database;

    public DatabaseTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"fiadex-tests-{Guid.NewGuid():N}.db");
        _database = new FiadexDatabase(_path);
        _database.Initialize();
    }

    [Fact]
    public void AddPayment_UpdatesBalanceAndRejectsAmountsAboveBalance()
    {
        var personId = _database.AddPerson("Ana Pérez", "3001234567");
        var loanId = _database.AddLoan(personId, 100m, DateTime.Today.AddDays(-5), "Préstamo de prueba");
        _database.AddPayment(loanId, 40m, DateTime.Today.AddDays(-1), "Abono parcial");

        var summary = _database.GetLoanSummary(loanId);
        Assert.Equal(60m, summary.Balance);

        Assert.Throws<InvalidOperationException>(() =>
            _database.AddPayment(loanId, 100m, DateTime.Today, "Abono excedido"));
        Assert.Equal(60m, _database.GetLoanSummary(loanId).Balance);
    }

    [Fact]
    public void GetLoanSummary_ReturnsOnlyRequestedLoan()
    {
        var personId = _database.AddPerson("Luis García", "3009876543");
        var first = _database.AddLoan(personId, 50m, DateTime.Today.AddDays(-2), "Uno");
        var second = _database.AddLoan(personId, 75m, DateTime.Today.AddDays(-1), "Dos");

        Assert.Equal(first, _database.GetLoanSummary(first).Loan.Id);
        Assert.Equal(second, _database.GetLoanSummary(second).Loan.Id);
        Assert.Throws<InvalidOperationException>(() => _database.GetLoanSummary(99999));
    }

    [Fact]
    public void LockedAccount_RequiresCorrectPasswordToUnlock()
    {
        const string password = "ClaveDePrueba123!";
        _database.CreateAdmin("admin", password);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.False(_database.Authenticate("admin", "ClaveIncorrecta1."));
        }

        Assert.True(_database.IsAccountLocked("admin"));
        Assert.False(_database.UnlockAccount("admin", "ClaveIncorrecta1."));
        Assert.True(_database.IsAccountLocked("admin"));
        Assert.True(_database.UnlockAccount("admin", password));
        Assert.False(_database.IsAccountLocked("admin"));
        Assert.True(_database.Authenticate("admin", password));
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite mantiene las conexiones en un pool: hay que
        // liberarlas antes de borrar, o el archivo sigue bloqueado en Windows.
        SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var file = _path + suffix;
            for (var attempt = 0; attempt < 5 && File.Exists(file); attempt++)
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // Un archivo temporal que no se pudo borrar no afecta al
                    // resultado de las pruebas; Windows lo liberará al cerrar.
                    Thread.Sleep(100);
                }
            }
        }
    }
}
