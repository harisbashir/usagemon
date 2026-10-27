$ErrorActionPreference = 'Stop'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('usagemon-test-' + [guid]::NewGuid())
$original = $env:LOCALAPPDATA
try {
    $env:LOCALAPPDATA = $temp
    $script = Join-Path $PSScriptRoot '../scripts/claude-statusline.ps1'
    $shell = (Get-Process -Id $PID).Path
    $inputJson = '{"transcript_path":"private","session_id":"secret","rate_limits":{"five_hour":{"used_percentage":0,"resets_at":1800000000},"seven_day":{"used_percentage":75.5,"resets_at":1800100000}}}'
    $output = $inputJson | & $shell -NoProfile -File $script
    if ($LASTEXITCODE -ne 0) { throw 'Bridge failed' }
    $path = Join-Path $temp 'UsageMon/claude-usage.json'
    $text = [IO.File]::ReadAllText($path)
    $snapshot = $text | ConvertFrom-Json
    if ($snapshot.five_hour.used_percentage -ne 0 -or $snapshot.seven_day.used_percentage -ne 75.5) { throw 'Incorrect percentages' }
    if ($snapshot.five_hour.resets_at -ne 1800000000) { throw 'Incorrect reset' }
    if ($text -match 'private|secret|transcript|session_id') { throw 'Private data persisted' }
    if ($snapshot.observed_at -lt [DateTimeOffset]::UtcNow.AddMinutes(-1).ToUnixTimeSeconds()) { throw 'Missing observation time' }
    if ($output -notmatch '5h 0%') { throw 'Missing terminal status output' }
    # Missing limits must retain the last valid snapshot instead of replacing it with empty data.
    '{}' | & $shell -NoProfile -File $script | Out-Null
    if ([IO.File]::ReadAllText($path) -ne $text) { throw 'Missing limits erased good data' }
    '{bad' | & $shell -NoProfile -File $script | Out-Null
    if ([IO.File]::ReadAllText($path) -ne $text) { throw 'Malformed input erased good data' }
    Write-Host 'Claude bridge: normalization, privacy, zero usage, observation time, and invalid-input retention passed.'
} finally {
    $env:LOCALAPPDATA = $original
    if (Test-Path $temp) { Remove-Item $temp -Recurse -Force }
}
