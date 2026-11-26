using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Fiadex;

public sealed partial class FiadexDatabase
{
    public DashboardSummary GetDashboard(DateTime? month = null)
    {
        var now = month ?? DateTime.Now;
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE((SELECT SUM(BalanceCents) FROM LoanAggregates), 0),
                   COALESCE((SELECT SUM(BalanceCents) FROM LoanAggregates WHERE BalanceCents > 0), 0),
                   COALESCE((SELECT COUNT(*) FROM LoanAggregates WHERE BalanceCents > 0), 0),
                   COALESCE((SELECT SUM(PaymentCents) FROM MonthlyAggregates
                             WHERE Year = $year AND Month = $month), 0),
                   COALESCE((SELECT SUM(PaymentCount) FROM PersonAggregates), 0),
                   COALESCE((SELECT SUM(LoanCount) FROM PersonAggregates), 0);
            """;
        command.Parameters.AddWithValue("$year", now.Year);
        command.Parameters.AddWithValue("$month", now.Month);

        using var reader = command.ExecuteReader();
        reader.Read();
        return new DashboardSummary(
            FromCents(reader.GetInt64(0)),
            FromCents(reader.GetInt64(3)),
            reader.GetInt32(2),
            reader.GetInt32(4),
            reader.GetInt32(5),
            GetMissingFieldsCount());
    }

    public List<MonthlyAmount> GetMonthlyAnalytics(int monthCount = 12)
    {
        var byMonth = new Dictionary<(int Year, int Month), (decimal Loans, decimal Payments)>();
        using (var connection = OpenConnection())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT Year, Month, LoanCents, PaymentCents FROM MonthlyAggregates
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                byMonth[(reader.GetInt32(0), reader.GetInt32(1))] =
                    (FromCents(reader.GetInt64(2)), FromCents(reader.GetInt64(3)));
            }
        }

        var today = DateTime.Today;
        var spanish = CultureInfo.GetCultureInfo("es-ES");
        var points = new List<(string Label, decimal Loans, decimal Payments)>();
        // De más antiguo a más reciente para que barras y línea se lean en orden cronológico.
        for (var offset = monthCount - 1; offset >= 0; offset--)
        {
            var month = new DateTime(today.Year, today.Month, 1).AddMonths(-offset);
            byMonth.TryGetValue((month.Year, month.Month), out var values);
            var label = month.ToString("MMM", spanish);
            label = char.ToUpperInvariant(label[0]) + label[1..];
            points.Add((label, values.Loans, values.Payments));
        }

        var max = points.Count == 0 ? 0 : points.Max(x => x.Payments);
        return points.Select(x => new MonthlyAmount(
            x.Label,
            x.Loans,
            x.Payments,
            max == 0 ? 0 : (double)(x.Payments / max * 110m))).ToList();
    }

    public decimal GetMedianLoanAmount()
    {
        var amounts = new List<decimal>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT AmountCents FROM Loans ORDER BY AmountCents";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            amounts.Add(FromCents(reader.GetInt64(0)));
        }

        if (amounts.Count == 0)
        {
            return 0;
        }

        var middle = amounts.Count / 2;
        return amounts.Count % 2 == 0
            ? (amounts[middle - 1] + amounts[middle]) / 2m
            : amounts[middle];
    }

    public int GetMissingFieldsCount()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT (SELECT COUNT(*) FROM Persons WHERE TRIM(Phone) = '')
                 + (SELECT COUNT(*) FROM Loans WHERE TRIM(Notes) = '')
                 + (SELECT COUNT(*) FROM Payments WHERE TRIM(Notes) = '')
            """;
        return (int)(long)command.ExecuteScalar()!;
    }
}
