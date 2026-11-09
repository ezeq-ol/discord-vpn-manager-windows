param()
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$spec=Get-Content -LiteralPath (Join-Path $root 'runtime.json') -Raw|ConvertFrom-Json
$cache=Join-Path $root '.cache'
New-Item -ItemType Directory -Path $cache -Force|Out-Null
$archive=Join-Path $cache 'runtime.zip'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
if(-not(Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $spec.archiveSHA256){
 Invoke-WebRequest -UseBasicParsing -Uri $spec.url -OutFile $archive
}
if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $spec.archiveSHA256){throw 'Runtime archive SHA256 mismatch'}
$stage=Join-Path $cache 'runtime'
Expand-Archive -LiteralPath $archive -DestinationPath $stage -Force
foreach($prop in $spec.files.PSObject.Properties){
 if($prop.Name -notin @('sing-box.exe','wintun.dll')){throw 'Unexpected runtime file'}
 $file=Join-Path $stage $prop.Name
 if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $prop.Value){throw ('Runtime file SHA256 mismatch: '+$prop.Name)}
 Copy-Item -LiteralPath $file -Destination (Join-Path $root $prop.Name) -Force
}
Write-Host 'Runtime restored and SHA256 verified.'
