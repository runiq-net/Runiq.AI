using Npgsql;
using Runiq.AI.Memory.Models;
using Runiq.AI.Memory.PostgreSql.Configuration;

namespace Runiq.AI.Memory.PostgreSql.Persistence;

// A provider-owned pool avoids replacing RAG's NpgsqlDataSource. The DI container owns disposal.
internal sealed class MemoryDatabase : IAsyncDisposable, IDisposable
{
    internal NpgsqlDataSource DataSource { get; }
    internal string Schema { get; }
    internal string QuotedSchema { get; }

    internal MemoryDatabase(PostgreSqlMemoryOptions options)
    {
        Schema = options.Schema;
        QuotedSchema = new NpgsqlCommandBuilder().QuoteIdentifier(Schema);
        DataSource = NpgsqlDataSource.Create(options.ConnectionString);
    }

    internal NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql.Replace("__SCHEMA__", QuotedSchema, StringComparison.Ordinal), connection, transaction);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    internal static async ValueTask<T> ExecuteAsync<T>(Func<ValueTask<T>> action)
    {
        try { return await action(); }
        catch (NpgsqlException exception) { throw new MemoryStoreException(MemoryStoreError.StorageFailure, exception); }
    }

    public void Dispose() => DataSource.Dispose();
    public ValueTask DisposeAsync() => DataSource.DisposeAsync();
}
