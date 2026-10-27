# Receives Claude Code's documented statusLine JSON on stdin.
# Persists only usage windows, never the input payload or credentials. Compatible with PowerShell 5.1+.
$ErrorActionPreference = 'Stop'
$temporary = $null
try {
    $inputData = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $limits = $inputData.rate_limits
    if ($null -eq $limits) { return }

    function Convert-Window($window) {
        if ($null -eq $window -or $null -eq $window.used_percentage) { return $null }
        $number = $window.used_percentage
        if ($number -isnot [int] -and $number -isnot [long] -and $number -isnot [double] -and $number -isnot [decimal]) { return $null }
        $used = [double]$number
        if ([double]::IsNaN($used) -or [double]::IsInfinity($used) -or $used -lt 0 -or $used -gt 100) { return $null }
        $reset = $null
        $epoch = 0L
        if ($null -ne $window.resets_at -and [long]::TryParse([string]$window.resets_at, [ref]$epoch)) {
            try { $reset = [DateTimeOffset]::FromUnixTimeSeconds($epoch).ToUnixTimeSeconds() } catch { }
        }
        return @{ used_percentage = $used; resets_at = $reset }
    }

    $five = Convert-Window $limits.five_hour
    $week = Convert-Window $limits.seven_day
    if ($null -eq $five -and $null -eq $week) { return }
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) { throw 'LOCALAPPDATA unavailable' }
    $directory = Join-Path $env:LOCALAPPDATA 'UsageMon'
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $destination = Join-Path $directory 'claude-usage.json'
    $temporary = Join-Path $directory ('claude-' + [guid]::NewGuid().ToString('N') + '.tmp')
    $snapshot = @{
        observed_at = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
        five_hour = $five
        seven_day = $week
    } | ConvertTo-Json -Depth 4 -Compress
    [IO.File]::WriteAllText($temporary, $snapshot, (New-Object Text.UTF8Encoding($false)))
    if ([IO.File]::Exists($destination)) {
        [IO.File]::Replace($temporary, $destination, $null)
    } else {
        try { [IO.File]::Move($temporary, $destination) }
        catch [IO.IOException] { [IO.File]::Replace($temporary, $destination, $null) }
    }
    $parts = @()
    if ($null -ne $five) { $parts += ('5h {0:0}%' -f $five.used_percentage) }
    if ($null -ne $week) { $parts += ('week {0:0}%' -f $week.used_percentage) }
    [Console]::WriteLine(($parts -join ' | '))
} catch {
    # Avoid interrupting Claude Code or printing input data on malformed/missing reports.
    [Console]::Error.WriteLine('UsageMon: usage snapshot was not updated.')
} finally {
    if ($null -ne $temporary -and [IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
}
