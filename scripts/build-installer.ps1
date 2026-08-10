[CmdletBinding()]
param(
    [string]$BackendProjectPath = "backend/backend.csproj",
    [string]$Configuration = "Release",
    [string]$ReleaseDirectory = "artifacts/release/backend",
    [string]$InstallerScriptPath = "installer/PlantMonitor.iss",
    [string]$InstallerOutputDirectory = "artifacts/installer",
    [string]$RuntimeIdentifier = "win-x64",
    [bool]$SelfContained = $true,
    [switch]$SkipFrontendBuild,
    [switch]$SkipBackendPublish
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

function Resolve-IsccPath {
    $fromCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($fromCommand) {
        return $fromCommand.Source
    }

    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "ISCC.exe was not found. Install Inno Setup 6 and re-run this script."
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

if (-not $SkipBackendPublish) {
    Invoke-External -FilePath "dotnet" -Arguments @(
        "publish",
        $BackendProjectPath,
        "-c", $Configuration,
        "-o", $ReleaseDirectory,
        "-r", $RuntimeIdentifier,
        "--self-contained", $SelfContained.ToString().ToLowerInvariant()
    ) -FailureMessage "Backend publish for installer failed."
}

$resolvedReleaseDir = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $ReleaseDirectory))
if (-not (Test-Path -LiteralPath $resolvedReleaseDir)) {
    throw "Release directory not found: $resolvedReleaseDir"
}

$resolvedInstallerScript = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $InstallerScriptPath))
if (-not (Test-Path -LiteralPath $resolvedInstallerScript)) {
    throw "Installer script not found: $resolvedInstallerScript"
}

$resolvedOutputDir = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $InstallerOutputDirectory))
if (-not (Test-Path -LiteralPath $resolvedOutputDir)) {
    New-Item -ItemType Directory -Path $resolvedOutputDir | Out-Null
}

$isccPath = Resolve-IsccPath

$env:PM_SOURCE_DIR = $resolvedReleaseDir
$env:PM_OUTPUT_DIR = $resolvedOutputDir

try {
    Invoke-External -FilePath $isccPath -Arguments @($resolvedInstallerScript) -FailureMessage "Installer compilation failed."
}
finally {
    Remove-Item Env:PM_SOURCE_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:PM_OUTPUT_DIR -ErrorAction SilentlyContinue
}

Write-Host "Installer build completed. Output: $resolvedOutputDir"