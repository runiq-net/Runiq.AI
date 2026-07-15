using Microsoft.Data.Sqlite;

namespace Runiq.AI.ExpenseDesk.Data;

public sealed class ExpenseDeskDatabase : IDisposable
{
    public ExpenseDeskDatabase()
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        Connection.Open();

        CreateSchema();
        Seed();
    }

    public SqliteConnection Connection { get; }

    public object SyncRoot { get; } = new();

    public void Dispose()
    {
        Connection.Dispose();
    }

    private void CreateSchema()
    {
        using var command = Connection.CreateCommand();
        command.CommandText = """
        CREATE TABLE Personnel (
            EmployeeId TEXT PRIMARY KEY,
            FullName TEXT NOT NULL,
            Department TEXT NOT NULL,
            Title TEXT NOT NULL,
            Location TEXT NOT NULL
        );

        CREATE TABLE Expenses (
            ExpenseId TEXT PRIMARY KEY,
            EmployeeId TEXT NOT NULL,
            ExpenseDate TEXT NOT NULL,
            Category TEXT NOT NULL,
            Amount REAL NOT NULL,
            Currency TEXT NOT NULL,
            Description TEXT NOT NULL,
            FOREIGN KEY(EmployeeId) REFERENCES Personnel(EmployeeId)
        );
        """;
        command.ExecuteNonQuery();
    }

    private void Seed()
    {
        using var transaction = Connection.BeginTransaction();

        foreach (var employee in ExpenseDeskSeedData.Personnel)
        {
            using var command = Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
            INSERT INTO Personnel (
                EmployeeId,
                FullName,
                Department,
                Title,
                Location
            )
            VALUES (
                @employeeId,
                @fullName,
                @department,
                @title,
                @location
            );
            """;
            command.Parameters.AddWithValue("@employeeId", employee.EmployeeId);
            command.Parameters.AddWithValue("@fullName", employee.FullName);
            command.Parameters.AddWithValue("@department", employee.Department);
            command.Parameters.AddWithValue("@title", employee.Title);
            command.Parameters.AddWithValue("@location", employee.Location);
            command.ExecuteNonQuery();
        }

        foreach (var expense in ExpenseDeskSeedData.Expenses)
        {
            using var command = Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
            INSERT INTO Expenses (
                ExpenseId,
                EmployeeId,
                ExpenseDate,
                Category,
                Amount,
                Currency,
                Description
            )
            VALUES (
                @expenseId,
                @employeeId,
                @expenseDate,
                @category,
                @amount,
                @currency,
                @description
            );
            """;
            command.Parameters.AddWithValue("@expenseId", expense.ExpenseId);
            command.Parameters.AddWithValue("@employeeId", expense.EmployeeId);
            command.Parameters.AddWithValue("@expenseDate", expense.ExpenseDate);
            command.Parameters.AddWithValue("@category", expense.Category);
            command.Parameters.AddWithValue("@amount", expense.Amount);
            command.Parameters.AddWithValue("@currency", expense.Currency);
            command.Parameters.AddWithValue("@description", expense.Description);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
