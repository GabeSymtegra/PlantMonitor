param(
    [string]$BackendUrl = "http://localhost:5265",
    [string]$FrontendHost = "127.0.0.1",
    [int]$FrontendPort = 5173,
    [switch]$SkipCleanup
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$cleanupScript = Join-Path $PSScriptRoot "cleanup-ports.ps1"

if (-not $SkipCleanup) {
    & $cleanupScript -Ports @(5265, $FrontendPort)
}

$backendCommand = "Set-Location '$repoRoot\\backend'; dotnet run --no-launch-profile --urls $BackendUrl"
$frontendCommand = "Set-Location '$repoRoot'; npm --prefix frontend run dev -- --host $FrontendHost --port $FrontendPort"

$backendProcess = Start-Process -FilePath "powershell" -ArgumentList "-NoExit", "-Command", $backendCommand -PassThru
$frontendProcess = Start-Process -FilePath "powershell" -ArgumentList "-NoExit", "-Command", $frontendCommand -PassThru

Write-Host "Started backend terminal process id: $($backendProcess.Id)"
Write-Host "Started frontend terminal process id: $($frontendProcess.Id)"

$reachable = $false
for ($attempt = 1; $attempt -le 20; $attempt++) {
    try {
        $response = Invoke-WebRequest -Method Post -Uri "$BackendUrl/api/auth/login" -ContentType "application/json" -Body '{"username":"test","password":"test"}'
        if ($response.StatusCode -eq 200) {
            $reachable = $true
            break
        }
    }
    catch {
        Start-Sleep -Seconds 1
    }
}

if ($reachable) {
    Write-Host "Backend reachable at $BackendUrl"
} else {
    Write-Warning "Backend did not become reachable within timeout."
}

Write-Host "Frontend expected at http://$FrontendHost`:$FrontendPort"
Write-Host "Use scripts/dev-stop.ps1 to stop listeners."
