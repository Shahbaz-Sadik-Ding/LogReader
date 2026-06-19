using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using LogReader.Models;

namespace LogReader.Parsing;

/// <summary>
/// Parses log4net XmlLayoutSchemaLog4j output. The file is a stream of
/// &lt;log4j:event&gt; elements with no document root, so we buffer text and
/// pull out complete event blocks as they arrive (supports live tail).
/// </summary>
public sealed class XmlLogParser : ILogParser
{
    private static readonly XNamespace Log4J = "http://jakarta.apache.org/log4j/";
    private const string OpenTag = "<log4j:event";
    private const string CloseTag = "</log4j:event>";
    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly StringBuilder _buffer = new();
    private int _counter;

    public string Name => "XML (log4j schema)";

    public void Reset()
    {
        _buffer.Clear();
        _counter = 0;
    }

    public IEnumerable<LogEntry> Feed(string chunk)
    {
        _buffer.Append(chunk);
        var results = new List<LogEntry>();

        // Materialise the buffer once, scan all complete events with a moving
        // offset, then drop the consumed prefix in a single Remove (avoids the
        // O(n^2) cost of ToString()-ing the whole buffer for every event).
        var text = _buffer.ToString();
        int consumed = 0;
        while (true)
        {
            int open = text.IndexOf(OpenTag, consumed, StringComparison.Ordinal);
            if (open < 0) { consumed = text.Length; break; }        // no (more) events

            int close = text.IndexOf(CloseTag, open, StringComparison.Ordinal);
            if (close < 0) { consumed = open; break; }              // wait for the rest of this event

            int eventEnd = close + CloseTag.Length;
            var entry = ParseEvent(text.Substring(open, eventEnd - open));
            if (entry != null) results.Add(entry);
            consumed = eventEnd;
        }

        if (consumed > 0) _buffer.Remove(0, consumed);
        return results;
    }

    public IEnumerable<LogEntry> Flush() => Array.Empty<LogEntry>();

    private LogEntry? ParseEvent(string block)
    {
        try
        {
            // Wrap so the log4j: prefix resolves to a real namespace.
            var wrapped = $"<root xmlns:log4j=\"{Log4J}\">{block}</root>";
            var root = XElement.Parse(wrapped);
            var ev = root.Element(Log4J + "event");
            if (ev == null) return null;

            _counter++;

            DateTime? ts = null;
            var tsAttr = ev.Attribute("timestamp")?.Value;
            if (long.TryParse(tsAttr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
                ts = Epoch.AddMilliseconds(ms).ToLocalTime();

            var message = ev.Element(Log4J + "message")?.Value ?? string.Empty;
            var throwable = ev.Element(Log4J + "throwable")?.Value;

            // Correlation id / user are emitted as <log4j:data name=".." value=".."/>.
            string correlationId = string.Empty, user = string.Empty;
            var props = ev.Element(Log4J + "properties");
            if (props != null)
            {
                foreach (var data in props.Elements(Log4J + "data"))
                {
                    var name = data.Attribute("name")?.Value ?? string.Empty;
                    var val = data.Attribute("value")?.Value ?? string.Empty;
                    if (name.IndexOf("correlation", StringComparison.OrdinalIgnoreCase) >= 0)
                        correlationId = val;
                    else if (name.IndexOf("user", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             name.IndexOf("identity", StringComparison.OrdinalIgnoreCase) >= 0)
                        user = val;
                }
            }

            return new LogEntry
            {
                LineNumber = _counter,
                Timestamp = ts,
                Level = ev.Attribute("level")?.Value ?? "INFO",
                Logger = ev.Attribute("logger")?.Value ?? string.Empty,
                Thread = ev.Attribute("thread")?.Value ?? string.Empty,
                CorrelationId = correlationId,
                User = user,
                Message = message,
                Exception = string.IsNullOrWhiteSpace(throwable) ? null : throwable,
                Raw = block
            };
        }
        catch
        {
            return null; // malformed event block — skip rather than crash
        }
    }
}
