using System;
using System.IO;
using System.Text;

namespace LogReader.Services;

/// <summary>
/// Reads a log file incrementally using a shared read handle so it works even
/// while another process is writing to it. Tracks the byte offset so each call
/// to <see cref="ReadNew"/> returns only the text appended since the last read.
/// Handles file truncation/rollover by resetting to the start.
/// </summary>
public sealed class TailReader
{
    private long _position;
    private readonly Encoding _encoding;

    public string Path { get; }

    public TailReader(string path, Encoding? encoding = null)
    {
        Path = path;
        _encoding = encoding ?? Encoding.UTF8;
    }

    /// <summary>Reads everything from the current offset to the end of the file.</summary>
    public string ReadNew()
    {
        if (!File.Exists(Path)) return string.Empty;

        using var fs = new FileStream(Path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        // File got shorter than where we were — assume rollover/truncate, start over.
        if (fs.Length < _position) _position = 0;
        if (fs.Length == _position) return string.Empty;

        fs.Seek(_position, SeekOrigin.Begin);
        using var reader = new StreamReader(fs, _encoding, detectEncodingFromByteOrderMarks: _position == 0);
        var text = reader.ReadToEnd();
        _position = fs.Length;
        return text;
    }

    public void Reset() => _position = 0;
}
