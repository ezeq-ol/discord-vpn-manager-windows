param([switch]$RestoreRuntime)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$runtime=@('sing-box.exe','wintun.dll')
if($RestoreRuntime -or @($runtime|Where-Object{-not(Test-Path -LiteralPath (Join-Path $root $_))}).Count -gt 0){
    & (Join-Path $root 'restore-runtime.ps1')
}
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not(Test-Path -LiteralPath $csc)){throw '.NET Framework compiler not found. Build on Windows with .NET Framework 4.x.'}
$out=Join-Path $root 'dist'
New-Item -ItemType Directory -Path $out -Force|Out-Null
$exe=Join-Path $out 'DiscordVPNManager.exe'
$arguments=@('/nologo','/target:winexe','/optimize+','/langversion:5',('/out:"{0}"' -f $exe),('/win32icon:"{0}"' -f (Join-Path $root 'app.ico')),'/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll')
foreach($name in @('logo-transparent.png','Aplicar-VPN-Discord.ps1','tunnel-launcher.ps1','discord-waiter.ps1','wintun.dll','sing-box.exe')){
 $arguments+=('/resource:"{0}",{1}' -f (Join-Path $root $name),$name)
}
$sources=@(Get-ChildItem -LiteralPath $root -File -Filter '*.cs')+@(Get-ChildItem -LiteralPath (Join-Path $root 'DiscordVPNManager') -File -Filter '*.cs')+@(Get-ChildItem -LiteralPath (Join-Path $root 'Properties') -File -Filter '*.cs')
$arguments+=@($sources|ForEach-Object{'"'+$_.FullName+'"'})
$response=Join-Path $out 'compile.rsp'
[IO.File]::WriteAllLines($response,$arguments,(New-Object Text.UTF8Encoding($true)))
& $csc ('@'+$response)
if($LASTEXITCODE -ne 0){throw 'Compilation failed'}
Write-Host "Built: $exe"
