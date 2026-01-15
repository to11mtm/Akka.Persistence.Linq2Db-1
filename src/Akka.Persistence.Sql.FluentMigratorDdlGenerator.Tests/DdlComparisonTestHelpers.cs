// -----------------------------------------------------------------------
//  <copyright file="DdlComparisonTestHelpers.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace Akka.Persistence.Sql.FluentMigratorDdlGenerator.Tests;

/// <summary>
/// Helper utilities for comparing DDL output! Super useful desu~ 🔍
/// </summary>
/// <remarks>
/// CopilotNotes: These helpers normalize SQL statements to make comparison
/// more meaningful. We focus on structural equivalence rather than exact
/// string matching, because different generators may format things differently! ✨
/// </remarks>
public static class DdlComparisonTestHelpers
{
    /// <summary>
    /// Normalizes SQL for comparison by removing comments and extra whitespace~
    /// </summary>
    public static string NormalizeSql(string sql)
    {
        if (string.IsNullOrEmpty(sql))
            return string.Empty;

        // Remove SQL comments (-- style)
        sql = Regex.Replace(sql, @"--.*$", "", RegexOptions.Multiline);

        // Remove multi-line comments
        sql = Regex.Replace(sql, @"/\*.*?\*/", "", RegexOptions.Singleline);

        // Normalize line endings
        sql = sql.Replace("\r\n", "\n").Replace("\r", "\n");

        // Collapse multiple whitespace to single space
        sql = Regex.Replace(sql, @"\s+", " ");

        // Trim each statement
        sql = sql.Trim();

        return sql;
    }

    /// <summary>
    /// Extracts just the CREATE TABLE statement for structural comparison! 📊
    /// </summary>
    public static string ExtractCreateTableStatement(string sql)
    {
        // Match CREATE TABLE ... ); pattern
        var match = Regex.Match(sql, @"CREATE\s+TABLE.*?\);", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        return match.Success ? NormalizeSql(match.Value) : string.Empty;
    }

    /// <summary>
    /// Extracts column definitions from a CREATE TABLE statement! 📝
    /// </summary>
    public static IEnumerable<string> ExtractColumnDefinitions(string createTableSql)
    {
        // Extract the part between ( and )
        var match = Regex.Match(createTableSql, @"\((.*)\)", 
            RegexOptions.Singleline);
        
        if (!match.Success)
            yield break;

        var columnsSection = match.Groups[1].Value;
        
        // Split by comma, but be careful about nested parentheses
        var columns = SplitColumnDefinitions(columnsSection);
        
        foreach (var col in columns)
        {
            var normalized = NormalizeSql(col);
            if (!string.IsNullOrWhiteSpace(normalized))
                yield return normalized;
        }
    }

    /// <summary>
    /// Smart column definition splitter that respects nested parentheses~
    /// </summary>
    private static IEnumerable<string> SplitColumnDefinitions(string columnsSection)
    {
        var depth = 0;
        var current = "";
        
        foreach (var c in columnsSection)
        {
            if (c == '(')
                depth++;
            else if (c == ')')
                depth--;
            
            if (c == ',' && depth == 0)
            {
                yield return current.Trim();
                current = "";
            }
            else
            {
                current += c;
            }
        }
        
        if (!string.IsNullOrWhiteSpace(current))
            yield return current.Trim();
    }

    /// <summary>
    /// Extracts table name from CREATE TABLE statement~
    /// </summary>
    public static string? ExtractTableName(string sql)
    {
        // Match various quoting styles: [name], "name", `name`, or just name
        var match = Regex.Match(sql, 
            @"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?:[\[\""` ]?(\w+)[\]\""`]?\.)?[\[\""` ]?(\w+)[\]\""`]?",
            RegexOptions.IgnoreCase);
        
        return match.Success ? match.Groups[2].Value : null;
    }

    /// <summary>
    /// Checks if two SQL scripts create tables with the same columns (by name)~
    /// </summary>
    public static (bool AreEquivalent, List<string> Differences) CompareTableStructure(
        string expectedSql, 
        string actualSql)
    {
        var differences = new List<string>();

        var expectedTable = ExtractTableName(expectedSql);
        var actualTable = ExtractTableName(actualSql);

        if (expectedTable?.ToLowerInvariant() != actualTable?.ToLowerInvariant())
        {
            differences.Add($"Table name mismatch: expected '{expectedTable}', got '{actualTable}'");
        }

        var expectedColumns = ExtractColumnDefinitions(ExtractCreateTableStatement(expectedSql))
            .Select(c => ExtractColumnName(c)?.ToLowerInvariant())
            .Where(c => c != null)
            .ToHashSet();

        var actualColumns = ExtractColumnDefinitions(ExtractCreateTableStatement(actualSql))
            .Select(c => ExtractColumnName(c)?.ToLowerInvariant())
            .Where(c => c != null)
            .ToHashSet();

        var missingInActual = expectedColumns.Except(actualColumns).ToList();
        var extraInActual = actualColumns.Except(expectedColumns).ToList();

        if (missingInActual.Any())
            differences.Add($"Missing columns: {string.Join(", ", missingInActual)}");

        if (extraInActual.Any())
            differences.Add($"Extra columns: {string.Join(", ", extraInActual)}");

        return (differences.Count == 0, differences);
    }

    /// <summary>
    /// Extracts column name from a column definition~
    /// </summary>
    private static string? ExtractColumnName(string columnDef)
    {
        // Handle CONSTRAINT definitions
        if (columnDef.TrimStart().StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase) ||
            columnDef.TrimStart().StartsWith("PRIMARY", StringComparison.OrdinalIgnoreCase) ||
            columnDef.TrimStart().StartsWith("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
            columnDef.TrimStart().StartsWith("INDEX", StringComparison.OrdinalIgnoreCase) ||
            columnDef.TrimStart().StartsWith("KEY", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Extract column name (first word, possibly quoted)
        var match = Regex.Match(columnDef.Trim(), @"^[\[\""` ]?(\w+)[\]\""`]?");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Gets the expected DDL file path based on provider and mapping mode~
    /// </summary>
    public static string GetExpectedDdlPath(string baseDir, DatabaseProvider provider, TableMappingMode mode, string fileName)
    {
        var mappingDir = mode == TableMappingMode.Default ? "default" : "compat";
        var providerDir = provider.ToString().ToLowerInvariant();
        return Path.Combine(baseDir, "ExpectedDdl", mappingDir, providerDir, fileName);
    }
}

