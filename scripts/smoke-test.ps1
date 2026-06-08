# End-to-end smoke test for the EarnedScreen service.
# Runs the real service against a sandbox hosts file + data dir (no admin needed) and drives it
# through the named pipe: default-block -> unlock -> Guillotine -> daily-limit.
$ErrorActionPreference = 'Stop'

$root    = Split-Path -Parent $PSScriptRoot
$exe     = Join-Path $root 'src\EarnedScreen.Service\bin\Debug\net9.0-windows\EarnedScreen.Service.exe'
$sandbox = Join-Path $env:TEMP ("earnedscreen-smoke-" + [guid]::NewGuid().ToString('N'))
$data    = Join-Path $sandbox 'data'
$hosts   = Join-Path $sandbox 'hosts'

New-Item -ItemType Directory -Force -Path $data | Out-Null
Set-Content -Path $hosts -Value "127.0.0.1 localhost`r`n" -NoNewline
$settings = [ordered]@{
    SessionMinutes    = 1
    SessionsPerDay    = 1
    BlockedDomains    = @('netflix.com', 'youtube.com')
    GatewayChecklist  = @('do the toll')
    CoolDownChecklist = @('cool down')
} | ConvertTo-Json
Set-Content -Path (Join-Path $data 'settings.json') -Value $settings

$env:EARNEDSCREEN_DATA  = $data
$env:EARNEDSCREEN_HOSTS = $hosts

function Test-Blocked { (Get-Content $hosts -Raw) -match 'EarnedScreen BLOCK START' }

function Send-Command([int]$cmd) {
    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', 'EarnedScreen.Command', [System.IO.Pipes.PipeDirection]::InOut)
    $pipe.Connect(3000)
    $reader = New-Object System.IO.StreamReader($pipe)
    $writer = New-Object System.IO.StreamWriter($pipe)
    $writer.AutoFlush = $true
    $writer.WriteLine("{`"Command`":$cmd}")
    $line = $reader.ReadLine()
    $pipe.Dispose()
    return ($line | ConvertFrom-Json)
}

$pass = $true
function Check($label, $cond) {
    $script:pass = $script:pass -and $cond
    "{0}  {1}" -f $(if ($cond) { '[PASS]' } else { '[FAIL]' }), $label | Write-Host
}

$proc = Start-Process -FilePath $exe -PassThru -WindowStyle Hidden
try {
    # Wait for the service to finish startup (Initialize applies the default block).
    $startDeadline = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $startDeadline -and -not (Test-Blocked)) { Start-Sleep -Milliseconds 250 }

    Check "Default state blocks the sandbox hosts file" (Test-Blocked)

    $r = Send-Command 1   # RequestUnlock
    Check "Unlock succeeds"                 ($r.Success -eq $true)
    Check "Status is Unlocked (1)"          ($r.Status -eq 1)
    Start-Sleep -Milliseconds 500
    Check "Hosts file is unblocked"         (-not (Test-Blocked))

    $r2 = Send-Command 1  # second unlock while active
    Check "Second unlock reports active session" ($r2.Message -match 'already active')

    Write-Host "Waiting up to 80s for the Guillotine..."
    $deadline = (Get-Date).AddSeconds(80)
    while ((Get-Date) -lt $deadline -and -not (Test-Blocked)) { Start-Sleep -Seconds 2 }
    Check "Guillotine re-blocked the hosts file" (Test-Blocked)

    $r3 = Send-Command 1  # unlock after today's session spent
    Check "Daily limit refuses a new session"   ($r3.Success -eq $false -and $r3.Message -match 'tomorrow')
}
finally {
    if ($proc -and -not $proc.HasExited) { $proc.Kill() }
    Remove-Item -Recurse -Force $sandbox -ErrorAction SilentlyContinue
}

if ($pass) { Write-Host "`nSMOKE TEST: ALL PASSED" } else { Write-Host "`nSMOKE TEST: FAILURES" ; exit 1 }
