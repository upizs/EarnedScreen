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
