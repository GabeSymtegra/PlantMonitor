[CmdletBinding()]
param(
    [string]$BackendProjectPath = "backend/backend.csproj",
    [string]$AgentProjectPath = "plc-service/plc-service.csproj",
    [string]$Configuration = "Release",
    [string]$ReleaseDirectory = "artifacts/release/backend",
    [string]$AgentReleaseSubdirectory = "privileged-agent",
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

$resolvedAgentProject = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $AgentProjectPath))
if (-not (Test-Path -LiteralPath $resolvedAgentProject)) {
    throw "Privileged agent project not found: $resolvedAgentProject"
}

$resolvedReleaseDirectory = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $ReleaseDirectory))
if (-not (Test-Path -LiteralPath $resolvedReleaseDirectory)) {
    New-Item -ItemType Directory -Path $resolvedReleaseDirectory -Force | Out-Null
}

$resolvedAgentReleaseDirectory = Join-Path $resolvedReleaseDirectory $AgentReleaseSubdirectory
if (Test-Path -LiteralPath $resolvedAgentReleaseDirectory) {
    Remove-Item -LiteralPath $resolvedAgentReleaseDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $resolvedAgentReleaseDirectory -Force | Out-Null

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

$agentPublishArguments = @(
    "publish",
    $resolvedAgentProject,
    "-c", $Configuration,
    "-o", $resolvedAgentReleaseDirectory,
    "-r", $RuntimeIdentifier,
    "--self-contained", $SelfContained.ToString().ToLowerInvariant()
)

Invoke-External -FilePath "dotnet" -Arguments $agentPublishArguments -FailureMessage "Privileged agent publish failed."

$publishedExe = Join-Path $resolvedReleaseDirectory "backend.exe"
if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Expected published executable was not created: $publishedExe"
}

$publishedAgentExe = Join-Path $resolvedAgentReleaseDirectory "plc-service.exe"
if (-not (Test-Path -LiteralPath $publishedAgentExe)) {
    throw "Expected privileged agent executable was not created: $publishedAgentExe"
}

Write-Host "Hosted release published successfully: $resolvedReleaseDirectory"
Write-Host "Privileged agent published successfully: $resolvedAgentReleaseDirectory"
Write-Host "Runtime identifier: $RuntimeIdentifier"
Write-Host "Self-contained: $SelfContained"
