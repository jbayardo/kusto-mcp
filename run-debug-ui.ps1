param(
  [Parameter(Mandatory = $false)]
  [string[]]$Clusters = @("https://cbuild.kusto.windows.net", "https://ctest.kusto.windows.net"),
    
  [Parameter(Mandatory = $false)]
  [string]$Credential = "Default"
)

$Config = @{
  Clusters   = $Clusters
  Credential = $Credential
}

$RandomPath = [System.IO.Path]::GetRandomFileName()
$ConfigPath = Join-Path $env:TEMP $RandomPath
$Config | ConvertTo-Json | Set-Content -Path $ConfigPath
$ConfigPath = $ConfigPath.Replace("\", "/")
try {
  $SourceRoot = (Get-Item -Path $PSScriptRoot).FullName
  $ProjectPath = Join-Path $SourceRoot "KustoMcp"
  Push-Location $ProjectPath
  try {
    Write-Host "Root: $SourceRoot"
    Write-Host "Project: $ProjectPath"

    Write-Host "Building KustoMcp..."
    dotnet build
  
    Write-Host "Running MCP Inspector"
    $ExePath = Join-Path $ProjectPath "bin/Debug/net9.0/KustoMcp.exe"
    $ExePath = Resolve-Path $ExePath
    npx @modelcontextprotocol/inspector $ExePath -- $ConfigPath
  }
  finally {
    Pop-Location
  }
}
finally {
  Remove-Item -Path $ConfigPath -Force
}
