param(
    [string]$BackendUrl = "http://127.0.0.1:5265",
    [string]$FrontendHost = "127.0.0.1",
    [int]$FrontendPort = 5173,
    [switch]$Isolated,
    [string]$IsolatedDatabasePath,
    [switch]$SkipCleanup
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$cleanupScript = Join-Path $PSScriptRoot "cleanup-ports.ps1"
$backendProjectPath = Join-Path $repoRoot "backend\backend.csproj"

if ($Isolated -and -not $PSBoundParameters.ContainsKey("BackendUrl")) {
    $BackendUrl = "http://localhost:5266"
}

$backendUri = [uri]$BackendUrl
$backendPort = $backendUri.Port

if ($Isolated) {
    if ([string]::IsNullOrWhiteSpace($IsolatedDatabasePath)) {
        $IsolatedDatabasePath = Join-Path $repoRoot "backend\plantmonitor.dev.db"
    }
}

if (-not $SkipCleanup) {
    & $cleanupScript -Ports @($backendPort, $FrontendPort)
}

if ($Isolated) {
    $safeDbPath = $IsolatedDatabasePath.Replace("'", "''")
    $backendCommand = "Set-Location '$repoRoot\\backend'; `$env:ASPNETCORE_ENVIRONMENT='Development'; `$env:ASPNETCORE_URLS='$BackendUrl'; `$env:ConnectionStrings__PlantMonitor='Data Source=$safeDbPath'; dotnet run --no-launch-profile --project '$backendProjectPath'"
}
else {
    $backendCommand = "Set-Location '$repoRoot\\backend'; dotnet run --no-launch-profile --urls $BackendUrl"
}

$frontendCommand = "Set-Location '$repoRoot'; `$env:VITE_DEV_BACKEND_ORIGIN='$BackendUrl'; npm --prefix frontend run dev -- --host $FrontendHost --port $FrontendPort"

$backendProcess = Start-Process -FilePath "powershell" -ArgumentList "-NoExit", "-Command", $backendCommand -PassThru
$frontendProcess = Start-Process -FilePath "powershell" -ArgumentList "-NoExit", "-Command", $frontendCommand -PassThru

Write-Host "Started backend terminal process id: $($backendProcess.Id)"
Write-Host "Started frontend terminal process id: $($frontendProcess.Id)"
Write-Host "Backend target URL: $BackendUrl"
Write-Host "Frontend expected URL: http://$FrontendHost`:$FrontendPort"

$backendReady = $false
for ($attempt = 1; $attempt -le 45; $attempt++) {
    try {
        $liveResponse = Invoke-WebRequest -Method Get -Uri "$BackendUrl/health/live"
        if ($liveResponse.StatusCode -eq 200) {
            $backendReady = $true
            break
        }
    }
    catch {
        Start-Sleep -Seconds 1
    }
}

if (-not $backendReady) {
    Write-Warning "Backend did not become reachable within timeout."
    Write-Host "Use scripts/dev-stop.ps1 to stop listeners."
    exit 1
}

Write-Host "Backend health check passed: GET /health/live"

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

try {
    $loginResponse = Invoke-WebRequest -Method Post -Uri "$BackendUrl/api/auth/login" -ContentType "application/json" -Body '{"username":"test","password":"test"}' -WebSession $session
    $readyResponse = Invoke-WebRequest -Method Get -Uri "$BackendUrl/health/ready"
    $linesResponse = Invoke-WebRequest -Method Get -Uri "$BackendUrl/api/lines" -WebSession $session
    $dashboardResponse = Invoke-WebRequest -Method Get -Uri "$BackendUrl/api/dashboard" -WebSession $session

    $frontendCheckHost = if ($FrontendHost -eq "0.0.0.0") { "127.0.0.1" } else { $FrontendHost }
    $frontendResponse = Invoke-WebRequest -Method Get -Uri "http://$frontendCheckHost`:$FrontendPort"

    Write-Host "Verification summary:"
    Write-Host "  POST /api/auth/login -> $($loginResponse.StatusCode)"
    Write-Host "  GET /health/ready    -> $($readyResponse.StatusCode)"
    Write-Host "  GET /api/lines       -> $($linesResponse.StatusCode)"
    Write-Host "  GET /api/dashboard   -> $($dashboardResponse.StatusCode)"
    Write-Host "  GET frontend root    -> $($frontendResponse.StatusCode)"
}
catch {
    Write-Warning "Startup verification failed: $($_.Exception.Message)"
    Write-Host "Use scripts/dev-stop.ps1 to stop listeners."
    exit 1
}

Write-Host "Use scripts/dev-stop.ps1 to stop listeners."
