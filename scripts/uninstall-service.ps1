#Requires -RunAsAdministrator
# Stops and removes the EarnedScreen service, then strips the EarnedScreen block from the hosts file
# so streaming is restored.
$ErrorActionPreference = 'Stop'

$name  = 'EarnedScreen'
$hosts = Join-Path $env:SystemRoot 'System32\drivers\etc\hosts'

$existing = Get-Service -Name $name -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -ne 'Stopped') { Stop-Service $name -Force -ErrorAction SilentlyContinue }
    sc.exe delete $name | Out-Null
    Write-Host "Service '$name' removed."
} else {
    Write-Host "Service '$name' was not installed."
}

# Stop the UI app and remove its launch-at-login entry.
Write-Host "Stopping the UI app and removing launch-at-login..."
taskkill /IM EarnedScreen.App.exe /F 2>$null | Out-Null
$runKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'
if (Get-ItemProperty -Path $runKey -Name 'EarnedScreen' -ErrorAction SilentlyContinue) {
    Remove-ItemProperty -Path $runKey -Name 'EarnedScreen' -ErrorAction SilentlyContinue
}

# Strip the EarnedScreen-managed block so the hosts file is clean again.
if (Test-Path $hosts) {
    $lines = Get-Content $hosts
    $out = New-Object System.Collections.Generic.List[string]
    $inSection = $false
    foreach ($line in $lines) {
        $t = $line.Trim()
        if ($t -eq '# === EarnedScreen BLOCK START ===') { $inSection = $true; continue }
        if ($t -eq '# === EarnedScreen BLOCK END ===')   { $inSection = $false; continue }
        if (-not $inSection) { $out.Add($line) }
    }
    Set-Content -Path $hosts -Value $out -Encoding ascii
    ipconfig /flushdns | Out-Null
    Write-Host "Hosts file cleaned and DNS flushed. Streaming restored."
}

# Remove browser DoH policy keys written by install-service.ps1 / EnforcementEngine.
Write-Host "Restoring browser DoH policies..."
$chromePath = 'HKLM:\SOFTWARE\Policies\Google\Chrome'
$edgePath   = 'HKLM:\SOFTWARE\Policies\Microsoft\Edge'
$ffPath     = 'HKLM:\SOFTWARE\Policies\Mozilla\Firefox\DNSOverHTTPS'
if (Test-Path $chromePath) { Remove-ItemProperty $chromePath -Name DnsOverHttpsMode -ErrorAction SilentlyContinue }
if (Test-Path $edgePath)   { Remove-ItemProperty $edgePath   -Name DnsOverHttpsMode -ErrorAction SilentlyContinue }
if (Test-Path $ffPath)     { Remove-ItemProperty $ffPath     -Name Enabled           -ErrorAction SilentlyContinue }
Write-Host "Done. Restart browsers for DoH settings to take effect."

# Restore family-safe DNS: revert active adapters to automatic and unhide the network settings UI.
Write-Host "Restoring DNS to automatic on active adapters..."
$active = Get-NetIPConfiguration | Where-Object { $_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up' }
foreach ($cfg in $active) {
    try { Set-DnsClientServerAddress -InterfaceIndex $cfg.InterfaceIndex -ResetServerAddresses -ErrorAction Stop }
    catch { Write-Warning "Could not reset DNS on interface $($cfg.InterfaceIndex): $_" }
}
ipconfig /flushdns | Out-Null

Write-Host "Unlocking network settings UI..."
$explorerPolicy = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer'
if (Test-Path $explorerPolicy) { Remove-ItemProperty $explorerPolicy -Name 'SettingsPageVisibility' -ErrorAction SilentlyContinue }

# Remove the NC_ LAN-properties locks from HKLM and every loaded real-user hive.
$ncSub = 'Software\Policies\Microsoft\Windows\Network Connections'
$ncRoots = @("HKLM:\$ncSub")
Get-ChildItem 'Registry::HKEY_USERS' -ErrorAction SilentlyContinue |
    Where-Object { $_.PSChildName -like 'S-1-5-21-*' -and $_.PSChildName -notlike '*_Classes' } |
    ForEach-Object { $ncRoots += "Registry::HKEY_USERS\$($_.PSChildName)\$ncSub" }
foreach ($root in $ncRoots) {
    if (Test-Path $root) {
        Remove-ItemProperty $root -Name 'NC_LanChangeProperties' -ErrorAction SilentlyContinue
        Remove-ItemProperty $root -Name 'NC_LanProperties'       -ErrorAction SilentlyContinue
    }
}
Write-Host "Network settings UI restored. Family-safe DNS removed."
