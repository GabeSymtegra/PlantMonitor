[CmdletBinding()]
param(
    [string]$ReleaseDirectory = "artifacts/release/backend",
    [string]$PackageDirectory = "artifacts/packages",
    [string]$Version = "0.1.0-alpha",
    [string]$PackageName = "PlantMonitor"
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
$archiveName = "$PackageName-$Version-$stamp.zip"
$archivePath = Join-Path $resolvedPackageDir $archiveName

if (Test-Path -LiteralPath $archivePath) {
    Remove-Item -LiteralPath $archivePath -Force
}

Compress-Archive -Path (Join-Path $resolvedReleaseDir "*") -DestinationPath $archivePath

$sha256 = (Get-FileHash -Path $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = "$archivePath.sha256"
"$sha256  $archiveName" | Out-File -FilePath $checksumPath -Encoding ascii -Force

$manifestPath = Join-Path $resolvedPackageDir "$PackageName-$Version-$stamp-manifest.json"
$manifest = [ordered]@{
    packageName = $PackageName
    version = $Version
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    artifact = [ordered]@{
        fileName = $archiveName
        sha256 = $sha256
        checksumFile = [System.IO.Path]::GetFileName($checksumPath)
        sizeBytes = (Get-Item -LiteralPath $archivePath).Length
    }
}
$manifest | ConvertTo-Json -Depth 8 | Out-File -FilePath $manifestPath -Encoding utf8 -Force

Write-Host "Release package created: $archivePath"
Write-Host "Checksum file created: $checksumPath"
Write-Host "Manifest created: $manifestPath"