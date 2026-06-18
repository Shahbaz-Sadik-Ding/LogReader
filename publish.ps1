<#
    Builds a single, self-contained LogReader.exe for Windows x64.
    Colleagues can run the resulting .exe directly — no .NET install required.

    Usage (from the project folder, in PowerShell):
        ./publish.ps1
#>

$ErrorActionPreference = 'Stop'

$outDir = Join-Path $PSScriptRoot 'dist'

Write-Host 'Publishing LogReader (single-file, self-contained, win-x64)...' -ForegroundColor Cyan

dotnet publish "$PSScriptRoot/LogReader.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -o "$outDir"

$exe = Join-Path $outDir 'LogReader.exe'
if (Test-Path $exe) {
    $sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host ""
    Write-Host "Done. EXE created ($sizeMb MB):" -ForegroundColor Green
    Write-Host "  $exe"
    Write-Host "Share that single file — colleagues just double-click it to run."
    explorer.exe "/select,`"$exe`""
} else {
    throw "Publish finished but LogReader.exe was not found in $outDir"
}
