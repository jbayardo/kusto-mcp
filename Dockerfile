# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY KustoMcp/KustoMcp.csproj KustoMcp/
RUN dotnet restore "KustoMcp/KustoMcp.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "KustoMcp/KustoMcp.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "KustoMcp/KustoMcp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage - runtime only
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create a directory for configuration
RUN mkdir -p /app/config

# Add entrypoint script
COPY docker-entrypoint.sh /app/
RUN chmod +x /app/docker-entrypoint.sh

ENTRYPOINT ["/app/docker-entrypoint.sh"]
