param([Parameter(Mandatory = $true)][string]$Executable)
$ErrorActionPreference = 'Stop'
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('usagemon-standalone-' + [guid]::NewGuid())
$process = $null
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class UsageMonPackageWindow {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
}
'@
try {
    [IO.Directory]::CreateDirectory($temporary) | Out-Null
    $standalone = Join-Path $temporary 'UsageMon.exe'
    Copy-Item $Executable $standalone
    $info = [Diagnostics.ProcessStartInfo]::new($standalone)
    $info.UseShellExecute = $false
    $info.Environment['CLAUDE_CONFIG_DIR'] = Join-Path $temporary '.claude'
    $process = [Diagnostics.Process]::Start($info)
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw "Standalone executable exited early: $($process.ExitCode)" }
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline)
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Standalone executable did not show a widget.' }
    # Let the Loaded handler apply monitor fitting before measuring the actual release binary.
    Start-Sleep -Milliseconds 1000
    $rectangle = [UsageMonPackageWindow+Rect]::new()
    if (-not [UsageMonPackageWindow]::GetWindowRect($process.MainWindowHandle, [ref]$rectangle)) { throw 'Cannot measure widget.' }
    $dpi = [UsageMonPackageWindow]::GetDpiForWindow($process.MainWindowHandle)
    $width = ($rectangle.Right - $rectangle.Left) * 96.0 / $dpi
    if ([Math]::Abs($width - 520) -gt 2) { throw "Expected a 520-DIP widget, got $width." }
    if (Test-Path (Join-Path $temporary 'scripts')) { throw 'Standalone check unexpectedly acquired sidecar scripts.' }
    Write-Host 'Standalone EXE: starts without sidecar files and displays the 520-DIP widget.'
} finally {
    if ($null -ne $process) {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit(5000) | Out-Null }
        $process.Dispose()
    }
    if (Test-Path $temporary) { Remove-Item $temporary -Recurse -Force }
}
