using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using LogReader.Models;

namespace LogReader.Parsing;

/// <summary>
/// Parses log4net PatternLayout (and similar) text logs using a configurable regex.
/// Lines that do not match the header regex are treated as continuation lines
/// (e.g. stack traces) and folded into the preceding entry's exception block.
/// </summary>
public sealed class PlainTextLogParser : ILogParser
{
    // Default matches the common log4net pattern, with two OPTIONAL bracketed
    // fields (correlation id and user/identity) between level and logger:
    //   %date [%thread] %-5level [%correlationId] [%user] %logger - %message
    //   2026-05-19 12:49:51,299 [12] DEBUG [98KDKDoPGXM6lPfFKJSzBV] [user@x] My.Logger - Message
    // The two brackets are optional, so simpler layouts still parse:
    //   2026-06-17 10:15:32,123 [10] INFO  My.Logger - Message text
    public const string DefaultPattern =
        @"^(?<timestamp>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}[.,]\d{1,7})\s+" +
        @"\[(?<thread>[^\]]*)\]\s+" +
        @"(?<level>TRACE|DEBUG|INFO|WARN|ERROR|FATAL|VERBOSE|WARNING|INFORMATION|CRITICAL)\s+" +
        @"(?:\[(?<correlationId>[^\]]*)\]\s+)?" +
        @"(?:\[(?<user>[^\]]*)\]\s+)?" +
        @"(?<logger>[^\s\[]\S*)\s+-\s+" +
        @"(?<message>.*)$";

    private static readonly string[] TimestampFormats =
    {
        "yyyy-MM-dd HH:mm:ss,fff",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss,fff",
        "yyyy-MM-dd HH:mm:ss",
    };

    private readonly Regex _regex;
    private readonly StringBuilder _lineBuffer = new();

    // Pending (in-progress) entry whose continuation lines may still arrive.
    private int _lineNumber;
    private DateTime? _ts;
    private string _level = "INFO";
    private string _logger = "";
    private string _thread = "";
    private string _correlationId = "";
    private string _user = "";
    private string _message = "";
    private int _startLine;
    private readonly StringBuilder _raw = new();
    private readonly StringBuilder _exception = new();
    private bool _hasPending;

    public string Name => "Plain text (PatternLayout)";

    public PlainTextLogParser(string? pattern = null)
    {
        var p = string.IsNullOrWhiteSpace(pattern) ? DefaultPattern : pattern;
        _regex = new Regex(p, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    public void Reset()
    {
        _lineBuffer.Clear();
        _lineNumber = 0;
        _hasPending = false;
        _raw.Clear();
        _exception.Clear();
    }

    public IEnumerable<LogEntry> Feed(string chunk)
    {
        _lineBuffer.Append(chunk);
        var text = _lineBuffer.ToString();
        int start = 0;
        var results = new List<LogEntry>();

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            int end = i;
            if (end > start && text[end - 1] == '\r') end--;
            ProcessLine(text.Substring(start, end - start), results);
            start = i + 1;
        }

        // Keep the trailing partial line (no newline yet) for the next chunk.
        _lineBuffer.Clear();
        if (start < text.Length) _lineBuffer.Append(text, start, text.Length - start);

        return results;
    }

    public IEnumerable<LogEntry> Flush()
    {
        var results = new List<LogEntry>();
        if (_lineBuffer.Length > 0)
        {
            ProcessLine(_lineBuffer.ToString(), results);
            _lineBuffer.Clear();
        }
        if (_hasPending)
        {
            results.Add(BuildPending());
            _hasPending = false;
        }
        return results;
    }

    private void ProcessLine(string line, List<LogEntry> results)
    {
        _lineNumber++;
        var match = _regex.Match(line);
        if (match.Success)
        {
            if (_hasPending) results.Add(BuildPending());
            BeginPending(match, line);
        }
        else
        {
            if (_hasPending)
            {
                // Continuation line: belongs to the current entry.
                if (_exception.Length > 0) _exception.Append('\n');
                _exception.Append(line);
                _raw.Append('\n').Append(line);
            }
            else if (line.Length > 0)
            {
                // Unparsable orphan line before any header — surface it rather than drop it.
                results.Add(new LogEntry
                {
                    LineNumber = _lineNumber,
                    Level = "INFO",
                    Message = line,
                    Raw = line
                });
            }
        }
    }

    private void BeginPending(Match match, string line)
    {
        _startLine = _lineNumber;
        _ts = ParseTimestamp(match.Groups["timestamp"].Value);
        _level = Value(match, "level", "INFO");
        _logger = Value(match, "logger", "");
        _thread = Value(match, "thread", "").Trim();
        _correlationId = Value(match, "correlationId", "").Trim();
        _user = Value(match, "user", "").Trim();
        _message = Value(match, "message", "");
        _raw.Clear();
        _raw.Append(line);
        _exception.Clear();
        _hasPending = true;
    }

    private LogEntry BuildPending() => new()
    {
        LineNumber = _startLine,
        Timestamp = _ts,
        Level = _level,
        Logger = _logger,
        Thread = _thread,
        CorrelationId = _correlationId,
        User = _user,
        Message = _message,
        Exception = _exception.Length > 0 ? _exception.ToString() : null,
        Raw = _raw.ToString()
    };

    private static string Value(Match m, string group, string fallback)
    {
        var g = m.Groups[group];
        return g.Success ? g.Value : fallback;
    }

    private static DateTime? ParseTimestamp(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (DateTime.TryParseExact(text, TimestampFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
            return dt;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out dt))
            return dt;
        return null;
    }
}
