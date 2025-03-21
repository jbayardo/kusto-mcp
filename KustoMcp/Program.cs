using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using McpDotNet;
using Azure.Identity;
using System.Text.Json;
using Azure.Core;

namespace KustoMcp
{
    internal class Program
    {
        public class ApplicationSettings
        {
            public List<string> Clusters { get; set; } = new List<string>();

            public string Credential { get; set; } = "Default";
        }

        static async Task Main(string[] args)
        {
            var builder = Host.CreateEmptyApplicationBuilder(settings: null);

            ApplicationSettings? settings = null;
            if (args.Length > 0 && !string.IsNullOrEmpty(args[0]) && File.Exists(args[0]))
            {
                string jsonContent = File.ReadAllText(args[0]);
                settings = JsonSerializer.Deserialize<ApplicationSettings>(jsonContent);
            }

            if (settings is null)
            {
                throw new InvalidOperationException("No configuration file specified.");
            }

            builder.Configuration
                .AddEnvironmentVariables()
                .AddCommandLine(args);

            builder.Logging.AddConsole();
            builder.Logging.AddDebug();

            if (settings.Clusters.Count == 0)
            {
                throw new InvalidOperationException("No Kusto clusters specified.");
            }

            var clusters = settings.Clusters.Select(url => new Uri(url)).ToList();

            var credential = GetCredential(settings.Credential);

            var cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KustoMcp",
                "cache");

            using var client = await KustoClient.CreateAsync(clusters, credential, cachePath);
            builder.Services.AddSingleton(client);

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithTools(typeof(KustoClientPlugin), typeof(KustoContextPlugin));

            var host = builder.Build();

            var logger = host.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger<Program>();
            logger.LogInformation("Application starting");
            logger.LogInformation("Connected to clusters: {Clusters}", string.Join(", ", settings.Clusters));

            await host.RunAsync();
        }

        private static TokenCredential GetCredential(string credentialType)
        {
            return credentialType.ToLower() switch
            {
                "cli" => new AzureCliCredential(),
                "managedidentity" => new ManagedIdentityCredential(),
                "default" or _ => new DefaultAzureCredential()
            };
        }
    }
}
