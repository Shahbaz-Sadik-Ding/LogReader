using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LogReader.Models;
using LogReader.Services;
using LogReader.ViewModels;

namespace LogReader.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // Ctrl+O = open, Ctrl+W = close active tab
        InputBindings.Add(new KeyBinding(_vm.OpenCommand, Key.O, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(
            new RelayCommand(() => { if (_vm.Active != null) _vm.CloseCommand.Execute(_vm.Active); }),
            Key.W, ModifierKeys.Control));

        SourceInitialized += OnSourceInitialized;

        // Keep the horizontal scrollbar usable for manual left-right panning, but
        // stop the grid from auto-panning sideways to a clicked/navigated cell.
        // Cancelling the cell's "bring into view" prevents the sideways jump
        // without affecting row selection or the preview pane.
        EventManager.RegisterClassHandler(typeof(DataGridCell),
            RequestBringIntoViewEvent,
            new RequestBringIntoViewEventHandler((_, e) => e.Handled = true));

        // Restore the saved theme (palette only; the title bar is set once we have a handle).
        _darkTheme = SessionStore.Load().DarkTheme;
        ApplyPalette(_darkTheme);
        ThemeToggle.IsChecked = _darkTheme;   // checked = dark
        ThemeLabel.Text = _darkTheme ? "Dark" : "Light";
    }

    // Segoe MDL2 Assets: moon (QuietHours) for dark, sun (Brightness) for light.
    private static string ThemeGlyph(bool dark) => dark ? "" : "";

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        EnableDarkTitleBar(hwnd, _darkTheme);
        AllowDragDropWhenElevated(hwnd);
    }

    // -------- Light / dark theme --------

    private bool _darkTheme = true;

    private void ThemeToggle_Click(object sender, RoutedEventArgs e) =>
        ApplyTheme(ThemeToggle.IsChecked == true);

    private void ApplyTheme(bool dark)
    {
        _darkTheme = dark;
        ApplyPalette(dark);
        ThemeLabel.Text = dark ? "Dark" : "Light";
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero) EnableDarkTitleBar(hwnd, dark);
    }

    // Swaps the active colour palette (MergedDictionaries[0]); DynamicResource
    // references throughout the app update live.
    private static void ApplyPalette(bool dark)
    {
        var uri = new Uri($"Themes/Palette.{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative);
        Application.Current.Resources.MergedDictionaries[0] =
            new ResourceDictionary { Source = uri };
    }

    /// <summary>Opens one or more log files (used for command-line arguments).</summary>
    public void OpenFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
            _vm.OpenFile(path);
    }

    // Right-click "Pin tab": IsPinned was just toggled by the menu's binding;
    // reorder so pinned tabs move to the left.
    private void PinTab_Click(object sender, RoutedEventArgs e) => _vm.ReorderPinned();

    private static LogDocumentViewModel? DocOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as LogDocumentViewModel;

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (DocOf(sender) is { } doc) _vm.CloseCommand.Execute(doc);
    }

    private void CloseOtherTabs_Click(object sender, RoutedEventArgs e) => _vm.CloseOthers(DocOf(sender));

    private void CloseAllTabs_Click(object sender, RoutedEventArgs e) => _vm.CloseAll();

    /// <summary>Reopens the files that were open when the app last closed.</summary>
    public void RestoreSession()
    {
        var state = SessionStore.Load();
        foreach (var path in state.Files.Where(File.Exists))
            _vm.OpenFile(path);

        foreach (var doc in _vm.Documents)
            if (state.Pinned.Contains(doc.FilePath)) doc.IsPinned = true;
        _vm.ReorderPinned();

        if (state.Active != null)
        {
            var match = _vm.Documents.FirstOrDefault(d => d.FilePath == state.Active);
            if (match != null) _vm.Active = match;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        SessionStore.Save(new SessionState
        {
            Files = _vm.Documents.Select(d => d.FilePath).ToList(),
            Pinned = _vm.Documents.Where(d => d.IsPinned).Select(d => d.FilePath).ToList(),
            Active = _vm.Active?.FilePath,
            DarkTheme = _darkTheme
        });

        // Stop each document's live-tail timer on close.
        foreach (var doc in _vm.Documents) doc.Dispose();
    }

    // -------- Dark (immersive) native title bar --------

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private static void EnableDarkTitleBar(IntPtr hwnd, bool dark)
    {
        int useDark = dark ? 1 : 0;
        // Windows 11 / recent Win10 attribute, with fallback to the older value.
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int)) != 0)
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDark, sizeof(int));
    }

    // -------- Let file drops through when the app runs elevated (UIPI) --------

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeWindowMessageFilterEx(
        IntPtr hwnd, uint message, int action, IntPtr pChangeFilterStruct);

    private static void AllowDragDropWhenElevated(IntPtr hwnd)
    {
        const int MSGFLT_ALLOW = 1;
        const uint WM_DROPFILES = 0x0233;
        const uint WM_COPYDATA = 0x004A;
        const uint WM_COPYGLOBALDATA = 0x0049;
        try
        {
            ChangeWindowMessageFilterEx(hwnd, WM_DROPFILES, MSGFLT_ALLOW, IntPtr.Zero);
            ChangeWindowMessageFilterEx(hwnd, WM_COPYDATA, MSGFLT_ALLOW, IntPtr.Zero);
            ChangeWindowMessageFilterEx(hwnd, WM_COPYGLOBALDATA, MSGFLT_ALLOW, IntPtr.Zero);
        }
        catch { /* older OS without the Ex API — non-elevated drops still work */ }
    }

    // -------- Drag & drop of log files --------

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        e.Handled = true;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        foreach (var path in files.Where(System.IO.File.Exists))
            _vm.OpenFile(path);
    }

    // -------- Level column filter dropdown --------

    private Popup? _levelPopup;

    private void LevelFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement btn || _vm.Active is not LogDocumentViewModel doc)
            return;

        if (_levelPopup != null) _levelPopup.IsOpen = false;

        var panel = new StackPanel();

        // Quick "select all / clear" row.
        void SetAll(bool v)
        {
            doc.ShowTrace = v; doc.ShowDebug = v; doc.ShowInfo = v;
            doc.ShowWarn = v; doc.ShowError = v; doc.ShowFatal = v;
        }
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        actions.Children.Add(MakeLinkButton("Select all", () => SetAll(true)));
        actions.Children.Add(MakeLinkButton("Clear all", () => SetAll(false)));
        panel.Children.Add(actions);
        panel.Children.Add(new Border
        {
            Height = 1,
            Background = (Brush)FindResource("BorderBrush"),
            Margin = new Thickness(0, 0, 0, 6)
        });

        AddLevelCheck(panel, "TRACE", nameof(LogDocumentViewModel.ShowTrace), doc);
        AddLevelCheck(panel, "DEBUG", nameof(LogDocumentViewModel.ShowDebug), doc);
        AddLevelCheck(panel, "INFO",  nameof(LogDocumentViewModel.ShowInfo),  doc);
        AddLevelCheck(panel, "WARN",  nameof(LogDocumentViewModel.ShowWarn),  doc);
        AddLevelCheck(panel, "ERROR", nameof(LogDocumentViewModel.ShowError), doc);
        AddLevelCheck(panel, "FATAL", nameof(LogDocumentViewModel.ShowFatal), doc);

        var border = new Border
        {
            Background = (Brush)FindResource("PanelBg2"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Child = panel
        };

        _levelPopup = new Popup
        {
            PlacementTarget = btn,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            Child = border,
            IsOpen = true
        };
    }

    private Button MakeLinkButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            Style = (Style)FindResource("LinkButton"),
            Margin = new Thickness(0, 0, 14, 0)
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private void AddLevelCheck(Panel panel, string text, string property, object source)
    {
        var cb = new CheckBox
        {
            Content = text,
            Style = (Style)FindResource("ModernCheckBox"),
            Margin = new Thickness(0, 3, 0, 3)
        };
        cb.SetBinding(ToggleButton.IsCheckedProperty,
            new Binding(property) { Source = source, Mode = BindingMode.TwoWay });
        panel.Children.Add(cb);
    }

    // -------- Right-click "Copy …" on a grid row --------

    // Right-click selects the row under the cursor before the menu opens.
    private void LogGrid_RightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var node = e.OriginalSource as DependencyObject;
        while (node != null && node is not DataGridRow)
            node = GetParentSafe(node);
        if (node is DataGridRow row)
            row.IsSelected = true;
    }

    // Walks up the tree tolerating text elements (Run/Span) inside highlighted
    // cells, which are ContentElements — not Visuals — so VisualTreeHelper alone
    // would throw on them.
    private static DependencyObject? GetParentSafe(DependencyObject node) => node switch
    {
        Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(node),
        FrameworkContentElement fce => fce.Parent,
        _ => LogicalTreeHelper.GetParent(node)
    };

    private LogEntry? CurrentEntry => _vm.Active?.SelectedEntry;

    private static void SetClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        // Retry: the clipboard is a shared OS resource and can be briefly locked
        // by another process, which would otherwise throw.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try { Clipboard.SetDataObject(text, true); return; }
            catch { System.Threading.Thread.Sleep(40); }
        }
    }

    private void CopyMessage_Click(object sender, RoutedEventArgs e) => SetClipboard(CurrentEntry?.Message);
    private void CopyCorrelationId_Click(object sender, RoutedEventArgs e) => SetClipboard(CurrentEntry?.CorrelationId);
    private void CopyLogger_Click(object sender, RoutedEventArgs e) => SetClipboard(CurrentEntry?.Logger);
    private void CopyTimestamp_Click(object sender, RoutedEventArgs e) => SetClipboard(CurrentEntry?.TimestampText);
    private void CopyRow_Click(object sender, RoutedEventArgs e) => SetClipboard(CurrentEntry?.Raw);
}
