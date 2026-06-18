# LogReader

**A fast, modern log4net log viewer for Windows.** LogReader is a free, open-source
desktop app for reading [log4net](https://logging.apache.org/log4net/) log files —
both plain-text `PatternLayout` logs and `XmlLayoutSchemaLog4j` XML logs. It opens
large files quickly, follows them live as they grow, and gives you fast search,
per-column filtering, and search highlighting in a clean dark UI.

Built with WPF on .NET 8. Contributions and issues are welcome.

## Features

- **Two formats, auto-detected** — plain-text log4net (`PatternLayout`) and log4j
  XML; the format is detected automatically from the file content.
- **Quick search** — substring or **regex**, with optional case sensitivity, across
  the full record (including folded stack traces), with matches highlighted in the rows.
- **Per-column filters** — a filter box under each column header, plus a Level
  dropdown with TRACE / DEBUG / INFO / WARN / ERROR / FATAL (and Select all / Clear all).
- **Live tail** — watches the file and appends new entries as your app writes them,
  auto-scrolling to the newest line. Survives log rollover/truncation.
- **Multi-file tabs** — open several logs at once (or drag & drop them); tabs stay on
  one row and shrink to fit.
- **Stack-trace folding** — continuation lines fold into their parent entry and show in
  the detail pane; rows with an exception are colour-flagged.
- **Copy** — right-click a row to copy any field, `Ctrl+C` for the whole row, or select
  text in the detail pane.
- **Fast on big files** — virtualised, bulk-loaded grid that stays responsive on
  40k+ line logs.

## Download

Grab the latest **`LogReader.exe`** from the [Releases page](../../releases). It's a
single self-contained executable — **no .NET install and no admin rights required**,
because the runtime is bundled in. Double-click to run on any Windows 10/11 PC.

> The app is unsigned, so Windows SmartScreen may show "Windows protected your PC" on
> first launch — click **More info → Run anyway**.

## Build from source

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

```powershell
git clone https://github.com/<you>/LogReader.git
cd LogReader
dotnet run
```

To produce the single self-contained `dist\LogReader.exe`:

```powershell
./publish.ps1
```

(Equivalent manual command:)

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -o dist
```

## Usage

Open a log from **Open log(s)…**, by dragging files onto the window, or by passing
paths on the command line:

```powershell
LogReader.exe "C:\Logs\app.log" "C:\Logs\other.log"
```

Sample logs to try are in `samples/` (`sample-app.log`, `sample-app-log4j.xml`,
`sample-titanadmin.log`).

### PowerShell helpers

`LogReader.Profile.ps1` provides `watch` / `logreader` functions. Dot-source it from
your `$PROFILE`:

```powershell
. "C:\Source\LogReader\LogReader.Profile.ps1"
watch MyProduct   # opens every .log in C:\Logs\MyProduct\MyProduct
```

Set `$LogReaderExe` in that file to wherever your `LogReader.exe` lives.

### Keyboard shortcuts

| Shortcut | Action            |
|----------|-------------------|
| `Ctrl+O` | Open log file(s)  |
| `Ctrl+W` | Close active tab  |

## Configuring the parse pattern

The default plain-text regex matches the layout below, where the **correlation id**
and **user/identity** brackets are optional:

```
%date [%thread] %-5level [%correlationId] [%user] %logger - %message%newline
2026-05-19 12:49:51,299 [12] DEBUG [98KDKDoPGXM6lPfFKJSzBV] [user@example.com] Common.Web.UI... - message
2026-06-17 09:14:02,131 [15] ERROR My.Logger - Something failed   (no brackets — also matches)
```

If your log4net `conversionPattern` differs, edit the regex in
`Parsing/PlainTextLogParser.cs` (`DefaultPattern`). It uses .NET named groups:
`timestamp`, `thread`, `level`, `correlationId`, `user`, `logger`, `message`. Any line
that doesn't match is treated as a continuation of the previous entry (so multi-line
messages and stack traces stay intact).

To emit XML that this app reads, configure log4net with:

```xml
<layout type="log4net.Layout.XmlLayoutSchemaLog4j" />
```

## Project layout

```
LogReader/
├─ Models/         LogEntry, LogLevel
├─ Parsing/        ILogParser, PlainTextLogParser, XmlLogParser, factory
├─ Services/       TailReader (incremental/shared-handle file reads)
├─ ViewModels/     MainViewModel, LogDocumentViewModel, RangeObservableCollection
├─ Behaviors/      AutoScrollToEnd, Highlighter
├─ Converters/     level→brush, null/string→visibility
├─ Themes/         DarkTheme.xaml
├─ Views/          MainWindow
└─ samples/        example logs
```

## Contributing

Issues and pull requests are welcome. Build from source as above (`dotnet run`), and
please keep changes focused. For UI work, the theme lives in `Themes/DarkTheme.xaml`
and the log model/parsing is under `Models/` and `Parsing/`.

## License

Open source under the MIT License — add a `LICENSE` file with your details.

---

# Release notes

## v1.2.0

**Parsing**
- Recognises the extended log4net layout with **correlation id** and **user/identity**
  fields: `%date [%thread] %-5level [%correlationId] [%user] %logger - %message`
  (both bracketed fields optional, so simpler layouts still parse).
- Padded thread ids (e.g. `[ 1]`) are trimmed; `[correlation id missing]` and empty
  `[]` are handled.

**Grid & columns**
- New **Thread** and **Correlation ID** columns.
- **Per-column filters** — a filter box under every column header that filters as you
  type and resizes with its column.
- **Level filter dropdown** in the Level header with **Select all / Clear all**.
- **Search highlighting** — matches from the global search and column filters are
  highlighted in glowing orange inside the rows (Level excluded).
- Rows carrying an **exception / stack trace** show their message text in a distinct colour.
- Selected rows stay clearly highlighted even while hovered.
- Simple left-right **horizontal scrolling** with no auto-jump on click/arrow-keys;
  smooth pixel-based vertical scrolling.

**Copy**
- Right-click a row to **Copy** message / correlation id / logger / timestamp / whole row;
  `Ctrl+C` copies the selected row.
- Detail/preview pane text is selectable for copying arbitrary substrings.

**UI / theme**
- Green app icon and a **dark title bar**.
- Single-row tabs that shrink to share the width (no more wrapping), with rounded
  corners and a selected-tab accent.
- Light-green, slim scrollbars; dark, rounded context menus.
- **Live tail** — glowing button while active and auto-scroll to the newest line.
- A loading screen (app logo + "Uploading file…") while large files open.

**Launching**
- Accepts **file paths as command-line arguments**, enabling the `watch` / `logreader`
  PowerShell helpers (`LogReader.Profile.ps1`).

**Performance**
- Bulk-loads parsed rows in one operation (single view refresh) instead of one
  notification per row — much faster for large files.
- O(1) visible/error counts in the status bar (no full re-scan per keystroke); lighter
  interactive search; removed the per-row fade that hurt scrolling.

## v1.0.0

Initial release.

- View **log4net** logs in two formats, **auto-detected**: plain-text `PatternLayout`
  and `XmlLayoutSchemaLog4j` XML.
- **Quick search** — substring or regex, with optional case sensitivity, across the full
  record including folded stack traces.
- **Level filters** for TRACE / DEBUG / INFO / WARN / ERROR / FATAL.
- **Live tail** that appends new lines as the file grows and survives rollover/truncation.
- **Multi-file tabs**, with drag & drop and `Ctrl+W` to close.
- **Stack-trace folding** — continuation lines attach to their parent entry and show in
  the detail pane.
- Colour-coded levels and a virtualised grid for large files, in a dark UI.
- Single self-contained Windows executable.
