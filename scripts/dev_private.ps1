param(
	[string]$PublicHost = "0.0.0.0",
	[int]$BackendPort = 5267,
	[int]$FrontendPort = 5174,
	[string]$ProxyBackendOrigin,
	[string]$VerificationBaseUrl,
	[string]$DatabasePath,
	[switch]$SkipCleanup
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$cleanupScript = Join-Path $PSScriptRoot "cleanup-ports.ps1"
$backendProjectPath = Join-Path $repoRoot "backend\backend.csproj"

if ([string]::IsNullOrWhiteSpace($ProxyBackendOrigin)) {
	$ProxyBackendOrigin = "http://127.0.0.1:$BackendPort"
}

if ([string]::IsNullOrWhiteSpace($VerificationBaseUrl)) {
	$VerificationBaseUrl = "http://127.0.0.1:$BackendPort"
}

if ([string]::IsNullOrWhiteSpace($DatabasePath)) {
	$DatabasePath = Join-Path $repoRoot "backend\plantmonitor.private.dev.db"
}

$backendUrl = "http://$PublicHost`:$BackendPort"
$safeDbPath = $DatabasePath.Replace("'", "''")

if (-not $SkipCleanup) {
	& $cleanupScript -Ports @($BackendPort, $FrontendPort)
}

$backendCommand = "Set-Location '$repoRoot\backend'; `$env:ASPNETCORE_ENVIRONMENT='Development'; `$env:ASPNETCORE_URLS='$backendUrl'; `$env:AllowedHosts='*'; `$env:ConnectionStrings__PlantMonitor='Data Source=$safeDbPath'; dotnet run --no-launch-profile --project '$backendProjectPath'"

$frontendCommand = "Set-Location '$repoRoot'; `$env:VITE_DEV_BACKEND_ORIGIN='$ProxyBackendOrigin'; npm --prefix frontend run dev -- --host $PublicHost --port $FrontendPort"

$backendProcess = Start-Process -FilePath "powershell" -ArgumentList "-NoExit", "-Command", $backendCommand -PassThru
$frontendProcess = Start-Process -FilePath "powershell" -ArgumentList "-NoExit", "-Command", $frontendCommand -PassThru

Write-Host "Started backend terminal process id: $($backendProcess.Id)"
Write-Host "Started frontend terminal process id: $($frontendProcess.Id)"
Write-Host "Backend bind URL: $backendUrl"
Write-Host "Frontend bind URL: http://$PublicHost`:$FrontendPort"
Write-Host "Frontend proxy target: $ProxyBackendOrigin"
Write-Host "Verification target: $VerificationBaseUrl"

$backendReady = $false
for ($attempt = 1; $attempt -le 60; $attempt++) {
	try {
		$liveResponse = Invoke-WebRequest -Method Get -Uri "$VerificationBaseUrl/health/live"
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
	Write-Host "Use scripts/dev-stop.ps1 -Ports @($BackendPort, $FrontendPort) to stop listeners."
	exit 1
}

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

try {
	$loginResponse = Invoke-WebRequest -Method Post -Uri "$VerificationBaseUrl/api/auth/login" -ContentType "application/json" -Body '{"username":"test","password":"test"}' -WebSession $session
	$readyResponse = Invoke-WebRequest -Method Get -Uri "$VerificationBaseUrl/health/ready"
	$linesResponse = Invoke-WebRequest -Method Get -Uri "$VerificationBaseUrl/api/lines" -WebSession $session
	$dashboardResponse = Invoke-WebRequest -Method Get -Uri "$VerificationBaseUrl/api/dashboard" -WebSession $session
	$frontendResponse = Invoke-WebRequest -Method Get -Uri "http://127.0.0.1:$FrontendPort"

	Write-Host "Verification summary:"
	Write-Host "  POST /api/auth/login -> $($loginResponse.StatusCode)"
	Write-Host "  GET /health/ready    -> $($readyResponse.StatusCode)"
	Write-Host "  GET /api/lines       -> $($linesResponse.StatusCode)"
	Write-Host "  GET /api/dashboard   -> $($dashboardResponse.StatusCode)"
	Write-Host "  GET frontend root    -> $($frontendResponse.StatusCode)"
}
catch {
	Write-Warning "Startup verification failed: $($_.Exception.Message)"
	Write-Host "Use scripts/dev-stop.ps1 -Ports @($BackendPort, $FrontendPort) to stop listeners."
	exit 1
}

$lanIp = $null
try {
	$lanIp = Get-NetIPAddress -AddressFamily IPv4 |
		Where-Object { $_.IPAddress -match '^(10\.|172\.(1[6-9]|2[0-9]|3[0-1])\.|192\.168\.)' -and $_.IPAddress -ne '127.0.0.1' } |
		Select-Object -First 1 -ExpandProperty IPAddress
}
catch {
	$lanIp = $null
}

if (-not [string]::IsNullOrWhiteSpace($lanIp)) {
	Write-Host "LAN access URLs:"
	Write-Host "  Frontend: http://$lanIp`:$FrontendPort"
	Write-Host "  Backend:  http://$lanIp`:$BackendPort"
}

Write-Host "Use scripts/dev-stop.ps1 -Ports @($BackendPort, $FrontendPort) to stop listeners."
