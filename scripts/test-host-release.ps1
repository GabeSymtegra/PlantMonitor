[CmdletBinding()]
param(
    [string]$ReleaseDirectory = "artifacts/release/backend",
    [int]$Port = 5050,
    [string]$EnvironmentName = "Development",
    [string]$DatabasePath = "artifacts/release/test-host-release.db",
    [string]$JwtSigningKey,
    [switch]$SkipFrontendRootCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Wait-Endpoint {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 60
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

        Start-Sleep -Seconds 1
    }

    return $false
}

$resolvedReleaseDirectory = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $ReleaseDirectory))
if (-not (Test-Path -LiteralPath $resolvedReleaseDirectory)) {
    throw "Release directory not found: $resolvedReleaseDirectory"
}

$publishedExe = Join-Path $resolvedReleaseDirectory "backend.exe"
if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Published executable missing: $publishedExe"
}

$resolvedDatabasePath = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $DatabasePath))
$databaseDirectory = Split-Path -Parent $resolvedDatabasePath
if (-not (Test-Path -LiteralPath $databaseDirectory)) {
    New-Item -ItemType Directory -Path $databaseDirectory -Force | Out-Null
}

$envRows = @(
    "ASPNETCORE_ENVIRONMENT=$EnvironmentName",
    "ASPNETCORE_URLS=http://127.0.0.1:$Port",
    "AllowedHosts=*",
    "ConnectionStrings__PlantMonitor=Data Source=$resolvedDatabasePath",
    "App__DatabasePath=$resolvedDatabasePath",
    "App__IsLanDeployment=true"
)

if (-not [string]::IsNullOrWhiteSpace($JwtSigningKey)) {
    $envRows += "Jwt__SigningKey=$JwtSigningKey"
}

$process = $null
try {
    $commandParts = $envRows | ForEach-Object {
        $pair = $_.Split('=', 2)
        "`$env:{0}='{1}'" -f $pair[0], $pair[1].Replace("'", "''")
    }
    $commandParts += "Set-Location '$resolvedReleaseDirectory'"
    $commandParts += "& '$publishedExe'"
    $command = $commandParts -join "; "

    $process = Start-Process -FilePath "powershell" -ArgumentList "-NoProfile", "-Command", $command -PassThru

    $baseUrl = "http://127.0.0.1:$Port"
    if (-not (Wait-Endpoint -Url "$baseUrl/health/live")) {
        throw "Hosted release did not become live within timeout."
    }

    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $loginResponse = Invoke-WebRequest -Method Post -Uri "$baseUrl/api/auth/login" -ContentType "application/json" -Body '{"username":"test","password":"test"}' -WebSession $session
    $readyResponse = Invoke-WebRequest -Method Get -Uri "$baseUrl/health/ready"
    $linesResponse = Invoke-WebRequest -Method Get -Uri "$baseUrl/api/lines" -WebSession $session
    $dashboardResponse = Invoke-WebRequest -Method Get -Uri "$baseUrl/api/dashboard" -WebSession $session

    Write-Host "Hosted release verification summary:"
    Write-Host "  POST /api/auth/login -> $($loginResponse.StatusCode)"
    Write-Host "  GET /health/ready    -> $($readyResponse.StatusCode)"
    Write-Host "  GET /api/lines       -> $($linesResponse.StatusCode)"
    Write-Host "  GET /api/dashboard   -> $($dashboardResponse.StatusCode)"

    if (-not $SkipFrontendRootCheck) {
        $rootResponse = Invoke-WebRequest -Method Get -Uri $baseUrl
        Write-Host "  GET /                -> $($rootResponse.StatusCode)"
    }
}
finally {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
    }
}
