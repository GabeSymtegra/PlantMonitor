$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot

try {
    Write-Host "Running backend build..."
    dotnet build .\backend\backend.csproj

    Write-Host "Running frontend build..."
    npm --prefix frontend run build

    Write-Host "Checking required docs..."
    $required = @(
        ".\\DEV-LOG.md",
        ".\\docs\\Deployment.md",
        ".\\docs\\Release.md",
        ".\\docs\\QA-Matrix.md"
    )

    foreach ($path in $required) {
        if (-not (Test-Path $path)) {
            throw "Missing required file: $path"
        }
    }

    Write-Host "Pre-deploy checks passed."
}
finally {
    Pop-Location
}
