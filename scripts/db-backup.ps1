[CmdletBinding()]
param(
    [string]$DatabasePath,
    [string]$BackupDirectory = "artifacts/backups",
    [int]$RetentionDays = 14
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-DefaultDatabasePath {
    $programData = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)
    return Join-Path $programData "PlantMonitor/Data/plantmonitor.db"
}

if ([string]::IsNullOrWhiteSpace($DatabasePath)) {
    $DatabasePath = Resolve-DefaultDatabasePath
}

$resolvedDbPath = [System.IO.Path]::GetFullPath($DatabasePath)
if (-not (Test-Path -LiteralPath $resolvedDbPath)) {
    throw "Database file not found: $resolvedDbPath"
}

$backupRoot = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $BackupDirectory))
if (-not (Test-Path -LiteralPath $backupRoot)) {
    New-Item -ItemType Directory -Path $backupRoot | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupName = "plantmonitor-$timestamp.db"
$backupPath = Join-Path $backupRoot $backupName

Copy-Item -LiteralPath $resolvedDbPath -Destination $backupPath -Force

# Retention policy cleanup keeps only recent snapshots.
$cutoff = (Get-Date).AddDays(-1 * [Math]::Abs($RetentionDays))
Get-ChildItem -Path $backupRoot -Filter "plantmonitor-*.db" -File |
    Where-Object { $_.LastWriteTimeUtc -lt $cutoff.ToUniversalTime() } |
    Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "Backup created: $backupPath"
