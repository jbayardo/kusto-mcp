using async_enumerable_dotnet;
using Kusto.Language;
using Kusto.Language.Symbols;
using Kusto.Toolkit;
using McpDotNet.Server;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace KustoMcp;

internal class KustoContextPlugin
{
    private readonly KustoClient _client;

    public KustoContextPlugin(KustoClient client)
    {
        _client = client;
    }

    public record DetailedClusterInfo
    {
        public required ClusterInfo Cluster { get; init; }

        public required IReadOnlyList<DatabaseInfo> Databases { get; init; }

        public static DetailedClusterInfo FromSymbol(ClusterSymbol cluster)
        {
            return new DetailedClusterInfo
            {
                Cluster = ClusterInfo.FromSymbol(cluster),
                Databases = cluster.Databases
                    .Select(DatabaseInfo.FromSymbol)
                    .ToList(),
            };
        }
    }

    public class SummaryInfo
    {
        public required IReadOnlyList<DetailedClusterInfo> Clusters { get; init; }

        public static SummaryInfo FromSymbol(GlobalState state)
        {
            return new SummaryInfo
            {
                Clusters = state.Clusters
                    .Select(DetailedClusterInfo.FromSymbol)
                    .ToList(),
            };
        }
    }

    [McpTool(nameof(ShowDetailedClusterInformation))]
    [Description("List of clusters, databases, and their tables in JSON format")]
    [return: Description("JSON string containing list of clusters, databases, and their tables")]
    public string ShowDetailedClusterInformation()
    {
        var summary = SummaryInfo.FromSymbol(_client.GlobalState);
        return JsonSerializer.Serialize(summary, JsonUtilities.JsonSerializerOptions);
    }

    public class ClusterInfo
    {
        public required string Name { get; init; }

        public static ClusterInfo FromSymbol(ClusterSymbol cluster)
        {
            return new ClusterInfo
            {
                Name = cluster.Name,
            };
        }
    }

    [McpTool(nameof(ShowClusters))]
    [Description("List all clusters available in the Kusto client")]
    [return: Description("JSON string containing list of clusters")]
    public string ShowClusters()
    {
        var clusters = _client.GlobalState.Clusters
            .Select(ClusterInfo.FromSymbol)
            .ToList();
        return JsonSerializer.Serialize(clusters, JsonUtilities.JsonSerializerOptions);
    }

    public class DatabaseInfo
    {
        public required string Name { get; init; }

        public required string? AlternateName { get; init; }

        public required IReadOnlyList<TableInfo> Tables { get; init; }

        public static DatabaseInfo FromSymbol(DatabaseSymbol database)
        {
            return new DatabaseInfo
            {
                Name = database.Name,
                AlternateName = string.IsNullOrEmpty(database.AlternateName) ? null : database.AlternateName,
                Tables = database.Tables
                    .Select(TableInfo.FromSymbol)
                    .ToList(),
            };
        }
    }

    [McpTool(nameof(ShowDatabases))]
    [Description("List all databases available in the given cluster, along with their tables")]
    [return: Description("JSON string containing list of databases and tables")]
    public string ShowDatabases(string cluster)
    {
        var clusterSymbol = GetCluster(_client, cluster);

        var databases = clusterSymbol.Databases
            .Select(DatabaseInfo.FromSymbol)
            .ToList();

        return JsonSerializer.Serialize(databases, JsonUtilities.JsonSerializerOptions);
    }

    public class TableInfo
    {
        public required string Name { get; init; }

        public required string? AlternateName { get; init; }

        public static TableInfo FromSymbol(TableSymbol table)
        {
            return new TableInfo
            {
                Name = table.Name,
                AlternateName = string.IsNullOrEmpty(table.AlternateName) ? null : table.AlternateName,
            };
        }
    }

    [McpTool(nameof(ShowTables))]
    [Description("List all tables available in the given database")]
    [return: Description("JSON string containing list of tables")]
    public string ShowTables(string cluster, string database)
    {
        var tables = GetDatabase(GetCluster(_client, cluster), database)
            .Tables
            .Select(TableInfo.FromSymbol)
            .ToList();

        return JsonSerializer.Serialize(tables, JsonUtilities.JsonSerializerOptions);
    }

    public class DetailedTableInfo
    {
        public required string Name { get; init; }

        public required string? AlternateName { get; init; }

        public required IReadOnlyList<ColumnInfo> Columns { get; init; }

        public static DetailedTableInfo FromSymbol(TableSymbol table)
        {
            return new DetailedTableInfo
            {
                Name = table.Name,
                AlternateName = string.IsNullOrEmpty(table.AlternateName) ? null : table.AlternateName,
                Columns = table.Columns
                    .Select(ColumnInfo.FromSymbol)
                    .ToList(),
            };
        }
    }

    public class ColumnInfo
    {
        public required string Name { get; init; }

        public required string Type { get; init; }

        public static ColumnInfo FromSymbol(ColumnSymbol column)
        {
            return new ColumnInfo
            {
                Name = column.Name,
                Type = column.Type.Name,
            };
        }
    }

    [McpTool(nameof(ShowTable))]
    [Description("Show the schema for a single table")]
    [return: Description("JSON string containing table schema")]
    public string ShowTable(string cluster, string database, string table)
    {
        var tableSymbol = GetTable(GetDatabase(GetCluster(_client, cluster), database), table);
        var detailedInfo = DetailedTableInfo.FromSymbol(tableSymbol);
        return JsonSerializer.Serialize(detailedInfo, JsonUtilities.JsonSerializerOptions);
    }

    [McpTool(nameof(Validate))]
    [Description("Quickly validate a given query's syntax is valid without running it")]
    [return: Description("OK if the query's syntax is valid, along with expected output schema. List of syntax errors otherwise")]
    public async Task<string> Validate(string cluster, string database, string query, CancellationToken cancellationToken)
    {
        try
        {
            var diagnostics = await TryGetDiagnosticsAsync(cluster, database, query, cancellationToken);
            if (diagnostics.IsFailure())
            {
                return diagnostics.Result;
            }

            var schema = diagnostics.Code!.GetResultColumns();
            return $"Query Syntax OK. Output Schema: {string.Join(",", schema.Select(c => $"{c.Name}: {c.Type.Name}"))}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    internal static ClusterSymbol GetCluster(KustoClient client, string cluster)
    {
        var clusterSymbol = client.GlobalState.GetCluster(cluster);
        if (clusterSymbol is null)
        {
            var clusters = client.Connections.Keys.Select(k => $"- {k}").IntersperseNewlines();
            var error = $"Cluster '{cluster}' not found. Valid clusters are:{Environment.NewLine}{clusters}";
            throw new ArgumentException(error);
        }

        return clusterSymbol;
    }

    internal static DatabaseSymbol GetDatabase(ClusterSymbol clusterSymbol, string database)
    {
        var databaseSymbol = clusterSymbol.GetDatabase(database);
        if (databaseSymbol is null)
        {
            var databases = clusterSymbol.Databases.Select(d => $"- {d.Name}").IntersperseNewlines();
            var error = $"Database '{database}' not found in cluster '{clusterSymbol.Name}'. Valid databases are:{Environment.NewLine}{databases}";
            throw new ArgumentException(error);
        }

        return databaseSymbol;
    }

    private static TableSymbol GetTable(DatabaseSymbol databaseSymbol, string table)
    {
        var tableSymbol = databaseSymbol.GetTable(table);
        if (tableSymbol is null)
        {
            var tables = databaseSymbol.Tables.Select(d => $"- {d.Name}").IntersperseNewlines();
            throw new ArgumentException($"Table '{table}' not found in database '{databaseSymbol.Name}'. Valid tables are:{Environment.NewLine}{tables}");
        }

        return tableSymbol;
    }

    internal static bool TryGetCluster(
        KustoClient client,
        string cluster,
        [NotNullWhen(true)] out ClusterSymbol? clusterSymbol,
        [NotNullWhen(false)] out string? error)
    {
        error = null;

        clusterSymbol = client.GlobalState.GetCluster(cluster);
        if (clusterSymbol is null)
        {
            var clusters = client.Connections.Keys.Select(k => $"- {k}").IntersperseNewlines();
            error = $"Cluster '{cluster}' not found. Valid clusters are:{Environment.NewLine}{clusters}";

            return false;
        }

        return true;
    }

    internal static bool TryGetDatabase(
        ClusterSymbol clusterSymbol,
        string database,
        [NotNullWhen(true)] out DatabaseSymbol? databaseSymbol,
        [NotNullWhen(false)] out string? error)
    {
        error = null;

        databaseSymbol = clusterSymbol.GetDatabase(database);
        if (databaseSymbol is null)
        {
            var databases = clusterSymbol.Databases.Select(d => $"- {d.Name}").IntersperseNewlines();
            error = $"Database '{database}' not found in cluster '{clusterSymbol.Name}'. Valid databases are:{Environment.NewLine}{databases}";

            return false;
        }

        return true;
    }

    internal record Diagnostics(
        bool Success,
        string Result,
        KustoCode? Code)
    {
        public bool IsFailure()
        {
            return !Success;
        }
    }

    internal class DiagnosticsResult
    {
        public required IReadOnlyList<string> Errors { get; init; }

        public required IReadOnlyList<DetailedTableInfo> ReferencedTables { get; init; }
    }

    public async Task<Diagnostics> TryGetDiagnosticsAsync(string cluster, string database, string query, CancellationToken cancellationToken)
    {
        if (!TryGetCluster(_client, cluster, out var clusterSymbol, out var clusterError))
        {
            return new Diagnostics(Success: false, Result: clusterError, Code: null);
        }

        if (!TryGetDatabase(clusterSymbol, database, out var databaseSymbol, out var databaseError))
        {
            return new Diagnostics(Success: false, Result: databaseError, Code: null);
        }

        var code = await _client.ParseAsync(clusterSymbol, databaseSymbol, query, cancellationToken);
        var diagnostics = code.GetDiagnostics(cancellationToken);
        var errors = diagnostics
                .Where(diagnostic => diagnostic.Severity == "Error")
                .Select(d => d.Message)
                .ToList();

        if (errors.Count == 0)
        {
            return new Diagnostics(Success: true, Result: string.Empty, Code: code);
        }

        var tables = code.GetDatabaseTablesReferenced();

        var tableSchemas = tables
            .Select(DetailedTableInfo.FromSymbol)
            .ToList();

        var result = new DiagnosticsResult
        {
            Errors = errors,
            ReferencedTables = tableSchemas,
        };

        string errorMessage = JsonSerializer.Serialize(result, JsonUtilities.JsonSerializerOptions);

        return new Diagnostics(
            Success: false,
            Result: errorMessage,
            Code: code);
    }
}
