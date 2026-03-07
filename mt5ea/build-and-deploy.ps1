param(
    [string]$TerminalId = "",
    [string]$LogPath = "$PSScriptRoot\build\metaeditor-compile.log"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoEaRoot = $PSScriptRoot
$terminalRoot = Join-Path $env:APPDATA "MetaQuotes\Terminal"

if (-not (Test-Path $terminalRoot)) {
    throw "MT5 terminal folder not found: $terminalRoot"
}

function Resolve-TerminalPath {
    param([string]$Id)

    if ($Id -and (Test-Path (Join-Path $terminalRoot $Id))) {
        return (Join-Path $terminalRoot $Id)
    }

    $candidates = @(Get-ChildItem -Path $terminalRoot -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName "MQL5\Experts") } |
        Sort-Object LastWriteTime -Descending)

    if ($candidates.Count -eq 0) {
        return $null
    }

    return $candidates[0].FullName
}

function Resolve-MetaEditorPath {
    param([string]$TerminalPath)

    $candidates = @(
        (Join-Path $TerminalPath "metaeditor64.exe"),
        (Join-Path $TerminalPath "metaeditor.exe"),
        'C:\Program Files\MetaTrader 5\metaeditor64.exe',
        'C:\Program Files\MetaTrader 5\metaeditor.exe',
        'C:\Program Files (x86)\MetaTrader 5\metaeditor64.exe',
        'C:\Program Files (x86)\MetaTrader 5\metaeditor.exe',
        "$env:APPDATA\MetaTrader 5\MetaEditor64.exe",
        "$env:APPDATA\MetaTrader 5\metaeditor.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $searchRoots = @(
        "$env:APPDATA\MetaQuotes\Terminal",
        "$env:LOCALAPPDATA\MetaQuotes\Terminal",
        "$env:LOCALAPPDATA\Programs"
    )

    foreach ($root in $searchRoots) {
        if (-not (Test-Path $root)) { continue }

        $found = Get-ChildItem -Path $root -Recurse -Filter 'metaeditor*.exe' -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1 -ExpandProperty FullName

        if ($found) { return $found }
    }

    return $null
}

# ── Step 1: Resolve terminal path ────────────────────────────────────────────

$terminalPath = Resolve-TerminalPath -Id $TerminalId
if (-not $terminalPath) {
    throw "Could not detect MT5 terminal with MQL5\Experts. Open MT5 once and retry."
}

Write-Host "MT5 Terminal: $terminalPath" -ForegroundColor Cyan

# ── Step 2: Deploy source files ──────────────────────────────────────────────

$targetRoot = Join-Path $terminalPath "MQL5\Experts\TradeAuto"
New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null

Copy-Item -Path (Join-Path $repoEaRoot "ExpertAdvisor.mq5") -Destination $targetRoot -Force

foreach ($dir in @("Http", "Models", "Services")) {
    $src = Join-Path $repoEaRoot $dir
    $dst = Join-Path $targetRoot $dir
    if (Test-Path $src) {
        if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
        Copy-Item -Path $src -Destination $dst -Recurse -Force
    }
}

Write-Host "Source files deployed to: $targetRoot" -ForegroundColor Green

# ── Step 3: Compile ──────────────────────────────────────────────────────────

$metaEditorPath = Resolve-MetaEditorPath -TerminalPath $terminalPath
if (-not $metaEditorPath) {
    throw "MetaEditor executable not found. Install MetaTrader 5 first: https://download.terminal.free/cdn/web/metaquotes.ltd/mt5/mt5setup.exe"
}

$logDirectory = Split-Path -Path $LogPath -Parent
if (-not (Test-Path $logDirectory)) {
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
}

$eaPath = Join-Path $targetRoot "ExpertAdvisor.mq5"
$logAbsolutePath = (Get-Item $logDirectory).FullName + '\' + (Split-Path -Path $LogPath -Leaf)

Write-Host "Using MetaEditor: $metaEditorPath"
Write-Host "Compiling EA: $eaPath"
Write-Host "Compile log: $logAbsolutePath"

$arguments = @(
    "/compile:$eaPath",
    "/log:$logAbsolutePath"
)

Start-Process -FilePath $metaEditorPath -ArgumentList $arguments -Wait -NoNewWindow

if (Test-Path $logAbsolutePath) {
    $logContent = Get-Content $logAbsolutePath -Raw
    if ($logContent -match 'Result:\s+(\d+)\s+errors') {
        $errorCount = [int]$Matches[1]
        if ($errorCount -gt 0) {
            throw "MetaEditor compile found $errorCount errors. Check log: $logAbsolutePath"
        }
        Write-Host "Compile completed successfully." -ForegroundColor Green
        Write-Host ($logContent -split "`n" | Select-Object -Last 3 | Out-String)
    }
} else {
    throw "Compile log not found at $logAbsolutePath"
}

# ── Step 4: Copy compiled binary back to repo build folder ───────────────────

$compiledEx5 = Join-Path $targetRoot "ExpertAdvisor.ex5"
if (Test-Path $compiledEx5) {
    $buildDir = Join-Path $repoEaRoot "build"
    New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
    Copy-Item -Path $compiledEx5 -Destination $buildDir -Force
    Write-Host "Compiled binary saved to: $buildDir\ExpertAdvisor.ex5" -ForegroundColor Green
}

Write-Host ""
Write-Host "Build and deploy complete." -ForegroundColor Green
Write-Host "Next: In MT5 Navigator, right-click Expert Advisors and click Refresh." -ForegroundColor Cyan
