using Microsoft.Data.Sqlite;
using System.IO;
using System.Security.Cryptography;

namespace Fiadex;

public sealed partial class FiadexDatabase : IFiadexDatabase
{
    private readonly string _connectionString;

    public FiadexDatabase()
        : this(GetDefaultDatabasePath())
    {
    }

    public FiadexDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public void Initialize()
    {
        using var connection = OpenConnection();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Persons (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Phone TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Loans (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PersonId INTEGER NOT NULL,
                AmountCents INTEGER NOT NULl CHECK (AmountCents >= 0),
                LoanDate TEXT NOT NULL,
                Notes TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (PersonId) REFERENCES Persons(Id)
            );

            CREATE TABLE IF NOT EXISTS Payments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                LoanId INTEGER NOT NULL,
                AmountCents INTEGER NOT NULL CHECK (AmountCents >= 0),
                PaymentDate TEXT NOT NULL,
                Notes TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (LoanId) REFERENCES Loans(Id)
            );

            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                PasswordSalt TEXT NOT NULL,
                FailedAttempts INTEGER NOT NULL DEFAULT 0,
                IsLocked INTEGER NOT NULL DEFAULT 0,
                LastLoginAt TEXT
            );

            CREATE INDEX IF NOT EXISTS IX_Payments_LoanId ON Payments(LoanId);
            CREATE INDEX IF NOT EXISTS IX_Loans_PersonId ON Loans(PersonId);

            CREATE TABLE IF NOT EXISTS LoanAggregates (
                LoanId INTEGER PRIMARY KEY,
                PersonId INTEGER NOT NULL,
                PaidCents INTEGER NOT NULL DEFAULT 0,
                BalanceCents INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS PersonAggregates (
                PersonId INTEGER PRIMARY KEY,
                LoanTotalCents INTEGER NOT NULL DEFAULT 0,
                PaidTotalCents INTEGER NOT NULL DEFAULT 0,
                LoanCount INTEGER NOT NULL DEFAULT 0,
                PaymentCount INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS MonthlyAggregates (
                Year INTEGER NOT NULL,
                Month INTEGER NOT NULL,
                LoanCents INTEGER NOT NULL DEFAULT 0,
                PaymentCents INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (Year, Month)
            );
            """;
        command.ExecuteNonQuery();
        ConsolidateLoanAccounts(connection);
        RefreshAggregates(connection);
    }

    // Una persona = una sola cuenta. Si en una base antigua había varios
    // préstamos para la misma persona, se fusionan en el más antiguo.
    private static void ConsolidateLoanAccounts(SqliteConnection connection)
    {
        var duplicates = new List<(long PersonId, long TargetId, long TotalCents, string FirstDate)>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT PersonId, MIN(Id), SUM(AmountCents), MIN(LoanDate)
                FROM Loans
                GROUP BY PersonId
                HAVING COUNT(*) > 1
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                duplicates.Add((
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetInt64(2),
                    reader.GetString(3)));
            }
        }

        if (duplicates.Count == 0)
        {
            return;
        }

        using var transaction = connection.BeginTransaction();
        foreach (var duplicate in duplicates)
        {
            var notes = new List<string>();
            using (var notesCommand = connection.CreateCommand())
            {
                notesCommand.Transaction = transaction;
                notesCommand.CommandText = """
                    SELECT Notes FROM Loans WHERE PersonId = $personId ORDER BY Id
                    """;
                notesCommand.Parameters.AddWithValue("$personId", duplicate.PersonId);
                using var notesReader = notesCommand.ExecuteReader();
                while (notesReader.Read())
                {
                    var note = notesReader.IsDBNull(0) ? string.Empty : notesReader.GetString(0).Trim();
                    if (!string.IsNullOrWhiteSpace(note) && !notes.Contains(note, StringComparer.Ordinal))
                    {
                        notes.Add(note);
                    }
                }
            }

            using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE Loans
                    SET AmountCents = $total,
                        LoanDate = $firstDate,
                        Notes = $notes
                    WHERE Id = $targetId
                    """;
                update.Parameters.AddWithValue("$total", duplicate.TotalCents);
                update.Parameters.AddWithValue("$firstDate", duplicate.FirstDate);
                update.Parameters.AddWithValue("$notes", string.Join(" | ", notes));
                update.Parameters.AddWithValue("$targetId", duplicate.TargetId);
                update.ExecuteNonQuery();
            }

            using (var movePayments = connection.CreateCommand())
            {
                movePayments.Transaction = transaction;
                movePayments.CommandText = """
                    UPDATE Payments SET LoanId = $targetId
                    WHERE LoanId IN (
                        SELECT Id FROM Loans
                        WHERE PersonId = $personId AND Id <> $targetId)
                    """;
                movePayments.Parameters.AddWithValue("$targetId", duplicate.TargetId);
                movePayments.Parameters.AddWithValue("$personId", duplicate.PersonId);
                movePayments.ExecuteNonQuery();
            }

            using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = """
                    DELETE FROM Loans
                    WHERE PersonId = $personId AND Id <> $targetId
                    """;
                delete.Parameters.AddWithValue("$personId", duplicate.PersonId);
                delete.Parameters.AddWithValue("$targetId", duplicate.TargetId);
                delete.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    public List<Person> GetPeople()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Phone, CreatedAt FROM Persons ORDER BY Name";

        var result = new List<Person>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Person
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Phone = reader.GetString(2),
                CreatedAt = DateTime.Parse(reader.GetString(3))
            });
        }

        return result;
    }

    public long AddPerson(string name, string phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Persons (Name, Phone, CreatedAt)
            VALUES ($name, $phone, $createdAt);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$phone", phone?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O"));
        var id = (long)command.ExecuteScalar()!;
        RefreshAggregates();
        return id;
    }

    public void UpdatePerson(long personId, string name, string phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Persons SET Name = $name, Phone = $phone
            WHERE Id = $personId
            """;
        command.Parameters.AddWithValue("$name", name.Trim());
        command.Parameters.AddWithValue("$phone", phone?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("$personId", personId);
        command.ExecuteNonQuery();
        RefreshAggregates();
    }

    public void DeletePerson(long personId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var payments = connection.CreateCommand())
        {
            payments.Transaction = transaction;
            payments.CommandText = """
                DELETE FROM Payments
                WHERE LoanId IN (SELECT Id FROM Loans WHERE PersonId = $personId)
                """;
            payments.Parameters.AddWithValue("$personId", personId);
            payments.ExecuteNonQuery();
        }

        using (var loans = connection.CreateCommand())
        {
            loans.Transaction = transaction;
            loans.CommandText = "DELETE FROM Loans WHERE PersonId = $personId";
            loans.Parameters.AddWithValue("$personId", personId);
            loans.ExecuteNonQuery();
        }

        using (var person = connection.CreateCommand())
        {
            person.Transaction = transaction;
            person.CommandText = "DELETE FROM Persons WHERE Id = $personId";
            person.Parameters.AddWithValue("$personId", personId);
            person.ExecuteNonQuery();
        }

        transaction.Commit();
        RefreshAggregates();
    }

    private static void ValidateLoanDate(DateTime loanDate)
    {
        if (loanDate.Date > DateTime.Today)
        {
            throw new ArgumentOutOfRangeException(nameof(loanDate), "La fecha del préstamo no puede ser futura.");
        }
    }

    private static void ValidatePaymentDate(DateTime paymentDate, DateTime loanDate)
    {
        if (paymentDate.Date > DateTime.Today)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentDate), "La fecha del abono no puede ser futura.");
        }

        if (paymentDate.Date < loanDate.Date)
        {
            throw new ArgumentOutOfRangeException(
                nameof(paymentDate),
                $"La fecha del abono no puede ser anterior a la fecha del préstamo ({loanDate:dd/MM/yyyy}).");
        }
    }

    private DateTime? GetEarliestPaymentDate(long loanId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MIN(PaymentDate) FROM Payments WHERE LoanId = $loanId";
        command.Parameters.AddWithValue("$loanId", loanId);
        var value = command.ExecuteScalar();
        return value is null || value is DBNull
            ? null
            : DateTime.Parse(value.ToString()!);
    }

    public long AddLoan(long personId, decimal amount, DateTime loanDate, string notes)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "El monto debe ser mayor que cero.");
        }

        ValidateLoanDate(loanDate);

        var trimmedNotes = notes?.Trim() ?? string.Empty;
        var entry = $"Préstamo adicional de {amount:C0} registrado el {loanDate:dd/MM/yyyy}";

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        long loanId = 0;
        string currentNotes = string.Empty;
        using (var lookup = connection.CreateCommand())
        {
            lookup.Transaction = transaction;
            lookup.CommandText = """
                SELECT Id, Notes FROM Loans WHERE PersonId = $personId ORDER BY Id LIMIT 1
                """;
            lookup.Parameters.AddWithValue("$personId", personId);
            using var reader = lookup.ExecuteReader();
            if (reader.Read())
            {
                loanId = reader.GetInt64(0);
                currentNotes = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            }
        }

        if (loanId == 0)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO Loans (PersonId, AmountCents, LoanDate, Notes)
                VALUES ($personId, $amountCents, $loanDate, $notes);
                SELECT last_insert_rowid();
                """;
            insert.Parameters.AddWithValue("$personId", personId);
            insert.Parameters.AddWithValue("$amountCents", ToCents(amount));
            insert.Parameters.AddWithValue("$loanDate", loanDate.ToString("O"));
            insert.Parameters.AddWithValue("$notes", trimmedNotes);
            loanId = (long)insert.ExecuteScalar()!;
        }
        else
        {
            var combined = new List<string>();
            if (!string.IsNullOrWhiteSpace(currentNotes))
            {
                combined.Add(currentNotes.Trim());
            }
            combined.Add(entry);
            if (!string.IsNullOrWhiteSpace(trimmedNotes))
            {
                combined.Add(trimmedNotes);
            }

            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE Loans
                SET AmountCents = AmountCents + $amountCents,
                    LoanDate = CASE WHEN LoanDate < $loanDate THEN LoanDate ELSE $loanDate END,
                    Notes = $notes
                WHERE Id = $loanId
                """;
            update.Parameters.AddWithValue("$amountCents", ToCents(amount));
            update.Parameters.AddWithValue("$loanDate", loanDate.ToString("O"));
            update.Parameters.AddWithValue("$notes", string.Join(" | ", combined));
            update.Parameters.AddWithValue("$loanId", loanId);
            update.ExecuteNonQuery();
        }

        transaction.Commit();
        RefreshAggregates();
        return loanId;
    }

    public void UpdateLoan(long loanId, long personId, decimal amount, DateTime loanDate, string notes)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "El monto debe ser mayor que cero.");
        }

        ValidateLoanDate(loanDate);

        var summary = GetLoanSummary(loanId);
        if (amount < summary.Paid)
        {
            throw new InvalidOperationException(
                $"El monto no puede ser menor que los abonos registrados ({summary.Paid:C0}).");
        }

        var earliestPaymentDate = GetEarliestPaymentDate(loanId);
        if (earliestPaymentDate.HasValue && loanDate.Date > earliestPaymentDate.Value.Date)
        {
            throw new InvalidOperationException(
                $"La fecha del préstamo no puede ser posterior al abono más antiguo ({earliestPaymentDate.Value:dd/MM/yyyy}).");
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Loans
            SET PersonId = $personId, AmountCents = $amountCents,
                LoanDate = $loanDate, Notes = $notes
            WHERE Id = $loanId
            """;
        command.Parameters.AddWithValue("$personId", personId);
        command.Parameters.AddWithValue("$amountCents", ToCents(amount));
        command.Parameters.AddWithValue("$loanDate", loanDate.ToString("O"));
        command.Parameters.AddWithValue("$notes", notes?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("$loanId", loanId);
        command.ExecuteNonQuery();
        RefreshAggregates();
    }

    public void DeleteLoan(long loanId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var payments = connection.CreateCommand())
        {
            payments.Transaction = transaction;
            payments.CommandText = "DELETE FROM Payments WHERE LoanId = $loanId";
            payments.Parameters.AddWithValue("$loanId", loanId);
            payments.ExecuteNonQuery();
        }

        using (var loans = connection.CreateCommand())
        {
            loans.Transaction = transaction;
            loans.CommandText = "DELETE FROM Loans WHERE Id = $loanId";
            loans.Parameters.AddWithValue("$loanId", loanId);
            loans.ExecuteNonQuery();
        }

        transaction.Commit();
        RefreshAggregates();
    }

    public long AddPayment(long loanId, decimal amount, DateTime paymentDate, string notes)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "El abono debe ser mayor que cero.");
        }

        var summary = GetLoanSummary(loanId);
        ValidatePaymentDate(paymentDate, summary.Loan.LoanDate);

        var balance = summary.Balance;
        if (amount > balance)
        {
            throw new InvalidOperationException($"El abono excede el saldo pendiente de {balance:C0}.");
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Payments (LoanId, AmountCents, PaymentDate, Notes)
            VALUES ($loanId, $amountCents, $paymentDate, $notes);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$loanId", loanId);
        command.Parameters.AddWithValue("$amountCents", ToCents(amount));
        command.Parameters.AddWithValue("$paymentDate", paymentDate.ToString("O"));
        command.Parameters.AddWithValue("$notes", notes?.Trim() ?? string.Empty);
        var id = (long)command.ExecuteScalar()!;
        RefreshAggregates();
        return id;
    }

    public void UpdatePayment(long paymentId, long loanId, decimal amount, DateTime paymentDate, string notes)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "El abono debe ser mayor que cero.");
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT AmountCents
            FROM Payments
            WHERE Id = $paymentId
            """;
        command.Parameters.AddWithValue("$paymentId", paymentId);
        var oldCents = (long)(command.ExecuteScalar() ?? throw new InvalidOperationException("Abono no encontrado."));

        var summary = GetLoanSummary(loanId);
        ValidatePaymentDate(paymentDate, summary.Loan.LoanDate);

        var paidWithoutCurrent = summary.Paid - FromCents(oldCents);
        if (amount > summary.Loan.Amount - paidWithoutCurrent)
        {
            throw new InvalidOperationException(
                $"El abono excede el saldo disponible de {summary.Loan.Amount - paidWithoutCurrent:C0}.");
        }

        using var update = connection.CreateCommand();
        update.CommandText = """
            UPDATE Payments
            SET LoanId = $loanId, AmountCents = $amountCents,
                PaymentDate = $paymentDate, Notes = $notes
            WHERE Id = $paymentId
            """;
        update.Parameters.AddWithValue("$loanId", loanId);
        update.Parameters.AddWithValue("$amountCents", ToCents(amount));
        update.Parameters.AddWithValue("$paymentDate", paymentDate.ToString("O"));
        update.Parameters.AddWithValue("$notes", notes?.Trim() ?? string.Empty);
        update.Parameters.AddWithValue("$paymentId", paymentId);
        update.ExecuteNonQuery();
        RefreshAggregates();
    }

    public void DeletePayment(long paymentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Payments WHERE Id = $paymentId";
        command.Parameters.AddWithValue("$paymentId", paymentId);
        command.ExecuteNonQuery();
        RefreshAggregates();
    }

    public List<LoanSummary> GetLoans()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT l.Id, l.PersonId, p.Name, l.AmountCents, l.LoanDate, l.Notes,
                   COALESCE(agg.PaidCents, 0)
            FROM Loans l
            JOIN Persons p ON p.Id = l.PersonId
            LEFT JOIN LoanAggregates agg ON agg.LoanId = l.Id
            ORDER BY l.LoanDate DESC
            """;

        var result = new List<LoanSummary>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var loan = new Loan
            {
                Id = reader.GetInt64(0),
                PersonId = reader.GetInt64(1),
                PersonName = reader.GetString(2),
                Amount = FromCents(reader.GetInt64(3)),
                LoanDate = DateTime.Parse(reader.GetString(4)),
                Notes = reader.GetString(5)
            };
            result.Add(new LoanSummary
            {
                Loan = loan,
                Paid = FromCents(reader.GetInt64(6))
            });
        }

        return result;
    }

    public LoanSummary GetLoanSummary(long loanId)
    {
        return GetLoans().Single(x => x.Loan.Id == loanId);
    }

    public List<Payment> GetPayments(long? loanId = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, LoanId, AmountCents, PaymentDate, Notes
            FROM Payments
            """ + (loanId.HasValue ? " WHERE LoanId = $loanId" : string.Empty) +
            " ORDER BY PaymentDate DESC";
        if (loanId.HasValue)
        {
            command.Parameters.AddWithValue("$loanId", loanId.Value);
        }

        var result = new List<Payment>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Payment
            {
                Id = reader.GetInt64(0),
                LoanId = reader.GetInt64(1),
                Amount = FromCents(reader.GetInt64(2)),
                PaymentDate = DateTime.Parse(reader.GetString(3)),
                Notes = reader.GetString(4)
            });
        }

        return result;
    }

    public List<PersonHistory> GetPersonHistory()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.Id,
                   p.Name,
                   COALESCE(agg.LoanTotalCents, 0),
                   COALESCE(agg.PaidTotalCents, 0),
                   COALESCE(agg.LoanCount, 0),
                   COALESCE(agg.PaymentCount, 0)
            FROM Persons p
            LEFT JOIN PersonAggregates agg ON agg.PersonId = p.Id
            ORDER BY p.Name
            """;

        var result = new List<PersonHistory>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var loanTotal = FromCents(reader.GetInt64(2));
            var paidTotal = FromCents(reader.GetInt64(3));
            result.Add(new PersonHistory(
                reader.GetInt64(0),
                reader.GetString(1),
                loanTotal,
                paidTotal,
                LoanCalculator.CalculateBalance(loanTotal, paidTotal),
                reader.GetInt32(4),
                reader.GetInt32(5)));
        }

        return result;
    }

    public List<Payment> GetPaymentHistory(long personId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT pay.Id, pay.LoanId, pay.AmountCents, pay.PaymentDate, pay.Notes
            FROM Payments pay
            JOIN Loans l ON l.Id = pay.LoanId
            WHERE l.PersonId = $personId
            ORDER BY pay.PaymentDate DESC
            """;
        command.Parameters.AddWithValue("$personId", personId);

        var result = new List<Payment>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Payment
            {
                Id = reader.GetInt64(0),
                LoanId = reader.GetInt64(1),
                Amount = FromCents(reader.GetInt64(2)),
                PaymentDate = DateTime.Parse(reader.GetString(3)),
                Notes = reader.GetString(4)
            });
        }

        return result;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static long ToCents(decimal amount)
    {
        return decimal.ToInt64(decimal.Round(amount * 100, 0, MidpointRounding.AwayFromZero));
    }

    private static decimal FromCents(long cents) => cents / 100m;

    private static string GetDefaultDatabasePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Fiadex", "fiadex.db");
    }
}
