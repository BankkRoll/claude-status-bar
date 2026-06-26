using System.IO;
using System.Text.Json;

namespace ClaudeStatusBar;

/// <summary>
/// Status written by the Claude Code hooks. Field names match the JSON keys the hooks emit.
/// </summary>
public sealed class ClaudeState
{
    public string State { get; set; } = "idle"; // idle | thinking | tool | permission | done
    public string Label { get; set; } = "";
    public string Tool { get; set; } = "";
    public string Project { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string Transcript { get; set; } = "";
    public double StartedAt { get; set; }
    public double Ts { get; set; }
}

/// <summary>
/// Reads the hook-written status file, re-parsing only when it changes on disk, and counts the
/// active sessions tracked under sessions.d. Active states older than 15 minutes, or whose
/// transcript ends with an interruption marker, are reported as idle.
/// </summary>
public sealed class StateStore
{
    public static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "statusbar");

    private static readonly string StatePath = Path.Combine(Dir, "state.json");
    private static readonly string SessionsDir = Path.Combine(Dir, "sessions.d");

    private DateTime _lastWrite = DateTime.MinValue;
    private ClaudeState _current = new();

    /// <summary>Re-read the status file if it changed, then return the effective state.</summary>
    public ClaudeState Poll()
    {
        try
        {
            var info = new FileInfo(StatePath);
            if (info.Exists && info.LastWriteTimeUtc != _lastWrite)
            {
                _lastWrite = info.LastWriteTimeUtc;
                _current = Parse(File.ReadAllText(StatePath));
            }
        }
        catch { }

        return Effective(_current);
    }

    /// <summary>Number of active sessions (one file per session under sessions.d).</summary>
    public int SessionCount()
    {
        try { return Directory.Exists(SessionsDir) ? Directory.GetFiles(SessionsDir).Length : 0; }
        catch { return 0; }
    }

    private static ClaudeState Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string S(string key) => root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        double N(string key) => root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
        var state = S("state");
        return new ClaudeState
        {
            State = state.Length == 0 ? "idle" : state,
            Label = S("label"),
            Tool = S("tool"),
            Project = S("project"),
            SessionId = S("sessionId"),
            Transcript = S("transcript"),
            StartedAt = N("startedAt"),
            Ts = N("ts"),
        };
    }

    private static ClaudeState Effective(ClaudeState s)
    {
        if (s.State is "thinking" or "tool" or "permission")
        {
            double age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - s.Ts;
            if (age > 900 ||
                (!string.IsNullOrEmpty(s.Transcript) && LastLineContains(s.Transcript, "interrupted by user")))
            {
                return new ClaudeState { State = "idle", Project = s.Project, SessionId = s.SessionId };
            }
        }
        return s;
    }

    private static bool LastLineContains(string path, string needle)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long start = Math.Max(0, fs.Length - 8192);
            fs.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            var text = reader.ReadToEnd();
            for (int i = text.Length; i > 0;)
            {
                int nl = text.LastIndexOf('\n', i - 1);
                var line = text.Substring(nl + 1, i - (nl + 1)).Trim();
                if (line.Length > 0) return line.Contains(needle, StringComparison.OrdinalIgnoreCase);
                i = nl;
                if (nl < 0) break;
            }
        }
        catch { }
        return false;
    }
}
