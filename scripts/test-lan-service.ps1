[CmdletBinding()]
param(
    [string]$ServiceName = "PlantMonitor-LAN",
    [string]$PrivilegedAgentServiceName = "PlantMonitor-PrivilegedAgent",
    [string]$HostNameOrIp = "localhost",
    [int]$Port = 5050,
    [int]$PrivilegedAgentPort = 5075,
    [string]$ExpectedDatabasePath = "C:\ProgramData\PlantMonitor\Data\plantmonitor.db",
    [string]$Username,
    [string]$Password
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

try {
    Add-Type -AssemblyName System.Net.Http -ErrorAction Stop
}
catch {
}

$baseUrl = "http://$HostNameOrIp`:$Port"
$isLocalHost = $HostNameOrIp -in @("localhost", "127.0.0.1", ".", $env:COMPUTERNAME)
$results = New-Object System.Collections.Generic.List[object]

function Invoke-Http {
    param(
        [string]$Url,
        [string]$Method = "GET",
        [string]$Body = "",
        [hashtable]$Headers = @{},
        [System.Net.CookieContainer]$CookieContainer = $null
    )

    $handler = [System.Net.Http.HttpClientHandler]::new()
    if ($CookieContainer) {
        $handler.CookieContainer = $CookieContainer
        $handler.UseCookies = $true
    }
    $client = [System.Net.Http.HttpClient]::new($handler)
    try {
        $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $Url)

        foreach ($headerName in $Headers.Keys) {
            $request.Headers.TryAddWithoutValidation($headerName, [string]$Headers[$headerName]) | Out-Null
        }

        if ($Method -ne "GET" -and $Body) {
            $request.Content = [System.Net.Http.StringContent]::new($Body, [System.Text.Encoding]::UTF8, "application/json")
        }

        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

        $setCookieValues = @()
        try {
            $setCookieValues = @($response.Headers.GetValues("Set-Cookie"))
        }
        catch {
            $setCookieValues = @()
        }

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Content = $content
            SetCookieValues = $setCookieValues
        }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

function Add-Result {
    param(
        [string]$Name,
        [bool]$Pass,
        [string]$Detail,
        [bool]$Required = $true
    )

    $results.Add([pscustomobject]@{
        Name = $Name
        Required = $Required
        Pass = $Pass
        Detail = $Detail
    }) | Out-Null
}

function Wait-Ready {
    param([string]$Url)

    $deadline = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $deadline) {
        $probe = Invoke-Http -Url $Url -Method "GET"
        if ($probe.StatusCode -ge 200 -and $probe.StatusCode -lt 300) {
            return $true
        }

        Start-Sleep -Seconds 2
    }

    return $false
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
$serviceExistsDetail = if ($service) { "Found" } else { "Missing" }
Add-Result -Name "Service exists" -Pass ([bool]$service) -Detail $serviceExistsDetail

$privilegedAgentService = Get-Service -Name $PrivilegedAgentServiceName -ErrorAction SilentlyContinue
$agentExistsDetail = if ($privilegedAgentService) { "Found" } else { "Missing" }
Add-Result -Name "Privileged agent service exists" -Pass ([bool]$privilegedAgentService) -Detail $agentExistsDetail

if ($isLocalHost) {
    $serviceStateDetail = if ($service) { $service.Status.ToString() } else { "Service missing" }
    Add-Result -Name "Service status is Running" -Pass ($service -and $service.Status -eq "Running") -Detail $serviceStateDetail

    $agentStateDetail = if ($privilegedAgentService) { $privilegedAgentService.Status.ToString() } else { "Service missing" }
    Add-Result -Name "Privileged agent status is Running" -Pass ($privilegedAgentService -and $privilegedAgentService.Status -eq "Running") -Detail $agentStateDetail

    $listener = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
    $listenerDetail = if ($listener) { "Listening" } else { "No listener" }
    Add-Result -Name "TCP port is listening" -Pass ([bool]$listener) -Detail $listenerDetail
}
else {
    Add-Result -Name "Service status is Running" -Pass $true -Detail "Skipped for remote host" -Required $false
    Add-Result -Name "Privileged agent status is Running" -Pass $true -Detail "Skipped for remote host" -Required $false
    Add-Result -Name "TCP port is listening" -Pass $true -Detail "Skipped for remote host" -Required $false
}

if ($isLocalHost) {
    $agentLive = Invoke-Http -Url "http://127.0.0.1:$PrivilegedAgentPort/health/live" -Method "GET"
    Add-Result -Name "Privileged agent /health/live returns success" -Pass ($agentLive.StatusCode -ge 200 -and $agentLive.StatusCode -lt 300) -Detail "HTTP $($agentLive.StatusCode)"
}
else {
    Add-Result -Name "Privileged agent /health/live returns success" -Pass $true -Detail "Skipped for remote host" -Required $false
}

$live = Invoke-Http -Url "$baseUrl/health/live" -Method "GET"
Add-Result -Name "/health/live returns success" -Pass ($live.StatusCode -ge 200 -and $live.StatusCode -lt 300) -Detail "HTTP $($live.StatusCode)"

$ready = Invoke-Http -Url "$baseUrl/health/ready" -Method "GET"
Add-Result -Name "/health/ready returns success" -Pass ($ready.StatusCode -ge 200 -and $ready.StatusCode -lt 300) -Detail "HTTP $($ready.StatusCode)"

$root = Invoke-Http -Url "$baseUrl/" -Method "GET"
$hasHtml = $root.StatusCode -ge 200 -and $root.StatusCode -lt 300 -and $root.Content -match "<html"
Add-Result -Name "Root returns frontend HTML" -Pass $hasHtml -Detail "HTTP $($root.StatusCode)"

$assetMatches = [regex]::Matches($root.Content, '(?:src|href)="(?<url>[^\"]+\.(?:js|css))"', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
$assetUrls = @()
foreach ($match in $assetMatches) {
    $relative = $match.Groups["url"].Value
    if ($relative.StartsWith("http://") -or $relative.StartsWith("https://")) {
        $assetUrls += $relative
    }
    else {
        if (-not $relative.StartsWith("/")) {
            $relative = "/" + $relative
        }

        $assetUrls += "$baseUrl$relative"
    }
}
$assetUrls = @($assetUrls | Select-Object -Unique)

$allAssetsAccessible = $true
$assetFailures = New-Object System.Collections.Generic.List[string]
foreach ($assetUrl in $assetUrls) {
    $assetResponse = Invoke-Http -Url $assetUrl -Method "GET"
    if ($assetResponse.StatusCode -lt 200 -or $assetResponse.StatusCode -ge 300) {
        $allAssetsAccessible = $false
        $assetFailures.Add("$assetUrl => HTTP $($assetResponse.StatusCode)") | Out-Null
    }
}

$assetDetail = if ($allAssetsAccessible) { "Checked $($assetUrls.Count) assets" } else { ($assetFailures -join "; ") }
Add-Result -Name "Referenced JS/CSS files are accessible" -Pass $allAssetsAccessible -Detail $assetDetail

$session = Invoke-Http -Url "$baseUrl/api/auth/session" -Method "GET"
Add-Result -Name "/api/auth/session responds without server error" -Pass ($session.StatusCode -lt 500) -Detail "HTTP $($session.StatusCode)"

$negotiate = Invoke-Http -Url "$baseUrl/hubs/lines/negotiate?negotiateVersion=1" -Method "POST" -Body "{}"
$signalRReachable = $negotiate.StatusCode -ne 404 -and $negotiate.StatusCode -lt 500
Add-Result -Name "SignalR negotiation endpoint is reachable" -Pass $signalRReachable -Detail "HTTP $($negotiate.StatusCode)"

$hasAnyCredentialInput = -not [string]::IsNullOrWhiteSpace($Username) -or -not [string]::IsNullOrWhiteSpace($Password)
if ($hasAnyCredentialInput) {
    if ([string]::IsNullOrWhiteSpace($Username) -or [string]::IsNullOrWhiteSpace($Password)) {
        Add-Result -Name "Credential check input is complete" -Pass $false -Detail "Provide both -Username and -Password to run login validation."
    }
    else {
        $cookieContainer = New-Object System.Net.CookieContainer
        $loginRequest = @{ username = $Username; password = $Password; rememberMe = $true } | ConvertTo-Json -Compress
        $loginResponse = Invoke-Http -Url "$baseUrl/api/auth/login" -Method "POST" -Body $loginRequest -CookieContainer $cookieContainer
        $loginSucceeded = $loginResponse.StatusCode -eq 200
        Add-Result -Name "Credential check login succeeds" -Pass $loginSucceeded -Detail "HTTP $($loginResponse.StatusCode)"

        $pmAuthCookie = @($loginResponse.SetCookieValues | Where-Object { $_ -like "pm_auth=*" } | Select-Object -First 1)
        $hasPmAuthCookie = $pmAuthCookie.Count -gt 0
        Add-Result -Name "Credential check login sets pm_auth cookie" -Pass $hasPmAuthCookie -Detail ($(if ($hasPmAuthCookie) { "pm_auth cookie present" } else { "pm_auth cookie missing" }))

        if ($hasPmAuthCookie) {
            $sessionAfterLogin = Invoke-Http -Url "$baseUrl/api/auth/session" -Method "GET" -CookieContainer $cookieContainer
            $sessionHealthy = $sessionAfterLogin.StatusCode -eq 200
            Add-Result -Name "Credential check session endpoint works with pm_auth cookie" -Pass $sessionHealthy -Detail "HTTP $($sessionAfterLogin.StatusCode)"

            $wifiStatus = Invoke-Http -Url "$baseUrl/api/admin/system/wifi/status" -Method "GET" -CookieContainer $cookieContainer
            $wifiStatusReachable = $wifiStatus.StatusCode -ne 404 -and $wifiStatus.StatusCode -lt 500
            Add-Result -Name "Credential check Wi-Fi status endpoint is reachable" -Pass $wifiStatusReachable -Detail "HTTP $($wifiStatus.StatusCode)"
        }
    }
}

if ($isLocalHost) {
    $serviceKey = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
    $envRows = @()
    if (Test-Path -LiteralPath $serviceKey) {
        $envValue = (Get-ItemProperty -Path $serviceKey -Name Environment -ErrorAction SilentlyContinue).Environment
        if ($envValue) {
            $envRows = @($envValue)
        }
    }

    $connRow = $envRows | Where-Object { $_ -like "ConnectionStrings__PlantMonitor=*" } | Select-Object -First 1
    $dbPathConfigured = $connRow -and $connRow -like "*Data Source=$ExpectedDatabasePath*"
    $dbPathDetail = if ($connRow) { $connRow } else { "ConnectionStrings__PlantMonitor not found" }
    Add-Result -Name "Database path is expected ProgramData path" -Pass ([bool]$dbPathConfigured) -Detail $dbPathDetail

    if ($service) {
        try {
            Restart-Service -Name $ServiceName -Force -ErrorAction Stop
            Start-Sleep -Seconds 2
            $runningAfterRestart = (Get-Service -Name $ServiceName).Status -eq "Running"
            $restartDetail = if ($runningAfterRestart) { "Running" } else { "Not running" }
            Add-Result -Name "Service restart succeeds" -Pass $runningAfterRestart -Detail $restartDetail

            $readyAfterRestart = Wait-Ready -Url "$baseUrl/health/ready"
            $readyDetail = if ($readyAfterRestart) { "Ready" } else { "Timed out" }
            Add-Result -Name "Application ready after restart" -Pass $readyAfterRestart -Detail $readyDetail
        }
        catch {
            Add-Result -Name "Service restart succeeds" -Pass $false -Detail $_.Exception.Message
            Add-Result -Name "Application ready after restart" -Pass $false -Detail "Restart step failed"
        }
    }
    else {
        Add-Result -Name "Service restart succeeds" -Pass $false -Detail "Service missing"
        Add-Result -Name "Application ready after restart" -Pass $false -Detail "Service missing"
    }

    $dbExists = Test-Path -LiteralPath $ExpectedDatabasePath
    $dbExistsDetail = if ($dbExists) { $ExpectedDatabasePath } else { "Missing $ExpectedDatabasePath" }
    Add-Result -Name "Database/config remains present after restart" -Pass $dbExists -Detail $dbExistsDetail
}
else {
    Add-Result -Name "Database path is expected ProgramData path" -Pass $true -Detail "Skipped for remote host" -Required $false
    Add-Result -Name "Service restart succeeds" -Pass $true -Detail "Skipped for remote host" -Required $false
    Add-Result -Name "Application ready after restart" -Pass $true -Detail "Skipped for remote host" -Required $false
    Add-Result -Name "Database/config remains present after restart" -Pass $true -Detail "Skipped for remote host" -Required $false
}

$requiredFailures = @($results | Where-Object { $_.Required -and -not $_.Pass })

Write-Host ""
Write-Host "PlantMonitor LAN Service Verification"
Write-Host "Target: $baseUrl"
Write-Host ""
$results | ForEach-Object {
    $state = if ($_.Pass) { "PASS" } else { "FAIL" }
    Write-Host ("{0,-6} | {1,-8} | {2} | {3}" -f $state, ($(if ($_.Required) { "Required" } else { "Optional" })), $_.Name, $_.Detail)
}

if ($requiredFailures.Count -gt 0) {
    Write-Error "Required checks failed: $($requiredFailures.Count)"
    exit 1
}

Write-Host "All required checks passed."
exit 0
