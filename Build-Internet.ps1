$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$runtime = Join-Path $root '单机客户端\当前服务端'
# Build into isolated outputs before touching installed programs.
$stage = Join-Path $root ('internet-build-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
dotnet publish (Join-Path $root '当前服务端\源码\managed-host\Nanaimo.Server.csproj') -c Release -f net8.0 -r win-x64 --self-contained true -o (Join-Path $stage 'server') --nologo
if ($LASTEXITCODE) { throw 'Server build failed; installed files unchanged.' }
dotnet publish (Join-Path $root '登录器源码\Nanaimo.Launcher.csproj') -c Release -f net8.0-windows -r win-x64 --self-contained true -o (Join-Path $stage 'launcher') --nologo
if ($LASTEXITCODE) { throw 'Launcher build failed; installed files unchanged.' }
$serverBin = Join-Path $runtime 'server-merged\bin'
$launcherBin = Join-Path $runtime 'launcher'
if (!(Test-Path (Join-Path $serverBin 'nanaimo_gameplay_bridge.exe'))) { throw 'Existing native bridge is missing.' }
$backup = Join-Path $stage 'backup'
New-Item -ItemType Directory -Path $backup -Force | Out-Null
Copy-Item $serverBin (Join-Path $backup 'server') -Recurse
Copy-Item $launcherBin (Join-Path $backup 'launcher') -Recurse
Copy-Item (Join-Path $stage 'server\*') $serverBin -Recurse -Force
Copy-Item (Join-Path $stage 'launcher\*') $launcherBin -Recurse -Force
Write-Host "Build installed. Backup: $backup"
