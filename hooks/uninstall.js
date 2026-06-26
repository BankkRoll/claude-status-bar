#!/usr/bin/env node
// Removes the status-bar hooks from ~/.claude/settings.json and stops the running app.
// Other hooks are left intact. The settings backup and the app executable are not touched.

const fs = require("fs");
const os = require("os");
const path = require("path");
const cp = require("child_process");

const home = os.homedir();
const sbDir = path.join(home, ".claude", "statusbar");
const MARKER = sbDir; // every hook command added by install.js points inside this directory
const settingsPath = path.join(home, ".claude", "settings.json");

try { cp.execSync('taskkill /IM ClaudeStatusBar.exe /F', { stdio: "ignore" }); } catch {}

if (!fs.existsSync(settingsPath)) {
  console.log("No settings.json; nothing to remove.");
  process.exit(0);
}

const settings = JSON.parse(fs.readFileSync(settingsPath, "utf8"));
for (const evt of Object.keys(settings.hooks || {})) {
  settings.hooks[evt] = (settings.hooks[evt] || [])
    .map((e) => ({ ...e, hooks: (e.hooks || []).filter((h) => !(h.command || "").includes(MARKER)) }))
    .filter((e) => (e.hooks || []).length > 0);
  if (settings.hooks[evt].length === 0) delete settings.hooks[evt];
}

fs.writeFileSync(settingsPath, JSON.stringify(settings, null, 2) + "\n");
console.log("Removed status-bar hooks from", settingsPath);
