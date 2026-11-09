$base = $PSScriptRoot
$deadline = (Get-Date).AddSeconds(240)
$ready = $false
while ((Get-Date) -lt $deadline) {
    try {
        $lines = [IO.File]::ReadAllLines((Join-Path $base 'vpn-ready.txt'))
        $proc = Get-Process -Id ([int]$lines[0]) -ErrorAction Stop
        $adapter = Get-NetAdapter -Name 'tun-discord' -ErrorAction SilentlyContinue
        if ($proc.ProcessName -eq 'sing-box' -and ([DateTime]::UtcNow - [IO.File]::GetLastWriteTimeUtc((Join-Path $base 'vpn-ready.txt'))).TotalSeconds -lt 90 -and $adapter.Status -eq 'Up') { $ready = $true; break }
    } catch { }
    Start-Sleep -Seconds 1
}
if (-not $ready) { exit 1 }
if (-not (Get-Process -Name Discord -ErrorAction SilentlyContinue)) {
    $update = Join-Path $env:LOCALAPPDATA 'Discord\Update.exe'
    if (Test-Path -LiteralPath $update) { Start-Process -FilePath $update -ArgumentList '--processStart Discord.exe' -WindowStyle Hidden }
}
