#!/bin/bash
set -e

# Default config file path
CONFIG_FILE="/app/config/config.json"

# If environment variables are provided, generate a config file
if [ -n "$KUSTO_CLUSTERS" ]; then
  # Convert comma-separated clusters to JSON array
  IFS=',' read -ra CLUSTER_ARRAY <<< "$KUSTO_CLUSTERS"
  CLUSTERS_JSON="["
  for i in "${!CLUSTER_ARRAY[@]}"; do
    if [ $i -gt 0 ]; then
      CLUSTERS_JSON+=","
    fi
    CLUSTERS_JSON+="\"${CLUSTER_ARRAY[$i]}\""
  done
  CLUSTERS_JSON+="]"
  
  # Set default credential if not provided
  CREDENTIAL=${KUSTO_CREDENTIAL:-"Default"}
  
  # Create JSON config
  echo "{\"Clusters\":$CLUSTERS_JSON,\"Credential\":\"$CREDENTIAL\"}" > $CONFIG_FILE
elif [ "$#" -gt 0 ]; then
  # If a config file is passed as an argument, use that
  CONFIG_FILE="$1"
fi

# Run the .NET application with the config file
dotnet KustoMcp.dll "$CONFIG_FILE"
