#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'

$root    = Split-Path -Parent $PSScriptRoot
$name    = 'EarnedScreen'
$proj    = Join-Path $root 'src\EarnedScreen.Service\EarnedScreen.Service.csproj'
$exe     = Join-Path $root 'src\EarnedScreen.Service\bin\Release\net9.0-windows\EarnedScreen.Service.exe'
$appProj = Join-Path $root 'src\EarnedScreen.App\EarnedScreen.App.csproj'
$appExe  = Join-Path $root 'src\EarnedScreen.App\bin\Release\net9.0-windows\EarnedScreen.App.exe'
$data    = Join-Path $env:ProgramData 'EarnedScreen'
$settingsFile = Join-Path $data 'settings.json'

# --- Step 1: Stop the running service AND UI app first so the build can overwrite their binaries ---
$existing = Get-Service -Name $name -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Stopping existing service..."
    if ($existing.Status -ne 'Stopped') { Stop-Service $name -Force }
    sc.exe delete $name | Out-Null
    Start-Sleep -Seconds 2
    Write-Host "Existing service removed."
}
Write-Host "Stopping the UI app if running..."
Get-Process EarnedScreen.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# --- Step 2: Build service + UI app (Release) ---
Write-Host "Building service (Release)..."
dotnet build $proj -c Release --nologo -v q
if (-not (Test-Path $exe)) { throw "Service build output not found: $exe" }
Write-Host "Building UI app (Release)..."
dotnet build $appProj -c Release --nologo -v q
if (-not (Test-Path $appExe)) { throw "App build output not found: $appExe" }

# --- Step 3: Merge canonical domain list into existing settings.json ---
$canonicalDomains = @(
    "netflix.com","www.netflix.com",
    "youtube.com","www.youtube.com","m.youtube.com","youtu.be","youtube-nocookie.com",
    "hulu.com","www.hulu.com",
    "disneyplus.com","www.disneyplus.com",
    "twitch.tv","www.twitch.tv",
    "primevideo.com","www.primevideo.com",
    "max.com","www.max.com","play.max.com",
    "hbomax.com","www.hbomax.com","play.hbomax.com"
)

if (Test-Path $settingsFile) {
    $s = Get-Content $settingsFile -Raw | ConvertFrom-Json
    $existing_domains = @($s.BlockedDomains)
    $merged = ($existing_domains + $canonicalDomains) | Sort-Object -Unique
    $s.BlockedDomains = $merged
    $json = $s | ConvertTo-Json -Depth 5
    # Use .NET directly — avoids the PS5/PS7 encoding name difference
    [System.IO.File]::WriteAllText($settingsFile, $json, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Settings updated: $($merged.Count) blocked domains."
}

# --- Step 4: Disable DoH in Chrome, Edge, Firefox via policy registry ---
Write-Host "Disabling browser DNS-over-HTTPS policies..."
$null = New-Item -Force -Path 'HKLM:\SOFTWARE\Policies\Google\Chrome'                | Out-Null
$null = New-Item -Force -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Edge'               | Out-Null
$null = New-Item -Force -Path 'HKLM:\SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS' | Out-Null
Set-ItemProperty 'HKLM:\SOFTWARE\Policies\Google\Chrome'                -Name DnsOverHttpsMode -Value 'off' -Type String
Set-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Edge'               -Name DnsOverHttpsMode -Value 'off' -Type String
Set-ItemProperty 'HKLM:\SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS' -Name Enabled          -Value 0    -Type DWord

# --- Step 5: Register and start the service ---
Write-Host "Registering service..."
New-Service -Name $name -BinaryPathName "`"$exe`"" `
    -DisplayName 'EarnedScreen' `
    -Description 'The Earned-Screen Protocol: blocks streaming until a daily session is earned.' `
    -StartupType Automatic | Out-Null

Start-Service $name

# --- Step 6: UI app — launch at login (machine-wide) and start it now in the tray ---
Write-Host "Configuring the UI app to launch at login..."
$runKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'
Set-ItemProperty -Path $runKey -Name 'EarnedScreen' -Value "`"$appExe`""
Start-Process $appExe

Write-Host ""
Write-Host "Done. '$name' is running and the UI is in your system tray (it'll auto-start at login)."
Write-Host "Restart Chrome/Edge/Firefox now for the DoH policy to take effect."
