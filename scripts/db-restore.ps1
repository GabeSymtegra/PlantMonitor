[CmdletBinding()]
param(
    [string]$BackupFile,
    [string]$DatabasePath
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
$backupRoot = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) "artifacts/backups"))

if ([string]::IsNullOrWhiteSpace($BackupFile)) {
    if (-not (Test-Path -LiteralPath $backupRoot)) {
        throw "Backup directory not found: $backupRoot"
    }

    $latest = Get-ChildItem -Path $backupRoot -Filter "plantmonitor-*.db" -File |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $latest) {
        throw "No backup files found in $backupRoot"
    }

    $BackupFile = $latest.FullName
}

$resolvedBackupPath = [System.IO.Path]::GetFullPath($BackupFile)
if (-not (Test-Path -LiteralPath $resolvedBackupPath)) {
    throw "Backup file not found: $resolvedBackupPath"
}

$dbDir = Split-Path -Path $resolvedDbPath -Parent
if (-not (Test-Path -LiteralPath $dbDir)) {
    New-Item -ItemType Directory -Path $dbDir | Out-Null
}

# Ensure operator stops running PlantMonitor service/process before restore.
Copy-Item -LiteralPath $resolvedBackupPath -Destination $resolvedDbPath -Force
Write-Host "Database restored from $resolvedBackupPath to $resolvedDbPath"
