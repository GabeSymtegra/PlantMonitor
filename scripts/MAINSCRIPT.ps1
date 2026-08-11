[CmdletBinding()]
param(
    [switch]$SkipPublish,
    [switch]$SkipUninstall,
    [switch]$SkipInstall,
    [switch]$SkipValidation,
    [string]$JwtSigningKey,
    [string]$HostBaseUrl = "http://localhost:5050",
    [SecureString]$Password = (ConvertTo-SecureString "test" -AsPlainText -Force)
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

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-LoginCheck {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl,
        [Parameter(Mandatory = $true)]
        [string]$Username,
        [Parameter(Mandatory = $true)]
        [SecureString]$UserPassword
    )

    $plainPassword = Convert-SecureStringToPlainText -SecureValue $UserPassword

    $body = @{
        username = $Username
        password = $plainPassword
        rememberMe = $true
    } | ConvertTo-Json -Compress

    $response = Invoke-WebRequest -Method Post -Uri "$BaseUrl/api/auth/login" -ContentType "application/json" -Body $body
    if ($response.StatusCode -ne 200) {
        throw "Login check failed for '$Username'. HTTP $($response.StatusCode)"
    }

    Write-Host "PASS  login: $Username" -ForegroundColor Green
}

function Convert-SecureStringToPlainText {
    param([Parameter(Mandatory = $true)][SecureString]$SecureValue)

    $ptr = [IntPtr]::Zero
    try {
        $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    }
    finally {
        if ($ptr -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
        }
    }
}

function Show-WirelessAccessSummary {
    param([int]$Port = 5050)

    $ipv4Addresses = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -ne "127.0.0.1" -and
            $_.IPAddress -notlike "169.254.*"
        } |
        Select-Object -ExpandProperty IPAddress -Unique

    Write-Host ""
    Write-Host "Wireless/LAN access URLs:" -ForegroundColor Yellow
    if (-not $ipv4Addresses -or $ipv4Addresses.Count -eq 0) {
        Write-Host "  No non-loopback IPv4 interfaces found." -ForegroundColor Yellow
        return
    }

    foreach ($ip in $ipv4Addresses) {
        Write-Host "  http://$ip`:$Port"
    }
}

Assert-Administrator

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

$publishScript = Join-Path $scriptRoot "publish-host-release.ps1"
$uninstallScript = Join-Path $scriptRoot "uninstall-lan-service.ps1"
$installScript = Join-Path $scriptRoot "install-lan-service.ps1"
$testScript = Join-Path $scriptRoot "test-lan-service.ps1"

foreach ($requiredScript in @($publishScript, $uninstallScript, $installScript, $testScript)) {
    if (-not (Test-Path -LiteralPath $requiredScript)) {
        throw "Required script not found: $requiredScript"
    }
}

Push-Location $repoRoot
try {
    Write-Host "PlantMonitor MAINSCRIPT starting..." -ForegroundColor Cyan
    Write-Host "Repository root: $repoRoot"

    if (-not $SkipPublish) {
        Write-Step "Publishing backend + frontend + privileged agent release"
        & $publishScript
    }
    else {
        Write-Step "Skipping publish step"
    }

    if (-not $SkipUninstall) {
        Write-Step "Refreshing service install (uninstall existing services, preserve data)"
        & $uninstallScript
    }
    else {
        Write-Step "Skipping uninstall step"
    }

    if (-not $SkipInstall) {
        Write-Step "Installing LAN services"
        if ([string]::IsNullOrWhiteSpace($JwtSigningKey)) {
            & $installScript
        }
        else {
            & $installScript -JwtSigningKey $JwtSigningKey
        }
    }
    else {
        Write-Step "Skipping install step"
    }

    if (-not $SkipValidation) {
        $plainPassword = Convert-SecureStringToPlainText -SecureValue $Password

        Write-Step "Running LAN service validation"
        & $testScript

        Write-Step "Running credential/session validation with operator account"
        & $testScript -Username "operator" -Password $plainPassword

        Write-Step "Running direct login checks for admin/operator/viewer"
        Invoke-LoginCheck -BaseUrl $HostBaseUrl -Username "admin" -UserPassword $Password
        Invoke-LoginCheck -BaseUrl $HostBaseUrl -Username "operator" -UserPassword $Password
        Invoke-LoginCheck -BaseUrl $HostBaseUrl -Username "viewer" -UserPassword $Password
    }
    else {
        Write-Step "Skipping validation step"
    }

    Show-WirelessAccessSummary -Port 5050

    Write-Host ""
    Write-Host "MAINSCRIPT completed successfully." -ForegroundColor Green
    Write-Host "Open: $HostBaseUrl"
}
finally {
    Pop-Location
}
