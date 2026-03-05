using System.Text;

namespace DeviceDesk.Modules.SuperAdmin.Utils;

public static class CsvHelper
{
    /// <summary>
    /// Escapes a value for CSV format by handling commas, quotes, and newlines.
    /// </summary>
    public static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // If the value contains comma, quote, or newline, wrap it in quotes and escape internal quotes
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    /// <summary>
    /// Builds a CSV row from multiple values.
    /// </summary>
    public static string BuildCsvRow(params string?[] values)
    {
        return string.Join(",", values.Select(CsvEscape));
    }

    /// <summary>
    /// Formats a DateTime as "yyyy-MM-dd HH:mm:ss" in UTC.
    /// </summary>
    public static string FormatTimestamp(DateTime? dt)
    {
        if (dt == null)
            return string.Empty;

        var utcTime = dt.Value.Kind == DateTimeKind.Utc 
            ? dt.Value 
            : dt.Value.ToUniversalTime();

        return utcTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// Formats a DateTimeOffset as "yyyy-MM-dd HH:mm:ss" in UTC.
    /// </summary>
    public static string FormatTimestamp(DateTimeOffset? dt)
    {
        if (dt == null)
            return string.Empty;

        return dt.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// Generates a filename with UTC timestamp.
    /// </summary>
    public static string GenerateFilename(string prefix)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        return $"{prefix}_{timestamp}.csv";
    }
}

