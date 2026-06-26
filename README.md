# Claude Status Bar

<img width="800" height="450" alt="claude-status-bar-promo" src="https://github.com/user-attachments/assets/8ad21e17-1e09-4e8c-843e-d1ca850123f8" />

A small Windows taskbar widget that shows Claude Code's live status: an animated icon while it
is thinking or running a tool, rotating activity words, a yellow dot when it is awaiting your
permission, and the elapsed time of the current turn. It docks into the empty space of the
taskbar and resizes itself so it never overlaps the notification tray.

<a href="https://github.com/BankkRoll/claude-status-bar/releases/latest"><img width="250" alt="Download for Windows" src="https://github.com/user-attachments/assets/69b3f2e8-5c20-4dfa-b7d7-2341639ab734"></a>

```
…  ✦ Frolicking…  0m 43s   |   ^  🖥 🔊  7:11 PM
```

> Inspired by [m1ckc3s/claude-status-bar](https://github.com/m1ckc3s/claude-status-bar) (a macOS
> menu-bar app). This is a full rewrite to native C and Windows — though the hook idea
> and some pieces of the `state.json` contract were drawn from the original.

## What it shows

- **Thinking** — the icon animates with a rotating activity word and a live `1m 1s` timer.
- **Running a tool** — a label such as `Editing`, `Reading`, `Running command`, or `Searching`.
- **Awaiting permission** — a paused yellow dot.
- **Idle / done** — rests on the Claude mark.

The widget tracks the taskbar height and DPI, and anchors itself just before the tray, reflowing
when tray icons appear or disappear.

## Menu

Click the widget (either mouse button) to open its menu:

- **Show timer** — toggle the elapsed-time readout.
- **Play Completion Sound** — a chime when a turn finishes.
- **Animation** — Claude Spark, Claude Code, or Crab Walking.
- **Show on** — choose which monitors display the widget (defaults to the primary monitor).
- **Quit** — close the widget; it returns on your next Claude Code session.

## Install

1. Download the latest `ClaudeStatusBar-win-x64.zip` from
   [Releases](https://github.com/BankkRoll/claude-status-bar/releases/latest) and extract it.
2. Double-click **setup.bat**. A small window opens, registers the hooks for the current user,
   prints a confirmation, and waits for a key press.
3. Start a new Claude Code session — the widget appears whenever Claude Code is running, and
   hides itself when no session is active.

To remove it, run **uninstall.bat** (or the command under [Uninstall](#uninstall)), then delete
the folder.

## Requirements

- Windows 10 or 11
- [Claude Code](https://claude.com/claude-code) (CLI)
- [Node.js](https://nodejs.org) — runs the hooks

The release build bundles the .NET runtime, so no separate install is needed.

## How it works

The Claude Code hooks write the current status to `~/.claude/statusbar/state.json`; the widget
polls that file and renders the icon and label.

```
Claude Code  ->  hooks  ->  ~/.claude/statusbar/state.json  ->  widget (polls 0.4s)
```

`SessionStart` launches the widget and `SessionEnd` removes the session; the widget exits on its
own once no sessions remain (each active session is a file under `~/.claude/statusbar/sessions.d`).

The widget is a borderless, top-most overlay. It locates the tray via
`Shell_TrayWnd → TrayNotifyWnd` and anchors itself just before it, re-asserting its z-order each
poll so it stays on top. One overlay window is created per selected monitor. No Explorer injection
or shell extensions are used.

## Build from source

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```powershell
.\build.ps1 -SelfContained -Zip   # -> dist\ClaudeStatusBar-win-x64.zip
.\build.ps1 -Install              # build and register the hooks in one step
```

The hook registration done by `setup.bat` runs `hooks\install.js`, which merges the hooks into
`~/.claude/settings.json` (backing it up once to `settings.json.bak-statusbar` and leaving other
hooks intact), copies the hook scripts to `~/.claude/statusbar`, and records the executable path.
It is safe to run repeatedly.

## Uninstall

Run `uninstall.bat`, or:

```powershell
node "%USERPROFILE%\.claude\statusbar\uninstall.js"
```

Removes the status-bar hooks and stops the running widget, leaving other hooks intact. Then delete
the application folder.

## Debugging

Set `CLAUDE_STATUSBAR_DEBUG=1` before starting a session to append hook activity to
`~/.claude/statusbar/hooks.log`.

## Trademark

Unofficial and not affiliated with, endorsed by, or sponsored by Anthropic. "Claude" and the
Claude mark are trademarks of Anthropic, used nominatively. The MIT license covers the source code
only.

Please don't contact Anthropic about this project. To report a bug, request a feature, or raise any
other issue, open an issue at
[github.com/BankkRoll/claude-status-bar/issues](https://github.com/BankkRoll/claude-status-bar/issues).

## License

MIT
