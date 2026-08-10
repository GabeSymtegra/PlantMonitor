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

function Get-ConnectionStringDataSource {
    param(
        [string]$ConnectionString
    )

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        return $null
    }

    $builder = New-Object System.Data.Common.DbConnectionStringBuilder
    try {
        $builder.ConnectionString = $ConnectionString
    }
    catch {
        return $null
    }

    if ($builder.ContainsKey("Data Source")) {
        return [string]$builder["Data Source"]
    }

    return $null
}

function Resolve-RegularModeDatabasePath {
    param(
        [string]$RepositoryRoot
    )

    $configuredConnection = [Environment]::GetEnvironmentVariable("ConnectionStrings__PlantMonitor")
    $dataSource = Get-ConnectionStringDataSource -ConnectionString $configuredConnection

    if ([string]::IsNullOrWhiteSpace($dataSource)) {
        $appsettingsPath = Join-Path $RepositoryRoot "backend\appsettings.json"
        if (Test-Path $appsettingsPath) {
            try {
                $appsettings = Get-Content -Path $appsettingsPath -Raw | ConvertFrom-Json
                $defaultConnection = $appsettings.ConnectionStrings.PlantMonitor
                $dataSource = Get-ConnectionStringDataSource -ConnectionString $defaultConnection
            }
            catch {
                $dataSource = $null
            }
        }
    }

    if ([string]::IsNullOrWhiteSpace($dataSource) -or $dataSource -eq ":memory:") {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($dataSource)) {
        return [System.IO.Path]::GetFullPath($dataSource)
    }

    $programData = [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)
    $dataDir = Join-Path $programData "PlantMonitor\Data"
    return [System.IO.Path]::GetFullPath((Join-Path $dataDir $dataSource))
}

function Test-DatabaseLockAvailability {
    param(
        [string]$DatabasePath
    )

    if ([string]::IsNullOrWhiteSpace($DatabasePath)) {
        return @{ IsAvailable = $true; LockFilePath = $null }
    }

    $lockDirectory = Join-Path ([System.IO.Path]::GetDirectoryName($DatabasePath)) ".locks"
    if (-not (Test-Path $lockDirectory)) {
        New-Item -ItemType Directory -Path $lockDirectory -Force | Out-Null
    }

    $hashBytes = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($DatabasePath))
    $hashPrefix = ([System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLowerInvariant()).Substring(0, 16)
    $lockFilePath = Join-Path $lockDirectory "plantmonitor-$hashPrefix.lck"

    try {
        $stream = [System.IO.File]::Open($lockFilePath, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $stream.Dispose()
        return @{ IsAvailable = $true; LockFilePath = $lockFilePath }
    }
    catch {
        return @{ IsAvailable = $false; LockFilePath = $lockFilePath; Error = $_.Exception.Message }
    }
}

function Show-LockDiagnostics {
    param(
        [int]$Port
    )

    try {
        $listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $listener) {
            Write-Host "Listener on port ${Port}: PID $($listener.OwningProcess)"
            $owner = Get-CimInstance Win32_Process -Filter "ProcessId=$($listener.OwningProcess)" -ErrorAction SilentlyContinue
            if ($null -ne $owner) {
                Write-Host "  $($owner.Name): $($owner.CommandLine)"
            }
        }
    }
    catch {
    }

    try {
        $backendProcesses = Get-CimInstance Win32_Process -Filter "Name='backend.exe'" -ErrorAction SilentlyContinue
        if ($backendProcesses) {
            Write-Host "Running backend.exe processes:"
            foreach ($process in $backendProcesses) {
                Write-Host "  PID $($process.ProcessId): $($process.CommandLine)"
            }
        }
    }
    catch {
    }

    try {
        $services = Get-Service | Where-Object { $_.Name -like '*PlantMonitor*' -or $_.DisplayName -like '*PlantMonitor*' }
        if ($services) {
            Write-Host "PlantMonitor services:"
            foreach ($service in $services) {
                Write-Host "  $($service.Status): $($service.Name) ($($service.DisplayName))"
            }
        }
    }
    catch {
    }
}

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

if (-not $Isolated) {
    $databasePath = Resolve-RegularModeDatabasePath -RepositoryRoot $repoRoot
    $lockCheck = Test-DatabaseLockAvailability -DatabasePath $databasePath
    if (-not $lockCheck.IsAvailable) {
        Write-Warning "Regular mode is blocked by a PlantMonitor database lock."
        Write-Host "Database path: $databasePath"
        Write-Host "Lock file: $($lockCheck.LockFilePath)"
        if (-not [string]::IsNullOrWhiteSpace($lockCheck.Error)) {
            Write-Host "Lock error: $($lockCheck.Error)"
        }
        Show-LockDiagnostics -Port $backendPort
        Write-Host "Use scripts/dev-stop.ps1 and retry, or run scripts/dev.ps1 -Isolated"
        exit 1
    }
}

if ($Isolated) {
    $safeDbPath = $IsolatedDatabasePath.Replace("'", "''")
    $backendCommand = "Set-Location '$repoRoot\\backend'; `$env:ASPNETCORE_ENVIRONMENT='Development'; `$env:ASPNETCORE_URLS='$BackendUrl'; `$env:ConnectionStrings__PlantMonitor='Data Source=$safeDbPath'; dotnet run --no-launch-profile --project '$backendProjectPath'"
}
else {
    $backendCommand = "Set-Location '$repoRoot\\backend'; `$env:ASPNETCORE_ENVIRONMENT='Development'; `$env:AllowedHosts='*'; dotnet run --no-launch-profile --urls $BackendUrl"
}

$frontendCommand = "Set-Location '$repoRoot'; `$env:VITE_DEV_BACKEND_ORIGIN='$BackendUrl'; npm --prefix frontend run dev -- --host $FrontendHost --port $FrontendPort"

$backendProcess = Start-Process -FilePath "powershell" -ArgumentList "-NoExit", "-Command", $backendCommand -PassThru
$frontendProcess = Start-Process -FilePath "powershell" -ArgumentList "-NoExit", "-Command", $frontendCommand -PassThru

Write-Host "Started backend terminal process id: $($backendProcess.Id)"
Write-Host "Started frontend terminal process id: $($frontendProcess.Id)"
Write-Host "Backend target URL: $BackendUrl"
Write-Host "Frontend expected URL: http://$FrontendHost`:$FrontendPort"

$backendReady = $false
$lastProbeError = $null
for ($attempt = 1; $attempt -le 45; $attempt++) {
    try {
        $liveResponse = Invoke-WebRequest -Method Get -Uri "$BackendUrl/health/live"
        if ($liveResponse.StatusCode -eq 200) {
            $backendReady = $true
            break
        }
    }
    catch {
        $lastProbeError = $_.Exception.Message
        Start-Sleep -Seconds 1
    }
}

if (-not $backendReady) {
    Write-Warning "Backend did not become reachable within timeout."
    if (-not [string]::IsNullOrWhiteSpace($lastProbeError)) {
        Write-Host "Last probe error: $lastProbeError"
    }
    Show-LockDiagnostics -Port $backendPort
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
