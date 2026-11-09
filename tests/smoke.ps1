param()
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
foreach($name in @('Aplicar-VPN-Discord.ps1','tunnel-launcher.ps1','discord-waiter.ps1')){
 $errors=$null
 [Management.Automation.Language.Parser]::ParseFile((Join-Path $root $name),[ref]$null,[ref]$errors)|Out-Null
 if($errors){throw ($errors|Out-String)}
}
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe=Join-Path $root 'dist\SmokeTests.exe'
& $csc /nologo ('/out:'+ $exe) /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'SmokeTests.cs')
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& $exe (Join-Path $root 'dist\DiscordVPNManager.exe')
if($LASTEXITCODE -ne 0){throw 'Smoke test failed'}
