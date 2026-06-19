using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LogReader.Services;

/// <summary>The set of open files (and which one was active) from the last run.</summary>
public sealed class SessionState
{
    public List<string> Files { get; set; } = new();
    public List<string> Pinned { get; set; } = new();
    public string? Active { get; set; }
    public bool DarkTheme { get; set; } = true;
}

/// <summary>
/// Persists the open-tabs session to %AppData%\LogReader\session.json so the
/// previously open files reopen on next launch. Best-effort — never throws.
/// </summary>
public static class SessionStore
{
    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LogReader");

    private static string FilePath => Path.Combine(Dir, "session.json");

    public static void Save(SessionState state)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(state));
        }
        catch { /* best-effort; a failed save just means no restore next time */ }
    }

    public static SessionState Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<SessionState>(File.ReadAllText(FilePath)) ?? new SessionState();
        }
        catch { /* corrupt/missing -> start clean */ }
        return new SessionState();
    }
}
