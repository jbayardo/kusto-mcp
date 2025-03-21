using async_enumerable_dotnet;
using Azure.Core;
using Kusto.Cloud.Platform.Utils;
using Kusto.Language;
using Kusto.Language.Symbols;
using Kusto.Toolkit;

namespace KustoMcp;

internal record KustoClient(Dictionary<Uri, Connection> Connections, CachedSymbolLoader Loader, SymbolResolver Resolver, GlobalState GlobalState) : IDisposable
{
    public static async Task<KustoClient> CreateAsync(IEnumerable<Uri> clusters, TokenCredential credential, string cachePath, CancellationToken cancellationToken = default)
    {
        var connections = clusters.ToDictionary(cluster => cluster, cluster => Connection.Create(cluster, credential));
        var loader = new CachedSymbolLoader(connections.First().Value.Builder, cachePath);
        var resolver = new SymbolResolver(loader);
        var state = await CreateStateAsync(clusters, loader, cancellationToken);
        return new KustoClient(connections, loader, resolver, state);
    }

    private static async Task<GlobalState> CreateStateAsync(IEnumerable<Uri> clusters, CachedSymbolLoader loader, CancellationToken cancellationToken)
    {
        var global = GlobalState.Default;

        foreach (var cluster in clusters)
        {
            global = await loader.AddOrUpdateClusterAsync(global, clusterName: cluster.ToString(), throwOnError: true, cancellationToken);
        }

        foreach (var cluster in global.Clusters)
        {
            foreach (var database in cluster.Databases)
            {
                global = await loader.AddOrUpdateDatabaseAsync(global, database.Name, cluster.Name, throwOnError: true, cancellationToken);
            }
        }

        return global;
    }

    public void Dispose()
    {
        foreach (var connection in Connections)
        {
            connection.Value.Dispose();
        }

        Loader.Dispose();
    }

    public Connection GetConnection(string cluster)
    {
        var clusterSymbol = GlobalState.GetCluster(cluster);
        if (clusterSymbol is null)
        {
            throw new ArgumentException($"Cluster '{cluster}' not found.");
        }

        return GetConnection(clusterSymbol);
    }

    public Connection GetConnection(ClusterSymbol cluster)
    {
        var uri = cluster.Name.IntoClusterUri();
        return Connections[uri];
    }

    public async Task<KustoCode> ParseAsync(ClusterSymbol cluster, DatabaseSymbol database, string query, CancellationToken cancellationToken = default)
    {
        var code = KustoCode.ParseAndAnalyze(
            query,
            GlobalState.WithCluster(cluster).WithDatabase(database),
            cancellationToken);
        return await Resolver.AddReferencedDatabasesAsync(code, throwOnError: true, cancellationToken);
    }
}

public static class KustoNamingExtensions
{
    public static Uri IntoClusterUri(this string cluster)
    {
        if (!cluster.StartsWith("http://") && !cluster.StartsWith("https://"))
        {
            cluster = "https://" + cluster;
        }

        return new Uri(cluster);
    }

    public static Uri IntoClusterUri(this ClusterSymbol cluster)
    {
        return cluster.Name.IntoClusterUri();
    }
}
