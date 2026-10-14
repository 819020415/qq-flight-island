param([Parameter(Mandatory=$true)][string]$Root)
$ErrorActionPreference='Stop'
$settings=Get-Content -LiteralPath (Join-Path $Root 'launchsettings.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if($settings.serverMode -eq 'merged') {
    $directory=Join-Path $Root 'server-merged'
    $exe=Join-Path $directory 'bin\Nanaimo.Server.exe'
    $native=Join-Path $directory 'bin\nanaimo_gameplay_bridge.exe'
    $profile=Join-Path $Root 'config\profile.ini'
    $data=Join-Path $directory 'data'
    $arguments='--data "'+$data+'" --native "'+$native+'" --profile "'+$profile+'" --login-port 11005 --world-port 12050 --profile-port 11999'
    $process=Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $Root `
        -RedirectStandardOutput (Join-Path $directory 'server.log') -RedirectStandardError (Join-Path $directory 'server-error.log') -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 700
    if($process.HasExited) {throw "Merged server exited. See server-merged/server-error.log."}
    exit 0
}
throw 'This project uses the merged server only.'
