using System;

namespace LogReader.Parsing;

public static class LogParserFactory
{
    /// <summary>
    /// Builds a parser for the requested format. When <see cref="LogFormat.Auto"/>
    /// is used, a sample of the file content decides between XML and plain text.
    /// </summary>
    public static ILogParser Create(LogFormat format, string? sample = null, string? customPattern = null)
    {
        var resolved = format == LogFormat.Auto ? Detect(sample) : format;
        return resolved == LogFormat.Xml
            ? new XmlLogParser()
            : new PlainTextLogParser(customPattern);
    }

    public static LogFormat Detect(string? sample)
    {
        if (string.IsNullOrWhiteSpace(sample)) return LogFormat.PlainText;
        var head = sample.Length > 4000 ? sample.Substring(0, 4000) : sample;
        return head.IndexOf("<log4j:event", StringComparison.OrdinalIgnoreCase) >= 0
            ? LogFormat.Xml
            : LogFormat.PlainText;
    }
}
