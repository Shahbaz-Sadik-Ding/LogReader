# ---------------------------------------------------------------------------
# LogReader PowerShell helpers (mirrors the old LogExpert watch/logexpert funcs)
#
# To use: dot-source this from your PowerShell $PROFILE, e.g. add this line:
#     . "C:\Source\LogReader\LogReader.Profile.ps1"
# ...or just copy the functions below straight into your $PROFILE.
# ---------------------------------------------------------------------------

# Path to the LogReader executable. Adjust if you keep it somewhere else.
$LogReaderExe = "C:\Source\LogReader\dist\LogReader.exe"

function logreader {
    & $LogReaderExe $args
}

function watch($product) {
    $path = "C:\Logs\$product\$product"

    if (-not (Test-Path $path)) {
        Write-Warning "Path not found: $path"
        return
    }

    # Every .log in the folder (not just names starting with the product), so
    # Error.*, Info.* etc. are all included. Full paths, forced into an array.
    $files = @(Get-ChildItem -Path $path -Filter "*.log" -File |
               Select-Object -ExpandProperty FullName)

    if ($files.Count -eq 0) {
        Write-Warning "No .log files found in $path"
        return
    }

    # Splat all matching files as separate arguments -> opens them as tabs.
    logreader @files
}
