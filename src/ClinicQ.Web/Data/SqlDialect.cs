namespace ClinicQ.Web.Data;

/// <summary>
/// The handful of SQL fragments that differ between SQL Server and SQLite. Everything else in the
/// repositories is written in the common subset both engines understand.
/// </summary>
public interface ISqlDialect
{
    /// <summary>Statement appended after an INSERT to return the new identity value.</summary>
    string SelectInsertedId { get; }

    /// <summary>Paging clause using parameters named @Offset and @Limit (must follow an ORDER BY).</summary>
    string Paging { get; }

    /// <summary>Case-insensitive LIKE expression for the given column against @Search.</summary>
    string Like(string column);
}

public sealed class SqlServerDialect : ISqlDialect
{
    public string SelectInsertedId => "SELECT CAST(SCOPE_IDENTITY() AS INT);";
    public string Paging => "OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";
    public string Like(string column) => $"{column} LIKE @Search";
}

public sealed class SqliteDialect : ISqlDialect
{
    public string SelectInsertedId => "SELECT last_insert_rowid();";
    public string Paging => "LIMIT @Limit OFFSET @Offset";
    public string Like(string column) => $"{column} LIKE @Search COLLATE NOCASE";
}

public static class SqlDialectFactory
{
    public static ISqlDialect For(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.SqlServer => new SqlServerDialect(),
        _ => new SqliteDialect()
    };
}
