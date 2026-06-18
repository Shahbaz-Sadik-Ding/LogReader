using System;

namespace LogReader.Models;

/// <summary>
/// A single parsed log record. Raw is always preserved so nothing is lost
/// even when a line does not match the configured pattern.
/// </summary>
public sealed class LogEntry
{
    public int LineNumber { get; init; }
    public DateTime? Timestamp { get; init; }
    public string Level { get; init; } = "INFO";
    public string Logger { get; init; } = string.Empty;
    public string Thread { get; init; } = string.Empty;

    /// <summary>log4net correlation id (e.g. %property{CorrelationId}).</summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>User / identity bracket (e.g. %identity), often empty.</summary>
    public string User { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    /// <summary>Stack trace / exception block or any continuation lines.</summary>
    public string? Exception { get; init; }

    /// <summary>True when this entry carries an exception / stack-trace block.</summary>
    public bool HasException => !string.IsNullOrWhiteSpace(Exception);

    /// <summary>The original unparsed text for this record.</summary>
    public string Raw { get; init; } = string.Empty;

    public string TimestampText =>
        Timestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? string.Empty;

    /// <summary>Normalised level used for filtering and coloring.</summary>
    public LogLevel LevelValue => LogLevels.Parse(Level);
}
