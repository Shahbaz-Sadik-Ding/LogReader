using System;

namespace LogReader.Models;

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4,
    Fatal = 5,
    Unknown = 99
}

public static class LogLevels
{
    public static LogLevel Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return LogLevel.Unknown;
        switch (text.Trim().ToUpperInvariant())
        {
            case "TRACE": case "VERBOSE": return LogLevel.Trace;
            case "DEBUG": return LogLevel.Debug;
            case "INFO": case "INFORMATION": return LogLevel.Info;
            case "WARN": case "WARNING": return LogLevel.Warn;
            case "ERROR": return LogLevel.Error;
            case "FATAL": case "CRITICAL": return LogLevel.Fatal;
            default: return LogLevel.Unknown;
        }
    }
}
