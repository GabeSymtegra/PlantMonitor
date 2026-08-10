[CmdletBinding()]
param(
    [string]$ServiceName = "PlantMonitor-LAN",
    [string]$InstallDirectory = "C:\Program Files\PlantMonitor",
    [string]$DataDirectory = "C:\ProgramData\PlantMonitor",
    [int]$Port = 5050,
    [string]$ReleaseDirectory = "artifacts\release\backend",
    [switch]$UseInstalledFiles,
    [string]$ExistingDatabasePath,
    [string]$EnvironmentName = "Production",
    [string]$AllowedHosts = "*",
    [string]$JwtSigningKey,
    [string]$BootstrapAdminPassword,
    [string]$BootstrapViewerPassword,
    [string]$BootstrapViewerUsername = "viewer"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Administrator privileges are required. Open PowerShell as Administrator and retry."
    }
}

function Assert-PortValid {
    param([int]$Candidate)

    if ($Candidate -lt 1 -or $Candidate -gt 65535) {
        throw "Port must be between 1 and 65535. Received: $Candidate"
    }
}

function Assert-PrivateNetworkProfile {
    $profiles = Get-NetConnectionProfile -ErrorAction Stop | Where-Object {
        $_.IPv4Connectivity -ne "Disconnected" -or $_.IPv6Connectivity -ne "Disconnected"
    }

    if ($profiles.Count -eq 0) {
        throw "Unable to detect an active network profile. Connect to the wired private LAN and retry."
    }

    $publicProfiles = $profiles | Where-Object { $_.NetworkCategory -eq "Public" }
    if ($publicProfiles.Count -gt 0) {
        $names = ($publicProfiles | ForEach-Object { $_.Name }) -join ", "
        throw "Active network profile is Public ($names). Change the host network profile to Private before installing."
    }
}

function Assert-PortAvailable {
    param([int]$Candidate)

    $listeners = Get-NetTCPConnection -State Listen -LocalPort $Candidate -ErrorAction SilentlyContinue
    if ($listeners) {
        $pids = ($listeners | Select-Object -ExpandProperty OwningProcess -Unique) -join ", "
        throw "Port $Candidate is already in use by process id(s): $pids"
    }
}

function Test-DatabaseUnlocked {
    param([string]$DatabaseFile)

    $stream = $null
    try {
        $stream = [System.IO.File]::Open($DatabaseFile, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::None)
        return $true
    }
    catch {
        return $false
    }
    finally {
        if ($stream) {
            $stream.Dispose()
        }
    }
}

function Wait-HealthEndpoint {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 90
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                return $true
            }
        }
        catch {
        }

        Start-Sleep -Seconds 2
    }

    return $false
}

function Set-ServiceEnvironment {
    param(
        [string]$Name,
        [string[]]$EnvironmentRows
    )

    $serviceKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$Name"
    if (-not (Test-Path -LiteralPath $serviceKey)) {
        throw "Service registry key not found: $serviceKey"
    }

    New-ItemProperty -Path $serviceKey -Name Environment -Value $EnvironmentRows -PropertyType MultiString -Force | Out-Null
}

function Configure-Recovery {
    param([string]$Name)

    & sc.exe failure $Name reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
    & sc.exe failureflag $Name 1 | Out-Null
}

function New-GeneratedJwtSigningKey {
    return [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
}

Assert-Administrator
Assert-PortValid -Candidate $Port
Assert-PrivateNetworkProfile
Assert-PortAvailable -Candidate $Port

$resolvedInstallDirectory = [System.IO.Path]::GetFullPath($InstallDirectory)
$resolvedDataDirectory = [System.IO.Path]::GetFullPath($DataDirectory)

$resolvedReleaseDirectory = if ($UseInstalledFiles) {
    $resolvedInstallDirectory
}
else {
    [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $ReleaseDirectory))
}

if (-not (Test-Path -LiteralPath $resolvedReleaseDirectory)) {
    throw "Release directory not found: $resolvedReleaseDirectory. Publish the backend-hosted release first."
}

$sourceExe = Join-Path $resolvedReleaseDirectory "backend.exe"
if (-not (Test-Path -LiteralPath $sourceExe)) {
    throw "Published executable missing: $sourceExe"
}

if ([string]::IsNullOrWhiteSpace($JwtSigningKey)) {
    $JwtSigningKey = New-GeneratedJwtSigningKey
}

$dataSubdirectories = @(
    (Join-Path $resolvedDataDirectory "Data"),
    (Join-Path $resolvedDataDirectory "Logs"),
    (Join-Path $resolvedDataDirectory "Secrets"),
    (Join-Path $resolvedDataDirectory "Backups")
)

foreach ($directory in $dataSubdirectories) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$targetDatabasePath = Join-Path $resolvedDataDirectory "Data\plantmonitor.db"
$backupDirectory = Join-Path $resolvedDataDirectory "Backups"
$existingService = Get-CimInstance -ClassName Win32_Service -Filter "Name='$ServiceName'" -ErrorAction SilentlyContinue

if ($existingService) {
    try {
        Stop-Service -Name $ServiceName -Force -ErrorAction Stop
    }
    catch {
        throw "Service '$ServiceName' already exists and could not be stopped for upgrade. $_"
    }
}

if ($ExistingDatabasePath) {
    $resolvedExistingDatabasePath = [System.IO.Path]::GetFullPath($ExistingDatabasePath)
    if (-not (Test-Path -LiteralPath $resolvedExistingDatabasePath)) {
        throw "ExistingDatabasePath does not exist: $resolvedExistingDatabasePath"
    }

    if (-not (Test-DatabaseUnlocked -DatabaseFile $resolvedExistingDatabasePath)) {
        throw "The source database appears to be in use. Stop the currently running PlantMonitor process before copying the SQLite database."
    }

    if (Test-Path -LiteralPath $targetDatabasePath) {
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $backupPath = Join-Path $backupDirectory "plantmonitor-$timestamp.db"
        Copy-Item -LiteralPath $targetDatabasePath -Destination $backupPath -Force
    }

    Copy-Item -LiteralPath $resolvedExistingDatabasePath -Destination $targetDatabasePath -Force
}

if (-not $UseInstalledFiles) {
    if (Test-Path -LiteralPath $resolvedInstallDirectory) {
        Remove-Item -LiteralPath $resolvedInstallDirectory -Recurse -Force
    }

    New-Item -ItemType Directory -Path $resolvedInstallDirectory -Force | Out-Null
    Copy-Item -Path (Join-Path $resolvedReleaseDirectory "*") -Destination $resolvedInstallDirectory -Recurse -Force
}

$installedExe = Join-Path $resolvedInstallDirectory "backend.exe"
$serviceScriptsDirectory = Join-Path $resolvedInstallDirectory "scripts"
$installedTestScript = Join-Path $serviceScriptsDirectory "test-lan-service.ps1"
$installedUninstallScript = Join-Path $serviceScriptsDirectory "uninstall-lan-service.ps1"
$serviceDisplayName = "PlantMonitor LAN Host"
$serviceAccount = "NT AUTHORITY\LocalService"
$firewallRuleName = "$ServiceName HTTP"

& icacls $resolvedDataDirectory /grant "NT AUTHORITY\LOCAL SERVICE:(OI)(CI)M" /T | Out-Null

$binaryPath = '"' + $installedExe + '"'
if ($existingService) {
    & sc.exe config $ServiceName binPath= $binaryPath obj= "$serviceAccount" start= auto DisplayName= "$serviceDisplayName" | Out-Null
}
else {
    & sc.exe create $ServiceName binPath= $binaryPath obj= "$serviceAccount" start= auto DisplayName= "$serviceDisplayName" | Out-Null
}

Set-Service -Name $ServiceName -StartupType Automatic
& sc.exe config $ServiceName start= delayed-auto | Out-Null
Configure-Recovery -Name $ServiceName

$connectionString = "Data Source=$targetDatabasePath;Cache=Shared"
$serviceEnvironment = @(
    "ASPNETCORE_ENVIRONMENT=$EnvironmentName",
    "ASPNETCORE_URLS=http://0.0.0.0:$Port",
    "AllowedHosts=$AllowedHosts",
    "ConnectionStrings__PlantMonitor=$connectionString",
    "App__DatabasePath=$targetDatabasePath",
    "App__IsLanDeployment=true"
)

if (-not [string]::IsNullOrWhiteSpace($JwtSigningKey)) {
    $serviceEnvironment += "Jwt__SigningKey=$JwtSigningKey"
}

if (-not [string]::IsNullOrWhiteSpace($BootstrapAdminPassword)) {
    $serviceEnvironment += "Auth__BootstrapAdminPassword=$BootstrapAdminPassword"
}

if (-not [string]::IsNullOrWhiteSpace($BootstrapViewerPassword)) {
    $serviceEnvironment += "Auth__BootstrapViewerPassword=$BootstrapViewerPassword"
    $serviceEnvironment += "Auth__BootstrapViewerUsername=$BootstrapViewerUsername"
}

Set-ServiceEnvironment -Name $ServiceName -EnvironmentRows $serviceEnvironment

$existingRule = Get-NetFirewallRule -DisplayName $firewallRuleName -ErrorAction SilentlyContinue
if ($existingRule) {
    Remove-NetFirewallRule -DisplayName $firewallRuleName
}

New-NetFirewallRule -DisplayName $firewallRuleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -Profile Private | Out-Null

Start-Service -Name $ServiceName

$liveUrl = "http://localhost:$Port/health/live"
$readyUrl = "http://localhost:$Port/health/ready"
$liveHealthy = Wait-HealthEndpoint -Url $liveUrl
$readyHealthy = Wait-HealthEndpoint -Url $readyUrl

if (-not $liveHealthy -or -not $readyHealthy) {
    Write-Error "Service startup validation failed."
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        Write-Host "Service status: $($service.Status)"
    }

    throw "Service did not report healthy within timeout."
}

$serviceState = Get-Service -Name $ServiceName
$ipv4Addresses = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object {
        $_.IPAddress -ne "127.0.0.1" -and
        $_.IPAddress -notlike "169.254.*"
    } |
    Select-Object -ExpandProperty IPAddress -Unique

Write-Host ""
Write-Host "Installation successful."
Write-Host "Service status: $($serviceState.Status)"
Write-Host "Local URL: http://localhost:$Port"
foreach ($ip in $ipv4Addresses) {
    Write-Host "LAN URL example: http://$ip`:$Port"
}
Write-Host "Database path: $targetDatabasePath"
if (Test-Path -LiteralPath $installedTestScript) {
    Write-Host "Validate service: powershell -ExecutionPolicy Bypass -File `"$installedTestScript`" -ServiceName `"$ServiceName`" -Port $Port"
}
if (Test-Path -LiteralPath $installedUninstallScript) {
    Write-Host "Uninstall service: powershell -ExecutionPolicy Bypass -File `"$installedUninstallScript`" -ServiceName `"$ServiceName`" -InstallDirectory `"$resolvedInstallDirectory`" -DataDirectory `"$resolvedDataDirectory`""
}