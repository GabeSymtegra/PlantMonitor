param(
    [int[]]$Ports = @(5265, 5173)
)

$ErrorActionPreference = "Stop"

$cleanupScript = Join-Path $PSScriptRoot "cleanup-ports.ps1"
& $cleanupScript -Ports $Ports

Write-Host "Stop routine complete."
