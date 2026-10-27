# Windows manual smoke test

Automated checks cover parsers, display state, bridge privacy, real stdio transport, Windows native metrics and basic window placement. These interactive checks require an actual Windows desktop; a cross-build does not establish that they passed.

- [ ] On Windows 10/11 x64 without .NET installed, download only the standalone release EXE into a permanent writable folder and run it without any scripts folder. Confirm there is no console window and no taskbar/Alt-Tab entry.
- [ ] CPU and RAM readings appear within two seconds and change with system activity. Compare with Task Manager across multiple samples; CPU sampling intervals differ, so values need not match exactly.
- [ ] Right-click any empty area and use the three-dot button; both open controls. Double-click the tray icon to restore a hidden widget. Exit removes the icon and stops UsageMon's Codex child process tree.
- [ ] With Codex CLI installed and signed in, values agree with Codex's usage display. Test an account with only a weekly window. Five-hour stays unavailable instead of showing weekly usage under 5h.
- [ ] With current Claude Code, click Set up Claude automatically and verify settings backup and unrelated properties are preserved, then restart Claude Code and start using Claude Code. Validate five-hour/weekly usage and reset dates. If rate_limits is not yet available, keep the widget in its waiting state.
- [ ] Stop Claude Code. After five minutes, its snapshot is explicitly stale. Resume work and check that a new snapshot clears stale status. Website-only usage does not independently refresh this bridge.
- [ ] Disconnect the network or sign Codex out. Prior data stays visible but stale, CPU/RAM keep working, and restoring access plus Refresh reconnects.
- [ ] Drag over a clear taskbar area, switch between other apps, open Start, minimize/maximize apps, and restart Explorer. The widget returns above the taskbar without taking focus. It covers the taskbar controls beneath its own rectangle.
- [ ] Lock position, attempt a drag, restart UsageMon, and verify saved position and lock. Unlock, move it, and verify again.
- [ ] Check reset countdowns including under one minute, a reset passing while offline, and missing reset timestamps. Passed resets show Updating until authoritative data arrives.
- [ ] Move between monitors at 100%, 150%, and 200% scaling. Unplug the saved monitor and restart. The widget remains visible and usable. Verify all four taskbar edges on supported configurations; auto-hidden taskbars use bottom-right fallback placement.
- [ ] Enable Launch at sign-in, sign out and back in, then disable it and confirm the registry value is removed. No administrator rights should be required.
- [ ] Suspend/resume Windows and check metrics, countdowns, and provider recovery.
- [ ] Measure UsageMon.exe idle private working set and CPU separately from the installed Codex app-server process. Record actual results rather than assigning an unmeasured footprint claim.

Release status: Windows build and automated native checks are reported by GitHub Actions. Interactive checks remain unverified until checked on a real desktop.

- [ ] Check the 520 × 48 compact layout. Each provider, CPU, and RAM percentage sits over its progress bar; reset countdowns remain visible. Check 0%, 100%, and unavailable values.
- [ ] With an existing custom Claude status line, automatic setup leaves the settings file unchanged and offers manual integration. With malformed settings, setup leaves the file intact and explains the error.
