using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Runiq.Agents.Tools;
using Runiq.ExpenseDesk.Data;

namespace Runiq.ExpenseDesk.Tools;

[RuniqTool(
    name: "expense_search",
    description: "Searches employee expense records by year, month, employee, department, and category.")]
public sealed class ExpenseSearchTool : IRuniqTool<ExpenseSearchInput, ExpenseSearchOutput>
{
    private readonly ExpenseDeskDatabase database;

    public ExpenseSearchTool(ExpenseDeskDatabase database)
    {
        this.database = database;
    }

    public Task<ExpenseSearchOutput> ExecuteAsync(
        ExpenseSearchInput input,
        CancellationToken cancellationToken = default)
    {
        input ??= new ExpenseSearchInput();

        lock (database.SyncRoot)
        {
            using var command = database.Connection.CreateCommand();
            command.CommandText = BuildQuery(input, command);

            using var reader = command.ExecuteReader();
            var expenses = new List<ExpenseRow>();

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();

                expenses.Add(new ExpenseRow(
                    ExpenseId: reader.GetString(0),
                    EmployeeId: reader.GetString(1),
                    FullName: reader.GetString(2),
                    Department: reader.GetString(3),
                    Title: reader.GetString(4),
                    Location: reader.GetString(5),
                    ExpenseDate: DateOnly.ParseExact(
                        reader.GetString(6),
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture),
                    Category: reader.GetString(7),
                    Amount: reader.GetDecimal(8),
                    Currency: reader.GetString(9),
                    Description: reader.GetString(10)));
            }

            return Task.FromResult(new ExpenseSearchOutput(
                Expenses: expenses,
                TotalAmount: expenses.Sum(expense => expense.Amount),
                Currency: "TRY",
                Count: expenses.Count));
        }
    }

    private static string BuildQuery(ExpenseSearchInput input, SqliteCommand command)
    {
        var whereClauses = new List<string>();

        if (input.Year is not null)
        {
            whereClauses.Add("strftime('%Y', e.ExpenseDate) = @year");
            command.Parameters.AddWithValue(
                "@year",
                input.Year.Value.ToString("0000", CultureInfo.InvariantCulture));
        }

        var month = NormalizeMonth(input.Month);

        if (month is not null)
        {
            whereClauses.Add("strftime('%m', e.ExpenseDate) = @month");
            command.Parameters.AddWithValue(
                "@month",
                month.Value.ToString("00", CultureInfo.InvariantCulture));
        }

        AddExactTextFilter(
            command,
            whereClauses,
            "e.EmployeeId",
            "@employeeId",
            input.EmployeeId);

        AddLikeFilter(
            command,
            whereClauses,
            "p.FullName",
            "@employeeName",
            input.EmployeeName);

        AddExactTextFilter(
            command,
            whereClauses,
            "p.Department",
            "@department",
            input.Department);

        AddExactTextFilter(
            command,
            whereClauses,
            "e.Category",
            "@category",
            input.Category);

        var sql = new StringBuilder("""
        SELECT
          e.ExpenseId,
          e.EmployeeId,
          p.FullName,
          p.Department,
          p.Title,
          p.Location,
          e.ExpenseDate,
          e.Category,
          e.Amount,
          e.Currency,
          e.Description
        FROM Expenses e
        JOIN Personnel p ON p.EmployeeId = e.EmployeeId
        """);

        if (whereClauses.Count > 0)
        {
            sql.AppendLine();
            sql.Append("WHERE ");
            sql.AppendJoin(" AND ", whereClauses);
        }

        sql.AppendLine();
        sql.Append("ORDER BY e.ExpenseDate, e.ExpenseId");

        return sql.ToString();
    }

    private static void AddExactTextFilter(
        SqliteCommand command,
        List<string> whereClauses,
        string columnName,
        string parameterName,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        whereClauses.Add($"LOWER({columnName}) = LOWER({parameterName})");
        command.Parameters.AddWithValue(parameterName, value.Trim());
    }

    private static void AddLikeFilter(
        SqliteCommand command,
        List<string> whereClauses,
        string columnName,
        string parameterName,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        whereClauses.Add($"LOWER({columnName}) LIKE LOWER({parameterName}) ESCAPE '\\'");
        command.Parameters.AddWithValue(parameterName, $"%{EscapeLikeValue(value.Trim())}%");
    }

    private static string EscapeLikeValue(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }

    private static int? NormalizeMonth(int? month)
    {
        return month is >= 1 and <= 12
            ? month
            : null;
    }
}

public sealed record ExpenseSearchInput
{
    public int? Year { get; init; }

    public int? Month { get; init; }

    public string? EmployeeId { get; init; }

    public string? EmployeeName { get; init; }

    public string? Department { get; init; }

    public string? Category { get; init; }
}

public sealed record ExpenseSearchOutput(
    IReadOnlyList<ExpenseRow> Expenses,
    decimal TotalAmount,
    string Currency,
    int Count);

public sealed record ExpenseRow(
    string ExpenseId,
    string EmployeeId,
    string FullName,
    string Department,
    string Title,
    string Location,
    DateOnly ExpenseDate,
    string Category,
    decimal Amount,
    string Currency,
    string Description);
