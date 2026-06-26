# Contributing

Thanks for your interest in improving Claude Status Bar for Windows.

## Project layout

```
src/      WPF application (C#, .NET 9)
hooks/    Claude Code hook scripts (Node.js) and the installer
assets/   Bundled resources (completion sound)
build.ps1 Build and packaging script
```

## Development setup

Requirements:

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js](https://nodejs.org)
- Windows 10 or 11

Build and run:

```powershell
.\build.ps1 -Install   # build and register the hooks against the local build
```

Then start a Claude Code session to exercise the widget. To debug the hooks, set
`CLAUDE_STATUSBAR_DEBUG=1` and inspect `~/.claude/statusbar/hooks.log`.

## How it fits together

The hooks write `~/.claude/statusbar/state.json`; the app polls that file and renders the
overlay. The two sides share the `state.json` contract documented in the README — keep them in
sync when changing it.

## Pull requests

- Keep changes focused; one concern per PR.
- Match the existing code style. Comments describe behavior, not history.
- Verify a clean build (`dotnet build src/ClaudeStatusBar.csproj -c Release`) with no warnings.
- Describe what you changed and how you tested it.

## Reporting bugs

Open an issue using the bug template and include your Windows version, whether you run a single
or multi-monitor setup, and the relevant lines from `hooks.log` when debugging is enabled.
