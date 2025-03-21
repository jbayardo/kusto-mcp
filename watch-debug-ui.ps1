if (-not (Get-Command watchexec -ErrorAction SilentlyContinue)) {
  Write-Error "watchexec is not installed. Please install it with 'cargo install watchexec-cli' or using a package manager."
  exit 1
}

$ScriptPath = Join-Path $PSScriptRoot "run-debug-ui.ps1"

Write-Host "Watching for changes... Press Ctrl+C to stop."
watchexec `
  --exts "cs,xaml,json,csproj" `
  --clear `
  -- "pwsh -File '$ScriptPath'"
