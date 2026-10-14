$ErrorActionPreference='Stop'
try {
    if ([IntPtr]::Size -ne 4) { throw 'Automatic friends requires 32-bit Windows PowerShell.' }
    [Console]::InputEncoding = New-Object Text.UTF8Encoding($false)
    $line = [Console]::In.ReadLine()
    if (-not $line -or $line.Length -gt 8192) { throw 'Missing launch configuration.' }
    $config = $line | ConvertFrom-Json
    $line = $null
    Add-Type -Path (Join-Path $PSScriptRoot 'FlightIsland.AutoFriends.dll')
    [FlightIslandNative.AutoFriends]::Run([int]$config.PID,[string]$config.Server,[string]$config.Account,[string]$config.Password,[string]$config.GamePath,$PSScriptRoot)
} catch {
    # Never serialize configuration or invocation arguments into the log.
    Add-Content -LiteralPath (Join-Path $PSScriptRoot 'auto-start-error.log') -Value ((Get-Date -Format o)+' '+$_.Exception.Message) -Encoding UTF8
    exit 1
} finally { $config=$null; $line=$null }
