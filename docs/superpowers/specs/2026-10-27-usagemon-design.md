# UsageMon design

## Intent

Build a lightweight Windows widget for Haris that keeps Claude subscription usage, Codex subscription usage, reset countdowns, and basic CPU/RAM usage visible while working. Publish the source to the public GitHub repository `harisbashir/usagemon`.

## Agreed requirements

- Native C# / WPF application.
- Floating, always-on-top widget that can be placed over the taskbar.
- Claude and Codex five-hour and weekly usage, where the account supplies those windows.
- Time remaining to each reset.
- Basic system CPU and physical RAM usage.
- Public GitHub repository under `harisbashir/usagemon`.

## Approaches

Native WPF was recommended for Windows integration, low overhead, and direct system metrics. Tauri/Rust was offered for web styling flexibility at the cost of additional build complexity. The user selected WPF. A floating overlay was explicitly accepted rather than integration inside Explorer's taskbar.

## Proposed behavior and builder decisions

- Target supported Windows 10/11 desktop systems using .NET 10 WPF; ship a portable Windows x64 build initially, with documented build commands.
- Compact horizontal dark strip, approximately taskbar height, with separate Claude, Codex, and CPU/RAM groups. Each provider shows labeled 5h/week percentages consumed and abbreviated countdowns. Tooltips expose full reset dates, connection status, and last update time. Width can accommodate large display text without clipping.
- Borderless, always on top, hidden from Alt-Tab and the taskbar, and movable by dragging. Save position and lock state. Allow placement within taskbar bounds, snapping to the taskbar on request. Keep on a visible monitor after display changes. Do not reserve taskbar space or interfere with Explorer internals. The overlay occupies its own clickable rectangle, so the user chooses a clear part of the taskbar.
- Tray menu: show/hide, lock/unlock, snap to taskbar, refresh, connection/setup help, launch at sign-in opt-in, exit. Closing hides the widget; exit releases resources and child processes. Settings are local to the current Windows user.
- CPU and RAM sample every two seconds using Windows APIs. Countdown rendering refreshes every second. Network/provider work runs off the UI thread. Provider failures never stop system metrics or the other provider.
- Missing windows display unavailable, never a fabricated percentage. Preserve a failed provider's last successful snapshot but visibly mark it stale. A passed reset time displays awaiting update; do not infer that usage reset to zero.

## Codex connection

Use an installed Codex CLI signed in to the user's ChatGPT subscription. Launch `codex app-server` over redirected standard input/output, perform the documented initialize/initialized handshake, and call `account/rateLimits/read`. Select the `codex` bucket when a multi-bucket response exists. Map windows by `windowDurationMins`, not primary/secondary position: 300 minutes is five hours; 10080 minutes is weekly. Preserve explicit unsupported-window states. Poll every 60 seconds, serialize requests, bound request times, and reconnect with bounded backoff. Dispose the process tree on exit. Do not start inference or purchase/reset usage. Setup help explains installing the CLI and using `codex login`; no API key is needed.

## Claude connection

Use Claude Code's documented status-line input: `rate_limits.five_hour` and `rate_limits.seven_day` with `used_percentage` and epoch-second `resets_at`. Include a PowerShell bridge that reads stdin and writes only the normalized usage snapshot and observation time atomically to the current user's local application-data directory. Never persist conversation text or the complete input payload. The widget reads that snapshot periodically. Setup provides an exact statusLine command; it must not overwrite an existing status line automatically. Document how to incorporate the bridge into an existing script. Claude Code must be installed, subscription-authenticated, and active enough to emit status updates; website-only activity will not independently refresh this bridge. Until the first valid report, show setup required. After five minutes without a report, retain usage with a visible stale indicator and observation time. No direct reuse of Claude OAuth secrets or undocumented subscription endpoints.

## Structure

- Core library: normalized usage records, provider response parsers, countdown formatting, and freshness semantics; testable without Windows.
- WPF app: widget, tray behavior, local settings, Windows metrics, Codex process transport, and Claude snapshot reader.
- PowerShell bridge and setup documentation.
- Tests for parser/window selection, absent and malformed data, epoch/countdown boundaries, and stale state. Bridge fixtures verify only usage data is persisted. Windows build automation compiles/tests and publishes portable output; manual smoke checklist covers taskbar stacking, dragging, DPI, multiple monitors, shutdown, and resume.

## Non-goals and constraints

No chat interface, token spending estimator, billing dashboard, server, browser scraping, or taskbar injection. No fixed CPU/RAM promise before Windows measurement. CLI processes contribute to total footprint, so report that separately when measuring. Keep dependencies minimal.

This workspace is empty, Linux-hosted, lacks dotnet, and contains a read-only placeholder `.git` directory rather than a usable Git repository. Shell network access cannot currently resolve github.com; `gh auth status` reports invalid authentication for harisbashir. These limit local compilation, committing, repository creation, and pushing. Source and automation can be prepared locally after design approval; do not claim Windows runtime verification or GitHub publication without successful evidence. Try available supported network/build capabilities before declaring either permanently blocked.

## Acceptance

On Windows, a user can run the portable build, see live CPU/RAM, connect installed subscription-authenticated CLIs following setup instructions, view correct available limit windows and reset countdowns, place and lock the widget over the taskbar, and restore its position on restart. Missing credentials/data are actionable and stale data is obvious. Automated core tests and Windows build must pass before calling the release verified. Public repository creation and upload require a successful GitHub connection.

## Primary references

- https://learn.chatgpt.com/docs/app-server — initialize handshake and account/rateLimits/read.
- https://github.com/openai/codex/blob/main/codex-rs/app-server-protocol/src/protocol/v2/account.rs — rate-limit response definitions.
- https://code.claude.com/docs/en/statusline — documented rate_limits fields and Windows script setup.
