param([Parameter(Mandatory=$true)][string]$PublicAddress, [string]$BindAddress='0.0.0.0')
$ErrorActionPreference='Stop'
$runtime=Join-Path $PSScriptRoot '单机客户端\当前服务端'
$bin=Join-Path $runtime 'server-merged\bin'
& (Join-Path $bin 'Nanaimo.Server.exe') --data (Join-Path $runtime 'server-merged\data') --native (Join-Path $bin 'nanaimo_gameplay_bridge.exe') --profile (Join-Path $runtime 'config\profile.ini') --login-port 11005 --world-port 12050 --profile-port 11999 --bind-address $BindAddress --public-address $PublicAddress --log-directory (Join-Path $runtime 'server-merged')
if($LASTEXITCODE) {throw 'Server failed; inspect server-merged/server-error.log.'}
