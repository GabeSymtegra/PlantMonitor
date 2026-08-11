[CmdletBinding()]
param(
    [string]$ServiceName = "PlantMonitor-LAN",
    [string]$PrivilegedAgentServiceName = "PlantMonitor-PrivilegedAgent",
    [string]$InstallDirectory = "C:\Program Files\PlantMonitor",
    [string]$DataDirectory = "C:\ProgramData\PlantMonitor",
    [switch]$RemoveData,
    [string]$ConfirmRemoveData
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

Assert-Administrator

$firewallRuleName = "$ServiceName HTTP"
$removed = New-Object System.Collections.Generic.List[string]
$preserved = New-Object System.Collections.Generic.List[string]

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne "Stopped") {
        try {
            Stop-Service -Name $ServiceName -Force -ErrorAction Stop
            $removed.Add("Stopped service '$ServiceName'.") | Out-Null
        }
        catch {
            throw "Failed to stop service '$ServiceName'. $_"
        }
    }

    & sc.exe delete $ServiceName | Out-Null
    $removed.Add("Deleted service '$ServiceName'.") | Out-Null
}
else {
    $preserved.Add("Service '$ServiceName' was not present.") | Out-Null
}

$privilegedAgentService = Get-Service -Name $PrivilegedAgentServiceName -ErrorAction SilentlyContinue
if ($privilegedAgentService) {
    if ($privilegedAgentService.Status -ne "Stopped") {
        try {
            Stop-Service -Name $PrivilegedAgentServiceName -Force -ErrorAction Stop
            $removed.Add("Stopped service '$PrivilegedAgentServiceName'.") | Out-Null
        }
        catch {
            throw "Failed to stop service '$PrivilegedAgentServiceName'. $_"
        }
    }

    & sc.exe delete $PrivilegedAgentServiceName | Out-Null
    $removed.Add("Deleted service '$PrivilegedAgentServiceName'.") | Out-Null
}
else {
    $preserved.Add("Service '$PrivilegedAgentServiceName' was not present.") | Out-Null
}

$firewallRule = Get-NetFirewallRule -DisplayName $firewallRuleName -ErrorAction SilentlyContinue
if ($firewallRule) {
    Remove-NetFirewallRule -DisplayName $firewallRuleName
    $removed.Add("Removed firewall rule '$firewallRuleName'.") | Out-Null
}
else {
    $preserved.Add("Firewall rule '$firewallRuleName' was not present.") | Out-Null
}

$resolvedInstallDirectory = [System.IO.Path]::GetFullPath($InstallDirectory)
if (Test-Path -LiteralPath $resolvedInstallDirectory) {
    Remove-Item -LiteralPath $resolvedInstallDirectory -Recurse -Force
    $removed.Add("Removed installed binaries at '$resolvedInstallDirectory'.") | Out-Null
}
else {
    $preserved.Add("Install directory '$resolvedInstallDirectory' was not present.") | Out-Null
}

$resolvedDataDirectory = [System.IO.Path]::GetFullPath($DataDirectory)
if ($RemoveData) {
    if ($ConfirmRemoveData -ne "REMOVE") {
        throw "Data removal requires explicit confirmation. Re-run with -RemoveData -ConfirmRemoveData REMOVE"
    }

    if (Test-Path -LiteralPath $resolvedDataDirectory) {
        Remove-Item -LiteralPath $resolvedDataDirectory -Recurse -Force
        $removed.Add("Removed data directory '$resolvedDataDirectory'.") | Out-Null
    }
    else {
        $preserved.Add("Data directory '$resolvedDataDirectory' was not present.") | Out-Null
    }
}
else {
    $preserved.Add("Preserved data directory '$resolvedDataDirectory' (default behavior).") | Out-Null
}

Write-Host "Uninstall summary:"
Write-Host "Removed:"
if ($removed.Count -eq 0) {
    Write-Host "  (none)"
}
else {
    foreach ($entry in $removed) {
        Write-Host "  $entry"
    }
}

Write-Host "Preserved:"
if ($preserved.Count -eq 0) {
    Write-Host "  (none)"
}
else {
    foreach ($entry in $preserved) {
        Write-Host "  $entry"
    }
}
