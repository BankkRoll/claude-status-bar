#!/usr/bin/env node
// Installer. Merges the status-bar hooks into ~/.claude/settings.json, preserving any other
// hooks, copies the hook scripts to ~/.claude/statusbar/, and records the path to
// ClaudeStatusBar.exe for lifecycle.js. Safe to run repeatedly; existing status-bar hooks are
// removed before being re-added.
// Usage: node install.js [path\to\ClaudeStatusBar.exe]

const fs = require("fs");
const os = require("os");
const path = require("path");

const home = os.homedir();
const sbDir = path.join(home, ".claude", "statusbar");
const MARKER = sbDir; // every hook command we add points inside this dir
const updateDest = path.join(sbDir, "update.js");
const lifecycleDest = path.join(sbDir, "lifecycle.js");
const settingsPath = path.join(home, ".claude", "settings.json");
const exePathFile = path.join(sbDir, "app-path.txt");
const node = process.execPath;

fs.mkdirSync(sbDir, { recursive: true });
fs.copyFileSync(path.join(__dirname, "update.js"), updateDest);
fs.copyFileSync(path.join(__dirname, "lifecycle.js"), lifecycleDest);

// Record the exe location for the launcher. Prefer an explicit arg; else guess next to install.js.
const exeArg = process.argv[2];
const guessed = path.join(__dirname, "..", "ClaudeStatusBar.exe");
const exe = exeArg || (fs.existsSync(guessed) ? guessed : "");
if (exe) fs.writeFileSync(exePathFile, path.resolve(exe));
else console.log("(!) No exe path given and none found; set", exePathFile, "manually.");

// Hook commands. Quote paths for spaces; forward slashes are fine for node on Windows.
const q = (p) => `"${p}"`;
const cmd = (evt) => `node ${q(updateDest)} ${evt}`;
const life = (evt) => `node ${q(lifecycleDest)} ${evt}`;

let settings = {};
if (fs.existsSync(settingsPath)) {
  settings = JSON.parse(fs.readFileSync(settingsPath, "utf8"));
  const bak = settingsPath + ".bak-statusbar";
  if (!fs.existsSync(bak)) fs.copyFileSync(settingsPath, bak);
}
settings.hooks = settings.hooks || {};

const stripOurs = (arr) =>
  (arr || [])
    .map((entry) => ({
      ...entry,
      hooks: (entry.hooks || []).filter((h) => !(h.command || "").includes(MARKER)),
    }))
    .filter((entry) => (entry.hooks || []).length > 0);

const addUnmatched = (evt, command) => {
  settings.hooks[evt] = stripOurs(settings.hooks[evt]);
  settings.hooks[evt].push({ hooks: [{ type: "command", command }] });
};
const addMatched = (evt, command) => {
  settings.hooks[evt] = stripOurs(settings.hooks[evt]);
  settings.hooks[evt].push({ matcher: "*", hooks: [{ type: "command", command }] });
};

// Status hooks (drive the animation/label)
addUnmatched("UserPromptSubmit", cmd("prompt"));
addMatched("PreToolUse", cmd("pre"));
addMatched("PostToolUse", cmd("post"));
addUnmatched("Notification", cmd("notify"));
addMatched("PermissionRequest", cmd("permreq"));
addUnmatched("Stop", cmd("stop"));
// Lifecycle hooks (launch the app on open; the app quits itself when no longer needed)
addUnmatched("SessionStart", life("start"));
addUnmatched("SessionEnd", life("end"));

fs.writeFileSync(settingsPath, JSON.stringify(settings, null, 2) + "\n");
console.log("Installed status-bar hooks into", settingsPath);
console.log("Scripts:", updateDest, "and", lifecycleDest);
if (exe) console.log("App path recorded:", path.resolve(exe));
console.log("Backup (first run only):", settingsPath + ".bak-statusbar");
