#Requires -RunAsAdministrator
# Builds the service in Release and registers it as an auto-start Windows Service running as SYSTEM
# (so hosts-file edits never prompt for UAC).
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$name = 'EarnedScreen'
$proj = Join-Path $root 'src\EarnedScreen.Service\EarnedScreen.Service.csproj'
$exe  = Join-Path $root 'src\EarnedScreen.Service\bin\Release\net9.0-windows\EarnedScreen.Service.exe'
$data = Join-Path $env:ProgramData 'EarnedScreen'
$settingsFile = Join-Path $data 'settings.json'

Write-Host "Building $name (Release)..."
dotnet build $proj -c Release | Out-Null
if (-not (Test-Path $exe)) { throw "Build output not found: $exe" }

# Merge the canonical domain list into existing settings so upgrades pick up new entries.
$canonicalDomains = @(
    "netflix.com","www.netflix.com",
    "youtube.com","www.youtube.com","m.youtube.com","youtu.be","youtube-nocookie.com",
    "hulu.com","www.hulu.com",
    "disneyplus.com","www.disneyplus.com",
    "twitch.tv","www.twitch.tv",
    "primevideo.com","www.primevideo.com",
    "max.com","www.max.com","hbomax.com","www.hbomax.com"
)

if (Test-Path $settingsFile) {
    $s = Get-Content $settingsFile | ConvertFrom-Json
    $merged = ($s.BlockedDomains + $canonicalDomains) | Sort-Object -Unique
    $s.BlockedDomains = $merged
    $s | ConvertTo-Json -Depth 5 | Set-Content $settingsFile -Encoding utf8NoBOM
    Write-Host "Settings merged: $($merged.Count) blocked domains."
}

# Disable DoH in Chrome, Edge, and Firefox via policy registry so hosts-file blocks aren't bypassed.
Write-Host "Disabling DNS-over-HTTPS in browser policies..."
$null = New-Item -Force -Path 'HKLM:\SOFTWARE\Policies\Google\Chrome'             | Out-Null
$null = New-Item -Force -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Edge'            | Out-Null
$null = New-Item -Force -Path 'HKLM:\SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS' | Out-Null
Set-ItemProperty 'HKLM:\SOFTWARE\Policies\Google\Chrome'             -Name DnsOverHttpsMode -Value 'off' -Type String
Set-ItemProperty 'HKLM:\SOFTWARE\Policies\Microsoft\Edge'            -Name DnsOverHttpsMode -Value 'off' -Type String
Set-ItemProperty 'HKLM:\SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS' -Name Enabled       -Value 0    -Type DWord

$existing = Get-Service -Name $name -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Removing existing service..."
    if ($existing.Status -ne 'Stopped') { Stop-Service $name -Force -ErrorAction SilentlyContinue }
    sc.exe delete $name | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Registering service..."
New-Service -Name $name -BinaryPathName "`"$exe`"" `
    -DisplayName 'EarnedScreen' `
    -Description 'The Earned-Screen Protocol: blocks streaming until a daily session is earned.' `
    -StartupType Automatic | Out-Null

Start-Service $name
Write-Host "Service '$name' installed and started. Streaming is now blocked by default."
Write-Host "Note: restart Chrome/Edge/Firefox for the DoH policy to take effect."
