using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using LogReader.Parsing;

namespace LogReader.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    public ObservableCollection<LogDocumentViewModel> Documents { get; } = new();

    public RelayCommand OpenCommand { get; }
    public RelayCommand CloseCommand { get; }
    public RelayCommand ReloadCommand { get; }

    public LogFormat[] Formats { get; } = { LogFormat.Auto, LogFormat.PlainText, LogFormat.Xml };

    public MainViewModel()
    {
        OpenCommand = new RelayCommand(_ => OpenFiles());
        CloseCommand = new RelayCommand(p => Close(p as LogDocumentViewModel), p => p is LogDocumentViewModel);
        ReloadCommand = new RelayCommand(_ => Active?.Reload(), _ => Active != null);
    }

    private LogFormat _selectedFormat = LogFormat.Auto;
    public LogFormat SelectedFormat
    {
        get => _selectedFormat;
        set => SetField(ref _selectedFormat, value);
    }

    private string _customPattern = PlainTextLogParser.DefaultPattern;
    public string CustomPattern
    {
        get => _customPattern;
        set => SetField(ref _customPattern, value);
    }

    private LogDocumentViewModel? _active;
    public LogDocumentViewModel? Active
    {
        get => _active;
        set => SetField(ref _active, value);
    }

    public void OpenFiles()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open log file(s)",
            Multiselect = true,
            Filter = "Log files (*.log;*.txt;*.xml)|*.log;*.txt;*.xml|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
            foreach (var path in dlg.FileNames) OpenFile(path);
    }

    public void OpenFile(string path)
    {
        if (!File.Exists(path)) return;
        var doc = new LogDocumentViewModel(path, SelectedFormat, CustomPattern);
        Documents.Add(doc);
        Active = doc;
    }

    private void Close(LogDocumentViewModel? doc)
    {
        if (doc == null) return;
        doc.Dispose();
        Documents.Remove(doc);
        if (Active == doc) Active = Documents.Count > 0 ? Documents[^1] : null;
    }

    /// <summary>Closes every open tab.</summary>
    public void CloseAll()
    {
        foreach (var d in Documents) d.Dispose();
        Documents.Clear();
        Active = null;
    }

    /// <summary>Closes every tab except <paramref name="keep"/>.</summary>
    public void CloseOthers(LogDocumentViewModel? keep)
    {
        if (keep == null) return;
        foreach (var d in Documents.Where(x => x != keep).ToList())
        {
            d.Dispose();
            Documents.Remove(d);
        }
        Active = keep;
    }

    /// <summary>Toggles a tab's pinned state and moves pinned tabs to the left.</summary>
    public void TogglePin(LogDocumentViewModel? doc)
    {
        if (doc == null) return;
        doc.IsPinned = !doc.IsPinned;
        ReorderPinned();
    }

    /// <summary>
    /// Stable reorder so pinned tabs sit at the left, each group keeping its
    /// existing order. Uses Move so the active tab/selection is preserved.
    /// </summary>
    public void ReorderPinned()
    {
        var desired = Documents.OrderByDescending(d => d.IsPinned).ToList();
        for (int i = 0; i < desired.Count; i++)
        {
            int cur = Documents.IndexOf(desired[i]);
            if (cur != i) Documents.Move(cur, i);
        }
    }
}
