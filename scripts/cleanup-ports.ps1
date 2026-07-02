param(
    [int[]]$Ports = @(5265, 5173)
)

$ErrorActionPreference = "Stop"

foreach ($port in $Ports) {
    $connections = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if (-not $connections) {
        Write-Host "No listener found on port $port"
        continue
    }

    $pids = $connections | Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($pid in $pids) {
        try {
            Stop-Process -Id $pid -Force -ErrorAction Stop
            Write-Host "Stopped process $pid on port $port"
        }
        catch {
            Write-Warning "Could not stop process $pid on port $port: $($_.Exception.Message)"
        }
    }
}
