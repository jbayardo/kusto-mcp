using McpDotNet.Server;
using System.ComponentModel;

namespace KustoMcp;

internal class KustoClientPlugin
{
    private readonly KustoClient _client;

    public KustoClientPlugin(KustoClient client)
    {
        _client = client;
    }

    [McpTool(nameof(FetchTableSample))]
    [Description("Fetch a sample of the given table from the given cluster and database")]
    [return: Description("Table sample in CSV format")]
    public async Task<string> FetchTableSample(string cluster, string database, string table, int size, CancellationToken cancellationToken)
    {
        if (size > 1000)
        {
            size = 1000;
        }

        if (size < 0)
        {
            size = 10;
        }

        try
        {
            return await ExecuteQuery(cluster, database, $"table('{table}') | limit {size}", cancellationToken);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpTool(nameof(ExecuteQuery))]
    [Description("Execute a query on the given cluster and database")]
    [return: Description("Query results in CSV format")]
    public async Task<string> ExecuteQuery(string cluster, string database, string query, CancellationToken cancellationToken)
    {
        try
        {
            var connection = _client.GetConnection(cluster);
            var csv = await connection.FetchQueryCsvAsync(query, database, cancellationToken: cancellationToken);
            return csv;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
