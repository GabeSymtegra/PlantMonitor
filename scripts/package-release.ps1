[CmdletBinding()]
param(
    [string]$ReleaseDirectory = "artifacts/release/backend",
    [string]$PackageDirectory = "artifacts/packages",
    [string]$Version = "0.1.0-alpha"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resolvedReleaseDir = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $ReleaseDirectory))
if (-not (Test-Path -LiteralPath $resolvedReleaseDir)) {
    throw "Release directory not found: $resolvedReleaseDir"
}

$resolvedPackageDir = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $PackageDirectory))
if (-not (Test-Path -LiteralPath $resolvedPackageDir)) {
    New-Item -ItemType Directory -Path $resolvedPackageDir | Out-Null
}

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$archiveName = "PlantMonitor-$Version-$stamp.zip"
$archivePath = Join-Path $resolvedPackageDir $archiveName

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

Compress-Archive -Path (Join-Path $resolvedReleaseDir "*") -DestinationPath $archivePath

Write-Host "Release package created: $archivePath"