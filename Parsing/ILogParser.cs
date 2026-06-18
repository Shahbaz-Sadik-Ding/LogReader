using System.Collections.Generic;
using LogReader.Models;

namespace LogReader.Parsing;

/// <summary>
/// Stateful, streaming log parser. Text is pushed in chunks (the whole file on
/// first load, then only appended bytes during live tail). Implementations buffer
/// partial lines/events internally so tailing works without re-reading the file.
/// </summary>
public interface ILogParser
{
    string Name { get; }

    /// <summary>Parse whatever complete records are available in the chunk.</summary>
    IEnumerable<LogEntry> Feed(string chunk);

    /// <summary>Emit any record still buffered (call when no more data follows).</summary>
    IEnumerable<LogEntry> Flush();

    /// <summary>Clear all internal state (used when reloading a file).</summary>
    void Reset();
}
