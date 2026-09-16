using System.Text.RegularExpressions;

namespace Invc.Infrastructure.Data;

/// <summary>
/// Defence-in-depth guard: every SQL text the application sends must be a single
/// SELECT (optionally starting with a CTE) and must not contain data- or schema-
/// modifying keywords. The production database is strictly read-only; the real
/// protection is the database login, this guard catches programming mistakes early.
/// </summary>
public static partial class ReadOnlySql
{
    private static readonly string[] ForbiddenKeywords =
    [
        "INSERT", "UPDATE", "DELETE", "MERGE", "TRUNCATE", "CREATE", "ALTER", "DROP",
        "GRANT", "DENY", "REVOKE", "EXEC", "EXECUTE", "sp_executesql", "xp_", "DBCC",
        "BULK", "OPENROWSET", "OPENQUERY", "BACKUP", "RESTORE", "SHUTDOWN", "RECONFIGURE",
        "INTO",
    ];

    /// <summary>Returns the SQL unchanged when it is a read-only SELECT; throws otherwise.</summary>
    public static string Ensure(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var stripped = StripComments(sql).Trim();
        if (stripped.Contains(';'))
        {
            throw new ReadOnlyViolationException("Statement batches are not allowed (';' found).");
        }

        if (!StartsWithSelectRegex().IsMatch(stripped))
        {
            throw new ReadOnlyViolationException("Only SELECT (or WITH ... SELECT) statements are allowed.");
        }

        foreach (var keyword in ForbiddenKeywords)
        {
            if (Regex.IsMatch(stripped, $@"(?<![\w@]){Regex.Escape(keyword)}(?![\w])", RegexOptions.IgnoreCase))
            {
                throw new ReadOnlyViolationException($"Forbidden keyword '{keyword}' in read-only SQL.");
            }
        }

        return sql;
    }

    private static string StripComments(string sql)
    {
        var noBlock = BlockCommentRegex().Replace(sql, " ");
        return LineCommentRegex().Replace(noBlock, " ");
    }

    [GeneratedRegex(@"^\s*(SELECT|WITH)\b", RegexOptions.IgnoreCase)]
    private static partial Regex StartsWithSelectRegex();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockCommentRegex();

    [GeneratedRegex(@"--[^\r\n]*")]
    private static partial Regex LineCommentRegex();
}

public sealed class ReadOnlyViolationException(string message) : InvalidOperationException(message);
