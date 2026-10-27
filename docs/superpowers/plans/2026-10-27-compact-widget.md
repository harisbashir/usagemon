# Compact widget and simpler setup implementation plan

> **For agentic workers:** Use superpowers:executing-plans inline, with test-driven-development and one independent final code review.

**Goal:** Apply the user's requested 20% width reduction, percentage bars, easier Claude setup, and executable release assets.

**Architecture:** Keep WPF and the existing documented CLI integrations. Embed the Claude bridge in the app and add an explicit setup button that installs it into local application data and configures Claude's user settings. Existing custom status lines stay intact and receive manual integration guidance.

**Spec:** The user's follow-up: 520 logical pixels instead of 650, progress bars with percentage text overlaid, easier Claude setup where possible, and an EXE in the release. Existing reset countdowns, tray controls, missing-data semantics and position locking remain required.

## Tasks

- [x] Write failing tests for setup creation, unrelated-setting preservation, exact backup, repeated setup, legacy bridge upgrade, custom status-line protection, and invalid JSON retention.
- [x] Implement `ClaudeSetup.Configure(settingsPath, dataDirectory, bridgeContent)` and an embedded-resource loader. The setup button is the user's explicit action; do not touch any actual Claude settings while developing/testing.
- [x] Change the default layout and monitor fitting to 520 × 48. Use percentage bars with centered text for Claude, Codex, CPU and RAM; retain reset countdowns. Add native view-model and embedded-helper integration checks.
- [x] Add a direct self-contained EXE release asset that works without a scripts folder, plus the existing ZIPs and a framework-dependent EXE. Update setup UI, docs and preview.
- [x] Run core/bridge tests and cross-build; obtain read-only review; resolve material findings. Push and run Windows CI, then publish v0.2.0 using the successful CI's binaries and verify assets.

## Review focus

- Existing custom Claude status lines and unrelated JSON settings are preserved; malformed settings are never overwritten.
- The original settings file is backed up before changing it. Setup can be repeated safely and updates an older UsageMon bridge.
- A lone released EXE contains the bridge and can install it into a stable path.
- 520-pixel layout fits all six bars and countdowns at normal and narrow-screen DPI settings.
- Missing readings remain unavailable rather than appearing as zero.

## Completion evidence

Windows CI 40471145246 passed at 5a075d2: 45 core tests, three native Windows tests, both PowerShell bridge fixtures, and actual standalone EXE startup/520-DIP measurement. Main CI 40471365327 also passed. Release v0.2.0 includes direct self-contained/framework-dependent EXEs and matching ZIPs. Settings updates use an exclusive Windows file handle with exact backups and in-process rollback to protect concurrent edits.
