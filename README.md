# KustoMcp - Kusto Model Context Protocol Server

A .NET-based server implementing Model Context Protocol for Kusto database interaction.

## Running Without Docker

You can run KustoMcp directly on your machine without Docker.

### Prerequisites

- .NET 9.0 SDK or Runtime installed on your system
- Access to the Kusto/Azure Data Explorer clusters you wish to connect to

### Building from Source

Clone the repository and build the application:

```bash
cd KustoMcp
dotnet build -c Release
```

### Running the Application

#### Option 1: Using a Configuration File

1. Create a JSON configuration file (e.g., `config.json`):

```json
{
  "Clusters": [
    "https://cluster1.kusto.windows.net",
    "https://cluster2.kusto.windows.net"
  ],
  "Credential": "Default"
}
```

2. Run the application with the config file:

```bash
dotnet run --project KustoMcp/KustoMcp.csproj -- path/to/config.json
```

Or using the published version:

```bash
cd KustoMcp/bin/Release/net9.0/publish
KustoMcp.exe path/to/config.json
```

#### Option 2: Using Environment Variables

You can set environment variables before running the application:

```bash
# Windows PowerShell
$env:KUSTO_CLUSTERS="https://cluster1.kusto.windows.net,https://cluster2.kusto.windows.net"
$env:KUSTO_CREDENTIAL="Default"
dotnet run --project KustoMcp/KustoMcp.csproj

# Linux/macOS
export KUSTO_CLUSTERS="https://cluster1.kusto.windows.net,https://cluster2.kusto.windows.net"
export KUSTO_CREDENTIAL="Default"
dotnet run --project KustoMcp/KustoMcp.csproj
```

### Publishing for Deployment

To create a deployable package:

```bash
dotnet publish -c Release -o publish KustoMcp/KustoMcp.csproj
```

This creates a publishable version in the `publish` directory that can be deployed to your server.

## Docker Support

KustoMcp can be run as a containerized application using Docker, which simplifies deployment and ensures consistent execution across different environments.

### Prerequisites

- Docker installed on your system
- Basic understanding of Kusto/Azure Data Explorer

### Building the Docker Image

From the root directory of the project, build the Docker image:

```bash
docker build -t kustomcp .
```

### Running the Container

There are multiple ways to configure and run the KustoMcp container:

#### Option 1: Using Environment Variables

Run the container by specifying Kusto clusters and credential type through environment variables:

```bash
docker run -e KUSTO_CLUSTERS="https://cluster1.kusto.windows.net,https://cluster2.kusto.windows.net" -e KUSTO_CREDENTIAL="Default" kustomcp
```

#### Option 2: Using a Configuration File

1. Create a JSON configuration file:

```json
{
  "Clusters": [
    "https://cluster1.kusto.windows.net",
    "https://cluster2.kusto.windows.net"
  ],
  "Credential": "Default"
}
```

2. Mount this file when running the container:

```bash
docker run -v /path/to/config.json:/app/config/config.json kustomcp
```

Or specify the config file path explicitly:

```bash
docker run -v /path/to/config.json:/app/custom-config.json kustomcp /app/custom-config.json
```

### Credential Types

The following credential types are supported:

- `Default` - Uses DefaultAzureCredential (recommended)
- `CLI` - Uses AzureCliCredential
- `ManagedIdentity` - Uses ManagedIdentityCredential

### Example: Running with Azure CLI Authentication

```bash
docker run -e KUSTO_CLUSTERS="https://mycluster.kusto.windows.net" \
           -e KUSTO_CREDENTIAL="CLI" \
           kustomcp
```

### Connecting to the Container

The KustoMcp server runs as a standalone executable within the container. To interact with it, you'll need to use a compatible MCP client or inspector.

### Docker Troubleshooting

#### Accessing Container Logs

To view logs from the running container:

```bash
docker logs <container_id>
```

#### Interactive Shell

To open an interactive shell in the running container:

```bash
docker exec -it <container_id> /bin/bash
```

#### Building a Custom Image

You can extend the Dockerfile to include additional tools or configurations as needed for your specific environment.
