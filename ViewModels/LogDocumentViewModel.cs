using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using LogReader.Models;
using LogReader.Parsing;
using LogReader.Services;

namespace LogReader.ViewModels;

/// <summary>One open log file = one tab. Owns parsing, filtering and live tail.</summary>
public sealed class LogDocumentViewModel : ObservableObject, IDisposable
{
    private readonly TailReader _reader;
    private readonly ILogParser _parser;
    private readonly DispatcherTimer _timer;
    private Regex? _searchRegex;

    public string FilePath { get; }
    public string Title { get; }
    public string ParserName => _parser.Name;

    public RangeObservableCollection<LogEntry> Entries { get; } = new();
    public ICollectionView View { get; }

    // Cached so the status bar never has to re-scan all rows.
    private int _errorCount;

    public LogDocumentViewModel(string filePath, LogFormat format, string? customPattern)
    {
        FilePath = filePath;
        Title = Path.GetFileName(filePath);

        string sample = ReadSample(filePath);
        _parser = LogParserFactory.Create(format, sample, customPattern);
        _reader = new TailReader(filePath);

        View = CollectionViewSource.GetDefaultView(Entries);
        View.Filter = FilterPredicate;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _timer.Tick += (_, _) => PollNew();

        _ = LoadInitialAsync();
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    // ---- Filtering state -------------------------------------------------

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { if (SetField(ref _searchText, value)) RebuildSearch(); }
    }

    private bool _useRegex;
    public bool UseRegex
    {
        get => _useRegex;
        set { if (SetField(ref _useRegex, value)) RebuildSearch(); }
    }

    private bool _matchCase;
    public bool MatchCase
    {
        get => _matchCase;
        set { if (SetField(ref _matchCase, value)) RebuildSearch(); }
    }

    private bool _showTrace = true, _showDebug = true, _showInfo = true,
                 _showWarn = true, _showError = true, _showFatal = true;

    public bool ShowTrace { get => _showTrace; set { if (SetField(ref _showTrace, value)) Refresh(); } }
    public bool ShowDebug { get => _showDebug; set { if (SetField(ref _showDebug, value)) Refresh(); } }
    public bool ShowInfo  { get => _showInfo;  set { if (SetField(ref _showInfo,  value)) Refresh(); } }
    public bool ShowWarn  { get => _showWarn;  set { if (SetField(ref _showWarn,  value)) Refresh(); } }
    public bool ShowError { get => _showError; set { if (SetField(ref _showError, value)) Refresh(); } }
    public bool ShowFatal { get => _showFatal; set { if (SetField(ref _showFatal, value)) Refresh(); } }

    // ---- Per-column "contains" filters ----------------------------------

    private string _filterTime = "", _filterThread = "", _filterCorrelationId = "",
                   _filterLogger = "", _filterMessage = "";

    public string FilterTime          { get => _filterTime;          set { if (SetField(ref _filterTime, value)) Refresh(); } }
    public string FilterThread        { get => _filterThread;        set { if (SetField(ref _filterThread, value)) Refresh(); } }
    public string FilterCorrelationId { get => _filterCorrelationId; set { if (SetField(ref _filterCorrelationId, value)) Refresh(); } }
    public string FilterLogger        { get => _filterLogger;        set { if (SetField(ref _filterLogger, value)) Refresh(); } }
    public string FilterMessage       { get => _filterMessage;       set { if (SetField(ref _filterMessage, value)) Refresh(); } }

    private LogEntry? _selected;
    public LogEntry? SelectedEntry
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    // ---- Live tail -------------------------------------------------------

    private bool _liveTail;
    public bool LiveTail
    {
        get => _liveTail;
        set
        {
            if (!SetField(ref _liveTail, value)) return;
            if (value) { PollNew(); _timer.Start(); }
            else _timer.Stop();
        }
    }

    // ---- Stats -----------------------------------------------------------

    public int TotalCount => Entries.Count;

    // ListCollectionView keeps its filtered Count up to date (O(1)); avoid
    // enumerating the whole view on every keystroke.
    public int VisibleCount => (View as ListCollectionView)?.Count ?? Entries.Count;

    public int ErrorCount => _errorCount;

    public string StatusText =>
        $"{VisibleCount:N0} shown / {TotalCount:N0} total · {ErrorCount:N0} error(s)";

    // ---- Loading ---------------------------------------------------------

    /// <summary>
    /// Reads and parses the whole file on a background thread (so the UI stays
    /// responsive and can show the loading overlay), then adds the rows on the
    /// UI thread.
    /// </summary>
    private async Task LoadInitialAsync()
    {
        IsLoading = true;
        try
        {
            var parsed = await Task.Run(() =>
            {
                var text = _reader.ReadNew();
                var list = new List<LogEntry>(_parser.Feed(text));
                list.AddRange(_parser.Flush());
                return list;
            });

            // Single bulk add => the view refilters once (one Reset) instead of
            // once per row.
            _errorCount += parsed.Count(e => e.LevelValue is LogLevel.Error or LogLevel.Fatal);
            Entries.AddRange(parsed);
            NotifyStats();
        }
        catch (IOException) { /* file vanished or locked; nothing to show */ }
        finally
        {
            IsLoading = false;
        }
    }

    private void PollNew()
    {
        try
        {
            var text = _reader.ReadNew();
            if (text.Length > 0) AppendParsed(text, flush: false);
        }
        catch (IOException) { /* file briefly locked; try again next tick */ }
    }

    private void AppendParsed(string text, bool flush)
    {
        if (text.Length > 0)
            foreach (var e in _parser.Feed(text)) Add(e);
        if (flush)
            foreach (var e in _parser.Flush()) Add(e);
        NotifyStats();

        void Add(LogEntry e)
        {
            if (e.LevelValue is LogLevel.Error or LogLevel.Fatal) _errorCount++;
            Entries.Add(e);
        }
    }

    public void Reload()
    {
        _parser.Reset();
        _reader.Reset();
        _errorCount = 0;
        Entries.Clear();
        _ = LoadInitialAsync();
    }

    // ---- Filter implementation ------------------------------------------

    private void RebuildSearch()
    {
        _searchRegex = null;
        if (!string.IsNullOrEmpty(_searchText) && _useRegex)
        {
            try
            {
                // No RegexOptions.Compiled here: compiling on every keystroke costs
                // more than it saves for short-lived interactive search.
                var opts = _matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                _searchRegex = new Regex(_searchText, opts);
            }
            catch (ArgumentException) { /* incomplete regex while typing — ignore */ }
        }
        Refresh();
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not LogEntry e) return false;

        // Level (multi-select) + per-column "contains" filters.
        if (!LevelVisible(e.LevelValue)) return false;
        if (!Contains(e.TimestampText, _filterTime)) return false;
        if (!Contains(e.Thread, _filterThread)) return false;
        if (!Contains(e.CorrelationId, _filterCorrelationId)) return false;
        if (!Contains(e.Logger, _filterLogger)) return false;
        if (!Contains(e.Message, _filterMessage)) return false;

        // Global quick-search across the whole raw line.
        if (string.IsNullOrEmpty(_searchText)) return true;
        if (_useRegex)
            return _searchRegex == null || _searchRegex.IsMatch(e.Raw);

        var cmp = _matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return e.Raw.IndexOf(_searchText, cmp) >= 0;
    }

    private static bool Contains(string field, string filter) =>
        string.IsNullOrEmpty(filter) ||
        field.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

    private bool LevelVisible(LogLevel level) => level switch
    {
        LogLevel.Trace => _showTrace,
        LogLevel.Debug => _showDebug,
        LogLevel.Info => _showInfo,
        LogLevel.Warn => _showWarn,
        LogLevel.Error => _showError,
        LogLevel.Fatal => _showFatal,
        _ => _showInfo // Unknown rides along with Info
    };

    private void Refresh()
    {
        View.Refresh();
        NotifyStats();
    }

    private void NotifyStats()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(StatusText));
    }

    private static string ReadSample(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(fs);
            var buffer = new char[4000];
            int read = reader.Read(buffer, 0, buffer.Length);
            return new string(buffer, 0, read);
        }
        catch { return string.Empty; }
    }

    public void Dispose() => _timer.Stop();
}
