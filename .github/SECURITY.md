# Security Policy

## Reporting a vulnerability

Please report security issues privately using GitHub's
[private vulnerability reporting](https://github.com/BankkRoll/claude-status-bar/security/advisories/new)
rather than opening a public issue.

Include a description, steps to reproduce, and the affected version. You can expect an initial
response within a few days.

## Scope

This is a local desktop utility. Its only privileged actions are writing the Claude Code hook
entries in `~/.claude/settings.json` and launching its own bundled executable. It makes no network
requests.
