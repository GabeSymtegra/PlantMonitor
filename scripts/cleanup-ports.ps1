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

    $processIds = $connections | Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($processId in $processIds) {
        try {
            Stop-Process -Id $processId -Force -ErrorAction Stop
            Write-Host "Stopped process $processId on port $port"
        }
        catch {
            Write-Warning "Could not stop process $processId on port $($port): $($_.Exception.Message)"
        }
    }
}
