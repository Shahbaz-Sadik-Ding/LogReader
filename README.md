# LogReader

A modern WPF desktop viewer for **log4net** log files (.NET 8). Open plain-text
`PatternLayout` logs or `XmlLayoutSchemaLog4j` XML logs, then search, filter and
live-tail them with a clean dark UI.

## Features

- **Two formats, auto-detected** — plain-text log4net (`PatternLayout`) and log4j
  XML. Override detection from the toolbar (`Auto` / `Plain text` / `XML`).
- **Quick search** — substring or **regex**, with optional case sensitivity.
  Searches the full raw record, including folded stack traces.
- **Level filters** — toggle TRACE / DEBUG / INFO / WARN / ERROR / FATAL pills.
- **Live tail** — watches the file and appends new entries as your app writes
  them. Survives log rollover/truncation.
- **Multi-file tabs** — open several logs at once; drag & drop files onto the
  window. Close with the ✕ or `Ctrl+W`.
- **Stack-trace folding** — continuation lines are attached to their parent
  entry and shown in the detail pane.
- **Colour-coded levels** and a virtualised grid that stays responsive on large
  files.

## Requirements

- Windows
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build & run

```powershell
cd LogReader
dotnet restore
dotnet run
```

## Build a shareable EXE

Run the helper script from the project folder:

```powershell
./publish.ps1
```

It produces a single self-contained `dist\LogReader.exe` (~150 MB) and opens
the folder. Send that one file to colleagues — they double-click it to run on
any Windows 10/11 PC. **No .NET install and no admin rights required**, because
the runtime is bundled inside the exe.

(Equivalent manual command:)

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -o dist
```

## Put it on GitHub

One-time setup (from the project folder):

```powershell
git init
git add .
git commit -m "Initial commit: LogReader log4net viewer"
git branch -M main
git remote add origin https://github.com/<you>/LogReader.git
git push -u origin main
```

You can create the empty `LogReader` repo on github.com first, or with the
GitHub CLI: `gh repo create LogReader --private --source . --push`.

## Release the EXE to colleagues (automated)

A GitHub Actions workflow (`.github/workflows/release.yml`) builds the EXE in
the cloud and publishes it to the repo's **Releases** page whenever you push a
version tag:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

A minute later, the EXE appears at
`https://github.com/<you>/LogReader/releases` — colleagues just download
`LogReader.exe` from there and run it. Bump the tag (`v1.0.1`, `v1.1.0`, …) for
each new version.

> First-time note: the workflow needs Actions enabled (default for new repos).
> No secrets to configure — it uses the built-in `GITHUB_TOKEN`.

### What colleagues do

1. Open the repo's **Releases** page (or you send them `LogReader.exe`).
2. Download `LogReader.exe`.
3. Double-click it. Windows SmartScreen may show "Windows protected your PC"
   for an unsigned app — click **More info → Run anyway** (one-time per file).
4. Open a log via the toolbar or drag a `.log` file onto the window.

## Try it

Sample logs are in `samples/`:

- `sample-app.log` — plain-text log4net output (with a multi-line exception)
- `sample-app-log4j.xml` — log4j XML output

Open either from the toolbar or drag it onto the window.

## Configuring the parse pattern

The default plain-text regex matches the layout below, where the **correlation
id** and **user/identity** brackets are optional:

```
%date [%thread] %-5level [%correlationId] [%user] %logger - %message%newline
2026-05-19 12:49:51,299 [12] DEBUG [98KDKDoPGXM6lPfFKJSzBV] [user@ding.com] Common.Web.UI... - message
2026-06-17 09:14:02,131 [15] ERROR Ding.Trading.Settlement - Settlement failed   (no brackets — also matches)
```

If your log4net `conversionPattern` differs, edit the regex in
`Parsing/PlainTextLogParser.cs` (`DefaultPattern`). It uses .NET named groups:
`timestamp`, `thread`, `level`, `correlationId`, `user`, `logger`, `message`.
Any line that doesn't match is treated as a continuation of the previous entry
(so multi-line messages and stack traces stay intact).

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
├─ ViewModels/     MainViewModel, LogDocumentViewModel, RelayCommand
├─ Converters/     level→brush, null/string→visibility
├─ Themes/         DarkTheme.xaml
├─ Views/          MainWindow
└─ samples/        example logs
```

## Keyboard shortcuts

| Shortcut | Action            |
|----------|-------------------|
| `Ctrl+O` | Open log file(s)  |
| `Ctrl+W` | Close active tab  |
