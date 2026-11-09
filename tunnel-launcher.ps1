# $PSScriptRoot first: $MyInvocation.MyCommand.Path comes back empty in some
# unattended hosts, and this runs at logon where a null path fails silently.
$base = $PSScriptRoot
if (-not $base) { $base = Split-Path -Parent $MyInvocation.MyCommand.Path }
if (-not $base) { exit 1 }
Set-Location $base
$readyPath = Join-Path $base "vpn-ready.txt"
Remove-Item -LiteralPath $readyPath -Force -ErrorAction SilentlyContinue

function Get-PhysicalInterfaceAlias {
    # Skip sing-box's own TUN: with auto_route it owns the default route, and
    # binding the direct outbound to it makes every UDP flow fail as a
    # "loopback connection to TUN range".
    try {
        $routes = Get-NetRoute -DestinationPrefix "0.0.0.0/0" -ErrorAction Stop | Sort-Object -Property RouteMetric
        foreach ($r in $routes) {
            $adapter = Get-NetAdapter -InterfaceIndex $r.InterfaceIndex -ErrorAction SilentlyContinue
            if ($adapter -and $adapter.Status -eq "Up" -and
                $adapter.Name -notlike "tun*" -and
                $adapter.InterfaceDescription -notlike "*sing-tun*") {
                return $adapter.Name
            }
        }
    } catch { }
    return $null
}

function Test-NetworkReady {
    # A default route on a physical adapter is necessary but not sufficient:
    # at logon Windows publishes the route before the link has finished
    # negotiating, so also require that a packet actually gets out.
    if (-not (Get-PhysicalInterfaceAlias)) { return $false }
    try {
        $client = New-Object System.Net.Sockets.TcpClient
        $async = $client.BeginConnect("1.1.1.1", 443, $null, $null)
        $ok = $async.AsyncWaitHandle.WaitOne(3000, $false) -and $client.Connected
        $client.Close()
        return $ok
    } catch {
        return $false
    }
}

function Get-LogTextSince($path, $offset) {
    # sing-box holds the log open, so it has to be read with FileShare
    # ReadWrite. Reading only what was appended after the start marker keeps
    # this from tripping over failures logged by an earlier run.
    if (-not (Test-Path $path)) { return "" }
    try {
        $stream = [System.IO.File]::Open($path, 'Open', 'Read', 'ReadWrite')
        if ($stream.Length -le $offset) { $stream.Close(); return "" }
        $stream.Seek($offset, 'Begin') | Out-Null
        $buffer = New-Object byte[] ($stream.Length - $offset)
        $stream.Read($buffer, 0, $buffer.Length) | Out-Null
        $stream.Close()
        return [System.Text.Encoding]::UTF8.GetString($buffer)
    } catch {
        return ""
    }
}

# Never run two instances. Two sing-box processes sharing one WireGuard key
# make the VPN server bounce the session between them, and the tunnel carries
# no data at all while that happens. If an orphan from an interrupted launcher
# is still alive, terminate it first (this task runs elevated) so the config on
# disk is the one actually in use.
$existing = @(Get-Process -Name "sing-box" -ErrorAction SilentlyContinue)
if ($existing.Count -gt 0) {
    foreach ($p in $existing) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
    for ($i = 0; $i -lt 20; $i++) {
        if (-not (Get-Process -Name "sing-box" -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Milliseconds 500
    }
    if (Get-Process -Name "sing-box" -ErrorAction SilentlyContinue) { exit 0 }
    Start-Sleep -Seconds 2
}

# Wait for the network before starting sing-box. This is the whole reason this
# launcher runs at logon rather than sing-box directly: sing-box dials the
# WireGuard endpoint once, at startup, and never retries that dial. Started on
# a timer against a machine whose adapter wasn't up yet, it logs
#   network: missing default interface
#   endpoint/wireguard[vpn]: connect to server: ... no such network interface
# and then runs all day with a dead VPN - TUN up, routes correct, every
# Discord connection dropped into a black hole while everything upstream
# reports a healthy tunnel.
$networkDeadline = (Get-Date).AddSeconds(180)
while (-not (Test-NetworkReady) -and (Get-Date) -lt $networkDeadline) {
    Start-Sleep -Seconds 3
}

$configPath = Join-Path $base "config.json"
$iface = Get-PhysicalInterfaceAlias
if ($iface -and (Test-Path $configPath)) {
    try {
        $cfg = Get-Content -Raw $configPath | ConvertFrom-Json
        $direct = $cfg.outbounds | Where-Object { $_.tag -eq "direct" } | Select-Object -First 1
        if ($direct -and $direct.bind_interface -ne $iface) {
            $direct | Add-Member -NotePropertyName bind_interface -NotePropertyValue $iface -Force
            $json = $cfg | ConvertTo-Json -Depth 10
            [System.IO.File]::WriteAllText($configPath, $json, (New-Object System.Text.UTF8Encoding($false)))
        }
    } catch { }
}

# Discard a runaway log while nothing holds it open
$logPath = Join-Path $base "sing-box.log"
if ((Test-Path $logPath) -and ((Get-Item $logPath).Length -gt 20MB)) {
    Remove-Item $logPath -Force -ErrorAction SilentlyContinue
}

# Supervised start. Waiting for the network above prevents the common case,
# but a link that flaps during logon can still hand sing-box a dead endpoint,
# and a tunnel that carries nothing is worse than no tunnel at all: it looks
# healthy from every angle except Discord's. So the dial is verified from the
# log and a bad start is retried instead of being left to sit there.
$singboxExe = Join-Path $base "sing-box.exe"
$deadEndpoint = 'endpoint/wireguard\[vpn\]: connect to server|network: missing default interface'

# The stop script drops this file before killing anything. Without it a
# restart racing the stop could bring sing-box back up right after the user
# was told the VPN was off.
$stopFlag = Join-Path $base "stop.flag"

while ($true) {
    if (Test-Path -LiteralPath $stopFlag) { exit 0 }

    $proc = $null
    foreach ($attempt in 1..4) {
        if (Test-Path -LiteralPath $stopFlag) { exit 0 }

        $offset = 0
        if (Test-Path $logPath) { $offset = (Get-Item $logPath).Length }

        Remove-Item -LiteralPath $readyPath -Force -ErrorAction SilentlyContinue
        $proc = Start-Process -FilePath $singboxExe -ArgumentList "run -c config.json" `
            -WorkingDirectory $base -WindowStyle Hidden -PassThru
        Start-Sleep -Seconds 20

        if ($proc.HasExited) {
            Start-Sleep -Seconds 10
            continue
        }

        if ((Get-LogTextSince $logPath $offset) -match $deadEndpoint) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 15
            continue
        }

        $adapter = Get-NetAdapter -Name 'tun-discord' -ErrorAction SilentlyContinue
        if (-not $adapter -or $adapter.Status -ne 'Up' -or -not (Test-NetworkReady)) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 5
            continue
        }
        [IO.File]::WriteAllLines($readyPath, @([string]$proc.Id, [string](Get-Date).ToUniversalTime().Ticks))
        break
    }

    if (-not $proc -or $proc.HasExited) {
        Start-Sleep -Seconds 10
        continue
    }

    # Healthy start, but the TUN can still be torn down later (network change,
    # adapter reset, sleep/resume). Watch it for as long as sing-box lives and
    # restart the whole thing if it disappears, instead of running all day with
    # a dead tunnel that looks healthy from every angle except Discord's.
    while (-not $proc.HasExited) {
        Start-Sleep -Seconds 30
        if (Test-Path -LiteralPath $stopFlag) {
            Remove-Item -LiteralPath $readyPath -Force -ErrorAction SilentlyContinue
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            exit 0
        }
        $tun = Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object {
            $_.Status -eq "Up" -and ($_.Name -like "tun*" -or $_.InterfaceDescription -like "*sing-tun*") }
        if ($tun) { [IO.File]::WriteAllLines($readyPath, @([string]$proc.Id, [string](Get-Date).ToUniversalTime().Ticks)) }
        if (-not $tun) {
            Remove-Item -LiteralPath $readyPath -Force -ErrorAction SilentlyContinue
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            break
        }
    }

    Remove-Item -LiteralPath $readyPath -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 10
}