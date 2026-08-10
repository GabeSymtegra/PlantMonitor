[CmdletBinding()]
param(
    [string]$BackendProjectPath = "backend/backend.csproj",
    [string]$Configuration = "Release",
    [string]$ReleaseDirectory = "artifacts/release/backend",
    [string]$RuntimeIdentifier = "win-x64",
    [bool]$SelfContained = $true,
    [switch]$SkipFrontendBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-External {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [string[]]$Arguments = @(),

        [string]$FailureMessage = "External command failed."
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FailureMessage Exit code: $LASTEXITCODE"
    }
}

$resolvedBackendProject = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $BackendProjectPath))
if (-not (Test-Path -LiteralPath $resolvedBackendProject)) {
    throw "Backend project not found: $resolvedBackendProject"
}

$resolvedReleaseDirectory = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $ReleaseDirectory))
if (-not (Test-Path -LiteralPath $resolvedReleaseDirectory)) {
    New-Item -ItemType Directory -Path $resolvedReleaseDirectory -Force | Out-Null
}

if (-not $SkipFrontendBuild) {
    Push-Location "frontend"
    try {
        Invoke-External -FilePath "npm" -Arguments @("run", "build") -FailureMessage "Frontend production build failed."
    }
    finally {
        Pop-Location
    }
}

$publishArguments = @(
    "publish",
    $resolvedBackendProject,
    "-c", $Configuration,
    "-o", $resolvedReleaseDirectory,
    "-r", $RuntimeIdentifier,
    "--self-contained", $SelfContained.ToString().ToLowerInvariant()
)

if ($SkipFrontendBuild) {
    $publishArguments += "/p:SkipFrontendBuild=true"
}

Invoke-External -FilePath "dotnet" -Arguments $publishArguments -FailureMessage "Hosted release publish failed."

$publishedExe = Join-Path $resolvedReleaseDirectory "backend.exe"
if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Expected published executable was not created: $publishedExe"
}

Write-Host "Hosted release published successfully: $resolvedReleaseDirectory"
Write-Host "Runtime identifier: $RuntimeIdentifier"
Write-Host "Self-contained: $SelfContained"
