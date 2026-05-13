using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace DeviceDesk.Modules.Phase1.Services.DocumentIngest;

public class DynamicIngestTableService
{
    private static readonly Regex SafeIdentifier = new(@"^[a-z][a-z0-9_]{0,62}$", RegexOptions.Compiled);

    public string NormalizeTableName(string suggested)
    {
        var s = suggested.Trim().ToLowerInvariant().Replace('-', '_').Replace(" ", "_");
        s = Regex.Replace(s, @"[^a-z0-9_]", "");
        if (string.IsNullOrEmpty(s)) s = "custom_doc";
        if (!s.StartsWith("ing_", StringComparison.Ordinal)) s = "ing_" + s.TrimStart('_');
        if (!SafeIdentifier.IsMatch(s))
            throw new ArgumentException("Table name must match snake_case letters, numbers, and underscores (max 63 chars after ing_).");
        if (s.Length > 64) s = s[..64];
        return s;
    }

    public string NormalizeColumnName(string name)
    {
        var s = name.Trim().ToLowerInvariant().Replace('-', '_').Replace(" ", "_");
        s = Regex.Replace(s, @"[^a-z0-9_]", "");
        if (string.IsNullOrEmpty(s)) s = "col";
        if (!SafeIdentifier.IsMatch(s))
            throw new ArgumentException($"Invalid column name: {name}");
        return s;
    }

    public string MapSqlType(string t)
    {
        var x = t.Trim().ToLowerInvariant();
        return x switch
        {
            "int" or "integer" => "INT",
            "bigint" => "BIGINT",
            "bit" or "bool" or "boolean" => "BIT",
            "decimal" or "money" or "currency" => "DECIMAL(18,2)",
            "float" or "double" => "FLOAT(53)",
            "datetime" or "datetime2" => "DATETIME2(7)",
            "date" => "DATE",
            "uniqueidentifier" or "guid" => "UNIQUEIDENTIFIER",
            "nvarchar(max)" => "NVARCHAR(MAX)",
            _ when x.StartsWith("nvarchar(") => x.ToUpperInvariant(),
            _ => "NVARCHAR(400)"
        };
    }

    public async Task CreateUserTableAsync(
        DbConnection connection,
        string tableName,
        IReadOnlyList<(string Column, string SqlType)> columns,
        CancellationToken ct)
    {
        if (columns.Count == 0)
            throw new InvalidOperationException("At least one column is required.");

        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE [dbo].[{tableName}] (");
        sb.AppendLine("  [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),");
        foreach (var (col, sqlType) in columns.Where(c => !string.Equals(c.Column, "id", StringComparison.OrdinalIgnoreCase)))
        {
            sb.Append("  [").Append(col).Append("] ").Append(sqlType).AppendLine(" NULL,");
        }

        sb.AppendLine("  [SourceFilePath] NVARCHAR(1024) NULL,");
        sb.AppendLine("  [CreatedAt] DATETIMEOFFSET(7) NOT NULL DEFAULT SYSUTCDATETIME()");
        sb.AppendLine(");");

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sb.ToString();
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task InsertRowAsync(
        DbConnection connection,
        string tableName,
        IReadOnlyDictionary<string, object?> values,
        string? sourceFilePath,
        CancellationToken ct)
    {
        var cols = values.Keys.ToList();
        var sb = new StringBuilder();
        sb.Append("INSERT INTO [dbo].[").Append(tableName).Append("] (");
        sb.Append(string.Join(", ", cols.Select(c => $"[{c}]")));
        sb.Append(", [SourceFilePath]) VALUES (");
        sb.Append(string.Join(", ", cols.Select(c => "@" + c)));
        sb.Append(", @SourceFilePath)");

        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sb.ToString();
        foreach (var c in cols)
        {
            var p = new SqlParameter("@" + c, values[c] ?? DBNull.Value);
            cmd.Parameters.Add(p);
        }

        cmd.Parameters.Add(new SqlParameter("@SourceFilePath", sourceFilePath ?? (object)DBNull.Value));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Insert into an existing user-created ingest table (columns must exist).</summary>
    public async Task InsertRowFlexibleAsync(
        DbConnection connection,
        string tableName,
        IReadOnlyDictionary<string, string> stringValues,
        string? sourceFilePath,
        CancellationToken ct)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in stringValues)
            dict[kv.Key] = kv.Value;
        await InsertRowAsync(connection, tableName, dict, sourceFilePath, ct);
    }
}
