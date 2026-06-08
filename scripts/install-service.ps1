#Requires -RunAsAdministrator
# Builds the service in Release and registers it as an auto-start Windows Service running as SYSTEM
# (so hosts-file edits never prompt for UAC).
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$name = 'EarnedScreen'
$proj = Join-Path $root 'src\EarnedScreen.Service\EarnedScreen.Service.csproj'
$exe  = Join-Path $root 'src\EarnedScreen.Service\bin\Release\net9.0-windows\EarnedScreen.Service.exe'

Write-Host "Building $name (Release)..."
dotnet build $proj -c Release | Out-Null
if (-not (Test-Path $exe)) { throw "Build output not found: $exe" }

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
