using Microsoft.Data.Sqlite;

namespace Fiadex;

public sealed partial class FiadexDatabase
{
    private void RefreshAggregates()
    {
        using var connection = OpenConnection();
        RefreshAggregates(connection);
    }

    private static void RefreshAggregates(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM LoanAggregates;
            INSERT INTO LoanAggregates (LoanId, PersonId, PaidCents, BalanceCents)
            SELECT l.Id, l.PersonId,
                   COALESCE(SUM(p.AmountCents), 0),
                   MAX(0, l.AmountCents - COALESCE(SUM(p.AmountCents), 0))
            FROM Loans l
            LEFT JOIN Payments p ON p.LoanId = l.Id
            GROUP BY l.Id;

            DELETE FROM PersonAggregates;
            INSERT INTO PersonAggregates (PersonId, LoanTotalCents, PaidTotalCents, LoanCount, PaymentCount)
            SELECT p.Id,
                   COALESCE((SELECT SUM(l.AmountCents) FROM Loans l WHERE l.PersonId = p.Id), 0),
                   COALESCE((SELECT SUM(a.PaidCents)
                             FROM Loans l JOIN LoanAggregates a ON a.LoanId = l.Id
                             WHERE l.PersonId = p.Id), 0),
                   (SELECT COUNT(*) FROM Loans l WHERE l.PersonId = p.Id),
                   (SELECT COUNT(*) FROM Payments pay JOIN Loans l ON l.Id = pay.LoanId
                    WHERE l.PersonId = p.Id)
            FROM Persons p;

            DELETE FROM MonthlyAggregates;
            INSERT INTO MonthlyAggregates (Year, Month, LoanCents, PaymentCents)
            SELECT y, m, SUM(lc), SUM(pc)
            FROM (
                SELECT CAST(substr(LoanDate, 1, 4) AS INTEGER) AS y,
                       CAST(substr(LoanDate, 6, 2) AS INTEGER) AS m,
                       AmountCents AS lc, 0 AS pc FROM Loans
                UNION ALL
                SELECT CAST(substr(PaymentDate, 1, 4) AS INTEGER),
                       CAST(substr(PaymentDate, 6, 2) AS INTEGER),
                       0, AmountCents FROM Payments
            )
            GROUP BY y, m;
            """;
        command.ExecuteNonQuery();
    }
}
