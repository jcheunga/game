using System.Data.Common;
#if SQLITE_TEST
using Microsoft.Data.Sqlite;
#endif
using Npgsql;

namespace CrownroadServer;

/// <summary>
/// Keeps the SQL call sites provider-neutral. Both providers use ADO.NET, but
/// their concrete parameter collections expose AddWithValue independently.
/// </summary>
public static class DbParameterCollectionExtensions
{
    public static DbParameter AddWithValue(this DbParameterCollection parameters, string name, object? value)
    {
        DbParameter parameter = parameters switch
        {
#if SQLITE_TEST
            SqliteParameterCollection => new SqliteParameter(name, value ?? DBNull.Value),
#endif
            NpgsqlParameterCollection => new NpgsqlParameter(name, value ?? DBNull.Value),
            _ => throw new NotSupportedException($"Unsupported database parameter collection: {parameters.GetType().Name}")
        };

        parameters.Add(parameter);
        return parameter;
    }
}
