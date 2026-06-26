#!/usr/bin/env node
// SessionStart/SessionEnd hook. Launches ClaudeStatusBar.exe and tracks each session as a file
// under sessions.d/; the app exits on its own once no sessions remain.
// Usage: node lifecycle.js <start|end>   (hook payload, including session_id, arrives on stdin)
// Set CLAUDE_STATUSBAR_DEBUG=1 to append activity to ~/.claude/statusbar/hooks.log.

const fs = require("fs");
const os = require("os");
const path = require("path");
const cp = require("child_process");

const EXE = "ClaudeStatusBar.exe";
const dir = path.join(os.homedir(), ".claude", "statusbar");
const sessDir = path.join(dir, "sessions.d");
const statePath = path.join(dir, "state.json");
const exePathFile = path.join(dir, "app-path.txt"); // absolute path to the exe, written by install.js
const event = process.argv[2];

fs.mkdirSync(sessDir, { recursive: true });

function debugLog(line) {
  if (process.env.CLAUDE_STATUSBAR_DEBUG !== "1") return;
  try { fs.appendFileSync(path.join(dir, "hooks.log"), `${new Date().toISOString()} [lifecycle:${event}] ${line}\n`); } catch {}
}

// Whether ClaudeStatusBar.exe is currently running.
function running() {
  try {
    const out = cp.execSync(`tasklist /FI "IMAGENAME eq ${EXE}" /NH`, { encoding: "utf8" });
    return out.includes(EXE);
  } catch { return false; }
}

const safeId = (s) => String(s || "").replace(/[^A-Za-z0-9_.-]/g, "").slice(0, 64) || "unknown";

function launch() {
  let exe = "";
  try { exe = fs.readFileSync(exePathFile, "utf8").trim(); } catch {}
  if (!exe || !fs.existsSync(exe)) return;
  const child = cp.spawn(exe, [], { detached: true, stdio: "ignore", windowsHide: true });
  child.unref();
}

// Reset the status to idle when the owning session ends without a Stop event. The session-id
// check ensures a different session's active turn is left untouched.
function clearStaleState(id) {
  try {
    const prev = JSON.parse(fs.readFileSync(statePath, "utf8"));
    if (safeId(prev.sessionId) !== id) return;
    if (!["thinking", "tool", "permission"].includes(prev.state)) return;
    const out = { ...prev, state: "idle", label: "", startedAt: 0, ts: Math.floor(Date.now() / 1000) };
    const tmp = statePath + "." + process.pid + ".tmp";
    fs.writeFileSync(tmp, JSON.stringify(out));
    fs.renameSync(tmp, statePath);
  } catch {}
}

let input = "", done = false;
process.stdin.on("data", (d) => (input += d));
process.stdin.on("end", run);
process.stdin.on("error", run);
setTimeout(run, 1000); // fall back if stdin never closes, so the session is never blocked

function run() {
  if (done) return; done = true;
  let id = "";
  try { id = JSON.parse(input).session_id; } catch {}
  id = safeId(id);

  if (event === "start") {
    // When the app is not running, any leftover session files are stale; clear them first.
    if (!running()) { try { for (const f of fs.readdirSync(sessDir)) fs.rmSync(path.join(sessDir, f), { force: true }); } catch {} }
    try { fs.writeFileSync(path.join(sessDir, id), ""); } catch {}
    clearStaleState(id);
    launch();
  } else if (event === "end") {
    try { fs.rmSync(path.join(sessDir, id), { force: true }); } catch {}
    clearStaleState(id);
  }
  debugLog(`session=${id} sessions=${(() => { try { return fs.readdirSync(sessDir).length; } catch { return "?"; } })()}`);
  process.exit(0);
}
