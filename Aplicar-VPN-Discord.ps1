[CmdletBinding()]
param(
    [string]$ConfPath = (Join-Path $env:USERPROFILE 'Downloads\wg-MX-FREE-1.conf'),
    [switch]$Stop,
    [switch]$DryRun,
    [ValidateSet('On', 'Off')][string]$StartupMode,
    [string]$ManagerPath
)

$ErrorActionPreference = 'Stop'

# Discord Single-Tunneling installation (sing-box + wintun + scheduled task)
$appDir   = Join-Path $env:LOCALAPPDATA 'Programs\Discord Single-Tunneling'
$logPath  = Join-Path $PSScriptRoot 'Discord-VPN.log'
$taskName = 'DiscordTunneling'

function Write-Log {
    param([string]$Message)
    $line = '{0}  {1}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message
    try { Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8 } catch { }
    Write-Host $Message
}

# ---------- self-elevate (TUN + scheduled task need Administrator) ----------
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin -and -not $DryRun) {
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $PSCommandPath))
    if ($StartupMode) { $argList += @('-StartupMode', $StartupMode); if ($ManagerPath) { $argList += @('-ManagerPath', ('"{0}"' -f $ManagerPath)) } }
    elseif ($Stop) { $argList += '-Stop' } else { $argList += @('-ConfPath', ('"{0}"' -f $ConfPath)) }
    try {
        Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $argList | Out-Null
    } catch {
        Write-Host 'Elevation cancelled - the tunnel was not changed.'
        exit 1
    }
    exit 0
}

function Get-LauncherProcesses {
    $launcherPath = Join-Path $appDir 'tunnel-launcher.ps1'
    try {
        return @(Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue |
            Where-Object { $_.CommandLine -and
                $_.CommandLine.IndexOf($launcherPath, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    } catch {
        return @()
    }
}

function Get-SingBoxProcesses {
    return @(Get-Process -Name 'sing-box' -ErrorAction SilentlyContinue)
}

function Get-StaleTunAdapters {
    try {
        return @(Get-NetAdapter -IncludeHidden -ErrorAction SilentlyContinue | Where-Object {
            $_.InterfaceDescription -like '*sing-tun*' })
    } catch {
        return @()
    }
}

function Stop-AllTunnel {
    # Stop the task first so the scheduler cannot start a fresh launcher while
    # we are tearing the old one down.
    $taskExists = $true
    try { Get-ScheduledTask -TaskName $taskName -ErrorAction Stop | Out-Null }
    catch { $taskExists = $false }

    if ($taskExists) {
        for ($i = 0; $i -lt 5; $i++) {
            try { Stop-ScheduledTask -TaskName $taskName -ErrorAction Stop } catch { }
            Start-Sleep -Milliseconds 500
            $state = $null
            try { $state = (Get-ScheduledTask -TaskName $taskName -ErrorAction Stop).State } catch { }
            if (-not $state -or $state -ne 'Running') { break }
            try { & schtasks.exe /End /TN $taskName 2>&1 | Out-Null } catch { }
        }
    }

    # Keep killing the supervisor and sing-box together until both are gone.
    # Killing several times matters because the launcher may respawn sing-box
    # between two of our checks.
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline) {
        $launchers = Get-LauncherProcesses
        foreach ($l in $launchers) {
            try { Stop-Process -Id $l.ProcessId -Force -ErrorAction Stop } catch { }
        }
        $singbox = Get-SingBoxProcesses
        foreach ($p in $singbox) {
            try { Stop-Process -Id $p.Id -Force -ErrorAction Stop } catch { }
        }
        if ($launchers.Count -eq 0 -and $singbox.Count -eq 0) { break }
        Start-Sleep -Milliseconds 400
    }

    Start-Sleep -Milliseconds 800

    # A force-killed sing-box can leave its TUN adapter and auto_route entries
    # behind. Windows then keeps sending Discord traffic into a dead tunnel
    # even though no process is running, which looks exactly like the VPN
    # "still running". Remove them so the normal connection is really back.
    $staleAdapters = Get-StaleTunAdapters
    foreach ($a in $staleAdapters) {
        try {
            Get-NetRoute -InterfaceIndex $a.ifIndex -ErrorAction SilentlyContinue |
                Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue
        } catch { }
        try { Disable-NetAdapter -Name $a.Name -Confirm:$false -ErrorAction SilentlyContinue } catch { }
        # Orphaned Wintun devices are removed with pnputil after process cleanup.
    }

    $leftLaunchers = Get-LauncherProcesses
    $leftSingbox = Get-SingBoxProcesses
    if ($leftLaunchers.Count -gt 0 -or $leftSingbox.Count -gt 0) {
        $pids = @()
        foreach ($l in $leftLaunchers) { $pids += $l.ProcessId }
        foreach ($p in $leftSingbox) { $pids += $p.Id }
        throw ('sing-box / tunnel launcher still running (PID {0})' -f ($pids -join ', '))
    }

    # Remove only orphaned sing-box Wintun devices after its processes exit.
    # Get-NetAdapter omits phantom devices, so adapter cleanup alone misses them.
    if ($leftLaunchers.Count -eq 0 -and $leftSingbox.Count -eq 0) {
        Get-PnpDevice -Class Net -ErrorAction SilentlyContinue |
            Where-Object { $_.InstanceId -like 'SWD\WINTUN\*' -and
                $_.FriendlyName -eq 'sing-tun Tunnel' -and -not $_.Present } |
            ForEach-Object {
                $result = & pnputil.exe /remove-device $_.InstanceId 2>&1
                Write-Log ('Wintun orphan cleanup: {0} (exit {1})' -f $_.InstanceId, $LASTEXITCODE)
            }
    }

    $remaining = Get-StaleTunAdapters
    if ($remaining.Count -gt 0) {
        Write-Log ('WARNING: stale TUN adapter still present: {0}' -f (($remaining | ForEach-Object { $_.Name }) -join ', '))
    }
}

function Ensure-ScheduledTask {
    param([bool]$EnableAtLogon = (Test-Path (Join-Path $appDir 'start-with-windows.flag')))
    $launcher = Join-Path $appDir 'tunnel-launcher.ps1'
    if (-not (Test-Path -LiteralPath $launcher)) { throw "tunnel-launcher.ps1 not found in $appDir" }

    $action = New-ScheduledTaskAction -Execute 'powershell.exe' `
        -Argument ('-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "{0}"' -f $launcher) `
        -WorkingDirectory $appDir
    $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" `
        -LogonType S4U -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
        -MultipleInstances IgnoreNew -ExecutionTimeLimit ([TimeSpan]::Zero)

    $taskArgs = @{ TaskName = $taskName; Action = $action; Principal = $principal; Settings = $settings; Force = $true; ErrorAction = 'Stop' }
    if ($EnableAtLogon) {
        $trigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
        $trigger.Delay = 'PT20S'
        $taskArgs.Trigger = $trigger
    }
    Register-ScheduledTask @taskArgs | Out-Null
}

function Get-RegistryValue {
    [CmdletBinding()]
    param([string]$LiteralPath, [string]$Name)
    try { return (Get-ItemProperty -LiteralPath $LiteralPath -Name $Name -ErrorAction Stop).$Name } catch { return $null }
}
function Set-ManagerStartup([bool]$Enabled) {
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $approvedKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'
    $backupFile = Join-Path $appDir 'discord-startup-backup.json'
    $name = 'Discord VPN Manager'
    if ($Enabled) {
        if (-not $ManagerPath -or -not (Test-Path -LiteralPath $ManagerPath -PathType Leaf)) { throw 'Manager executable not found. Save startup from the updated Manager.' }
        $source = [IO.Path]::GetFullPath($ManagerPath)
        if ([IO.Path]::GetFileName($source) -ne 'DiscordVPNManager.exe') { throw 'Invalid Manager executable name.' }
        $installed = Join-Path $appDir 'DiscordVPNManager.exe'
        if ($source -ne [IO.Path]::GetFullPath($installed)) {
            $needCopy = -not (Test-Path -LiteralPath $installed)
            if (-not $needCopy) { $needCopy = (Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $installed).Hash }
            if ($needCopy) { Copy-Item -LiteralPath $source -Destination $installed -Force }
        }
        if (-not (Test-Path -LiteralPath $backupFile)) {
            $discordRun = Get-RegistryValue -LiteralPath $runKey -Name Discord -ErrorAction SilentlyContinue
            $discordApproval = Get-RegistryValue -LiteralPath $approvedKey -Name Discord -ErrorAction SilentlyContinue
            $approvalBase64 = $null
            if ($null -ne $discordApproval) { $approvalBase64 = [Convert]::ToBase64String([byte[]]$discordApproval) }
            [ordered]@{ HadRun = ($null -ne $discordRun); Run = $discordRun; ApprovalBase64 = $approvalBase64 } |
                ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $backupFile -Encoding UTF8
        }
        Ensure-ScheduledTask -EnableAtLogon $true
        New-Item -Path $runKey -Force | Out-Null
        New-ItemProperty -LiteralPath $runKey -Name $name -Value ('"{0}" --startup' -f $installed) -PropertyType String -Force | Out-Null
        # Remove this entry's disabled state only when the user explicitly enables it.
        if (Test-Path -LiteralPath $approvedKey) { Remove-ItemProperty -LiteralPath $approvedKey -Name $name -ErrorAction SilentlyContinue }
        Remove-ItemProperty -LiteralPath $runKey -Name Discord -ErrorAction SilentlyContinue
        Set-Content -LiteralPath (Join-Path $appDir 'start-with-windows.flag') -Value enabled -Encoding ASCII
    } else {
        Ensure-ScheduledTask -EnableAtLogon $false
        Remove-ItemProperty -LiteralPath $runKey -Name $name -ErrorAction SilentlyContinue
        Remove-ItemProperty -LiteralPath $approvedKey -Name $name -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $backupFile) {
            $backup = Get-Content -LiteralPath $backupFile -Raw | ConvertFrom-Json
            $current = Get-RegistryValue -LiteralPath $runKey -Name Discord -ErrorAction SilentlyContinue
            if ($backup.HadRun -and -not $current) {
                New-ItemProperty -LiteralPath $runKey -Name Discord -Value $backup.Run -PropertyType String -Force | Out-Null
                $approvalBytes = $null
                if ($backup.ApprovalBase64) { $approvalBytes = [Convert]::FromBase64String($backup.ApprovalBase64) }
                elseif ($null -ne $backup.Approval) {
                    if ($backup.Approval.PSObject.Properties['value']) { $approvalBytes = [byte[]]$backup.Approval.value }
                    else { $approvalBytes = [byte[]]$backup.Approval }
                }
                if ($null -ne $approvalBytes) {
                    New-Item -Path $approvedKey -Force | Out-Null
                    New-ItemProperty -LiteralPath $approvedKey -Name Discord -Value $approvalBytes -PropertyType Binary -Force | Out-Null
                }
            }
            Remove-Item -LiteralPath $backupFile -Force
        }
        Remove-Item -LiteralPath (Join-Path $appDir 'start-with-windows.flag') -Force -ErrorAction SilentlyContinue
    }
}

if ($StartupMode) {
    try {
        Set-ManagerStartup -Enabled ($StartupMode -eq 'On')
        Write-Log "STARTUP: $StartupMode (Manager tray -> VPN ready -> Discord)"
        exit 0
    } catch { Write-Log "ERROR: $($_.Exception.Message)"; exit 1 }
}

if ($Stop) {
    $stopFlag = Join-Path $appDir 'stop.flag'
    # Drops the flag before killing so a restart racing this stop cannot bring
    # sing-box back after the user was told the VPN was off.
    try { Set-Content -LiteralPath $stopFlag -Value (Get-Date -Format o) -Encoding ASCII -Force } catch { }
    Remove-Item -LiteralPath (Join-Path $appDir 'vpn-ready.txt') -Force -ErrorAction SilentlyContinue
    Write-Log 'Stopping Discord VPN tunnel...'
    try {
        Stop-AllTunnel
    } catch {
        Write-Log "ERROR: $($_.Exception.Message)"
        exit 1
    }
    Remove-Item -LiteralPath $stopFlag -Force -ErrorAction SilentlyContinue
    Write-Log 'Tunnel stopped. Discord is back on the normal connection.'
    exit 0
}

# ---------- helpers ----------
function Get-ConfField {
    param([string]$Content, [string]$Name)
    $m = [regex]::Match($Content, '(?im)^\s*' + [regex]::Escape($Name) + '\s*=\s*(.+?)\s*$')
    if (-not $m.Success) { return $null }
    return (($m.Groups[1].Value -replace '\s+#.*$', '').Trim())
}

function Get-PhysicalInterfaceAlias {
    try {
        $routes = Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction Stop | Sort-Object -Property RouteMetric
        foreach ($r in $routes) {
            $a = Get-NetAdapter -InterfaceIndex $r.InterfaceIndex -ErrorAction SilentlyContinue
            if ($a -and $a.Status -eq 'Up' -and
                $a.Name -notlike 'tun*' -and
                $a.InterfaceDescription -notlike '*sing-tun*') {
                return $a.Name
            }
        }
    } catch { }
    return $null
}

function Test-TunUp {
    $a = Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object {
        $_.Status -eq 'Up' -and ($_.InterfaceDescription -like '*sing-tun*' -or $_.Name -like 'tun*')
    }
    return [bool]$a
}

function Test-Connectivity {
    $ok = $false
    try {
        $c = New-Object System.Net.Sockets.TcpClient
        $ar = $c.BeginConnect('1.1.1.1', 443, $null, $null)
        $ok = $ar.AsyncWaitHandle.WaitOne(5000, $false) -and $c.Connected
        $c.Close()
    } catch { $ok = $false }
    if (-not $ok) { return $false }
    try { [System.Net.Dns]::GetHostEntry('discord.com') | Out-Null } catch { return $false }
    return $true
}

try {
    Write-Log '=== Discord VPN: applying configuration ==='
    Write-Log "Conf: $ConfPath"

    if (-not (Test-Path -LiteralPath $appDir)) { throw "Discord Single-Tunneling is not installed at $appDir" }
    $singbox = Join-Path $appDir 'sing-box.exe'
    if (-not (Test-Path -LiteralPath $singbox)) { throw "sing-box.exe not found in $appDir" }
    if (-not (Test-Path -LiteralPath $ConfPath)) { throw "Conf file not found: $ConfPath" }

    $conf = Get-Content -LiteralPath $ConfPath -Raw
    $privateKey = Get-ConfField $conf 'PrivateKey'
    $addressRaw = Get-ConfField $conf 'Address'
    $publicKey  = Get-ConfField $conf 'PublicKey'
    $endpointRaw = Get-ConfField $conf 'Endpoint'
    $keepaliveRaw = Get-ConfField $conf 'PersistentKeepalive'
    $mtuRaw = Get-ConfField $conf 'MTU'

    if (-not $privateKey -or -not $addressRaw -or -not $publicKey -or -not $endpointRaw) {
        throw 'Could not read PrivateKey / Address / PublicKey / Endpoint from the .conf file.'
    }

    $keepalive = 25
    if ($keepaliveRaw -and [int]::TryParse($keepaliveRaw, [ref]$keepalive)) { } else { $keepalive = 25 }
    $wgMtu = 1408
    if ($mtuRaw -and [int]::TryParse($mtuRaw, [ref]$wgMtu)) { } else { $wgMtu = 1408 }

    if ($wgMtu -lt 1280 -or $wgMtu -gt 9000) { throw 'WireGuard MTU must be between 1280 and 9000.' }

    # Endpoint may be host:port or [ipv6]:port
    $epHost = $null; $epPort = 51820
    if ($endpointRaw -match '^\[(.+)\]:(\d+)$') { $epHost = $Matches[1]; $epPort = [int]$Matches[2] }
    elseif ($endpointRaw -match '^(.+):(\d+)$') { $epHost = $Matches[1]; $epPort = [int]$Matches[2] }
    else { $epHost = $endpointRaw }

    $parts = ($addressRaw -split ',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    $addr4 = $parts | Where-Object { $_ -notmatch ':' } | Select-Object -First 1
    $addr6 = $parts | Where-Object { $_ -match ':' } | Select-Object -First 1

    $endpointAddresses = @()
    $allowedIps = @()
    $tunAddresses = @('172.19.0.1/30')
    if ($addr4) { $endpointAddresses += $addr4; $allowedIps += '0.0.0.0/0' }
    if ($addr6) {
        $endpointAddresses += $addr6
        $allowedIps += '::/0'
        $nativeV6 = Get-NetRoute -AddressFamily IPv6 -DestinationPrefix '::/0' -ErrorAction SilentlyContinue |
            Where-Object { $_.InterfaceAlias -notlike 'tun*' -and $_.InterfaceAlias -notlike '*Teredo*' } | Select-Object -First 1
        if ($nativeV6) { $tunAddresses += 'fdfe:dcba:9876::1/126' }
    }

    $routeExclude = @()
    if ($epHost -match '^\d{1,3}(\.\d{1,3}){3}$') { $routeExclude += "$epHost/32" }
    elseif ($epHost -match ':') { $routeExclude += "$epHost/128" }

    # Capture all destinations so dynamic voice/video servers reach process rules.
    # Non-Discord traffic uses the direct outbound bound to the physical adapter.

    # Discord process names - matched at the network layer, so voice/video
    # (UDP/WebRTC) goes through the VPN too, not just chat (TCP).
    $discordProcesses = @(
        'Discord.exe', 'DiscordPTB.exe', 'DiscordCanary.exe', 'DiscordDevelopment.exe', 'Update.exe'
    )

    $physical = Get-PhysicalInterfaceAlias
    $directOutbound = [ordered]@{ type = 'direct'; tag = 'direct'; domain_resolver = 'dns-direct' }
    if ($physical) { $directOutbound['bind_interface'] = $physical }

    $wireguardEndpoint = [ordered]@{
        type = 'wireguard'
        tag = 'vpn'
        system = $false
        address = $endpointAddresses
        private_key = $privateKey
        mtu = $wgMtu
        detour = 'direct'
        peers = @(
            [ordered]@{
                address = $epHost
                port = $epPort
                public_key = $publicKey
                allowed_ips = $allowedIps
                persistent_keepalive_interval = $keepalive
            }
        )
    }

    $configObj = [ordered]@{
        log = [ordered]@{ level = 'warn'; output = 'sing-box.log'; timestamp = $true }
        dns = [ordered]@{
            servers = @(
                [ordered]@{ type = 'udp'; tag = 'dns-direct'; server = '1.1.1.1'; server_port = 53; detour = 'direct' }
            )
            final = 'dns-direct'
        }
        endpoints = @($wireguardEndpoint)
        inbounds = @(
            [ordered]@{
                type = 'tun'
                tag = 'tun-in'
                interface_name = 'tun-discord'
                address = $tunAddresses
                mtu = [Math]::Min(1280, $wgMtu)
                dns_mode = 'disabled'
                auto_route = $true
                strict_route = $false
                stack = 'gvisor'
                route_exclude_address = $routeExclude
            }
        )
        outbounds = @($directOutbound)
        route = [ordered]@{
            default_domain_resolver = 'dns-direct'
            rules = @(
                [ordered]@{ action = 'sniff' }
                [ordered]@{ process_name = $discordProcesses; action = 'route'; outbound = 'vpn' }
            )
            final = 'direct'
        }
    }

    $configPath = Join-Path $appDir 'config.json'
    $jsonText = $configObj | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($configPath, $jsonText, (New-Object System.Text.UTF8Encoding($false)))
    Write-Log "Config written: $configPath (direct interface: $physical)"

    $check = & $singbox check -c $configPath 2>&1
    if ($LASTEXITCODE -ne 0) { throw "sing-box rejected the generated config: $check" }

    if ($DryRun) {
        Write-Log 'DRY RUN: config generated and valid. Nothing was started or stopped.'
        exit 0
    }

    Write-Log 'Stopping old tunnel instance...'
    Stop-AllTunnel
    # A previous stop may have left the sentinel behind; clear it before the
    # launcher starts or it would exit immediately.
    Remove-Item -LiteralPath (Join-Path $appDir 'stop.flag') -Force -ErrorAction SilentlyContinue

    # A crashed run can leave a disabled sing-tun adapter behind; bring it up
    # so the next instance does not inherit it.
    Get-NetAdapter -IncludeHidden -ErrorAction SilentlyContinue |
        Where-Object { $_.InterfaceDescription -like '*sing-tun*' -and $_.Status -eq 'Disabled' } |
        ForEach-Object { Enable-NetAdapter -Name $_.Name -Confirm:$false -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 500

    try {
        Ensure-ScheduledTask
        Write-Log "Scheduled task '$taskName' registered/updated."
    } catch {
        Write-Log "WARNING: could not register the scheduled task ($($_.Exception.Message))."
    }

    Write-Log 'Starting tunnel via scheduled task...'
    $started = $false
    try {
        Start-ScheduledTask -TaskName $taskName -ErrorAction Stop
        $started = $true
    } catch {
        Write-Log "Scheduled task unavailable ($($_.Exception.Message)); starting sing-box directly."
    }
    if (-not $started) {
        Start-Process -FilePath $singbox -ArgumentList 'run -c config.json' -WorkingDirectory $appDir -WindowStyle Hidden | Out-Null
    }

    Write-Log 'Waiting for the TUN adapter...'
    $deadline = (Get-Date).AddSeconds(90)
    $tunOk = $false
    while ((Get-Date) -lt $deadline) {
        if (Test-TunUp) { $tunOk = $true; break }
        Start-Sleep -Milliseconds 750
    }

    $netOk = $false
    if ($tunOk) {
        foreach ($i in 1..8) {
            if (Test-Connectivity) { $netOk = $true; break }
            Start-Sleep -Seconds 3
        }
    }

    if ($tunOk -and $netOk) {
        Write-Log 'SUCCESS: tunnel is up and the normal connection still works.'
        Write-Log 'Open the "Discord (Tunneling)" shortcut to start Discord through the VPN.'
        exit 0
    }

    Write-Log 'FAILED: the tunnel did not come up healthy. Stopping it so you keep internet.'
    Stop-AllTunnel
    exit 1
}
catch {
    Write-Log "ERROR: $($_.Exception.Message)"
    try { Stop-AllTunnel } catch { }
    exit 1
}
