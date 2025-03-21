using Azure.Core;
using Kusto.Cloud.Platform.Data;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using System.Runtime.CompilerServices;

namespace KustoMcp;

public record Connection(Uri Cluster, KustoConnectionStringBuilder Builder, ICslQueryProvider Client, ICslAdminProvider AdminClient) : IDisposable
{
    public static Connection Create(Uri cluster, TokenCredential credential)
    {
        var connection = new KustoConnectionStringBuilder(cluster.ToString())
            .WithAadAzureTokenCredentialsAuthentication(credential);
        var client = KustoClientFactory.CreateCslQueryProvider(connection);
        var adminClient = KustoClientFactory.CreateCslAdminProvider(connection);
        return new Connection(cluster, connection, client, adminClient);
    }

    public async Task<string> FetchQueryCsvAsync(
        string query,
        string database = "NetDefaultDB",
        ClientRequestProperties? properties = null,
        CancellationToken cancellationToken = default)
    {
        if (properties is null)
        {
            properties = CreateDefaultRequestProperties(database);
        }

        try
        {
            using var response = await Client.ExecuteQueryAsync(database, query, properties, cancellationToken);

            await using var writer = new StringWriter();
            int n = 0;
            do
            {
                if (n > 0)
                {
                    writer.WriteLine();
                }

                response.WriteAsCsv(includeHeaderAsFirstRow: true, writer);
                n++;
            }
            while (response.NextResult());

            return writer.ToString();
        }
        catch (Exception ex)
        {
            ex.Data.Add("query", query);
            ex.Data.Add("database", database);
            ex.Data.Add("properties", properties);

            throw;
        }
    }

    public async IAsyncEnumerable<T> ExecuteQueryAsync<T>(
        string query,
        string database = "NetDefaultDB",
        ClientRequestProperties? properties = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (properties is null)
        {
            properties = CreateDefaultRequestProperties(database);
        }

        using var response = await Client.ExecuteQueryAsync(database, query, properties, cancellationToken);

        await foreach (var entry in GenerateAsync<T>(response, cancellationToken))
        {
            yield return entry;
        }
    }

    public static ClientRequestProperties CreateDefaultRequestProperties(string database)
    {
        ClientRequestProperties? properties = new ClientRequestProperties()
        {
            ClientRequestId = $"gilda;{database};{Guid.NewGuid().ToString()}",
        };

        properties.SetOption("notruncation", "true");
        properties.SetOption("maxmemoryconsumptionperiterator", "68719476736");
        properties.SetOption("max_memory_consumption_per_query_per_node", "68719476736");

        // WARNING: this may break the cluster when the load is high enough
        // properties.SetOption("norequesttimeout", "true");

        return properties;
    }

    public async IAsyncEnumerable<T> ExecuteAdminQueryAsync<T>(
        string query,
        string database = "NetDefaultDB",
        ClientRequestProperties? properties = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (properties is null)
        {
            properties = CreateDefaultRequestProperties(database);
        }

        using var response = await AdminClient.ExecuteControlCommandAsync(database, query, properties);

        await foreach (var entry in GenerateAsync<T>(response, cancellationToken))
        {
            yield return entry;
        }
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    private static async IAsyncEnumerable<T> GenerateAsync<T>(System.Data.IDataReader response, [EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    {
        var reader = new ObjectReader<T>(response, disposeReader: false, nameBasedColumnMapping: true);

        foreach (var entry in reader)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return entry;
        }
    }

    public void Dispose()
    {
        Client.Dispose();
        AdminClient.Dispose();
    }
}
