param(
    [string]$EaPath = "$PSScriptRoot\ExpertAdvisor.mq5",
    [string]$LogPath = "$PSScriptRoot\build\metaeditor-compile.log"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-MetaEditorPath {
    $candidates = @(
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
        if (-not (Test-Path $root)) {
            continue
        }

        $found = Get-ChildItem -Path $root -Recurse -Filter 'metaeditor*.exe' -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1 -ExpandProperty FullName

        if ($found) {
            return $found
        }
    }

    return $null
}

if (-not (Test-Path $EaPath)) {
    throw "EA file not found: $EaPath"
}

$metaEditorPath = Resolve-MetaEditorPath
if (-not $metaEditorPath) {
    throw "MetaEditor executable not found. Install MetaTrader 5 first: https://download.terminal.free/cdn/web/metaquotes.ltd/mt5/mt5setup.exe"
}

$logDirectory = Split-Path -Path $LogPath -Parent
if (-not (Test-Path $logDirectory)) {
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
}

$eaAbsolutePath = (Resolve-Path $EaPath).Path
$logAbsolutePath = (Resolve-Path $logDirectory).Path + '\\' + (Split-Path -Path $LogPath -Leaf)

Write-Host "Using MetaEditor: $metaEditorPath"
Write-Host "Compiling EA: $eaAbsolutePath"
Write-Host "Compile log: $logAbsolutePath"

$arguments = @(
    "/compile:$eaAbsolutePath",
    "/log:$logAbsolutePath"
)

$process = Start-Process -FilePath $metaEditorPath -ArgumentList $arguments -PassThru -Wait -NoNewWindow

if (Test-Path $logAbsolutePath) {
    $logContent = Get-Content $logAbsolutePath -Raw
    if ($logContent -match 'Result:\s+(\d+)\s+errors') {
        $errorCount = [int]$Matches[1]
        if ($errorCount -gt 0) {
            throw "MetaEditor compile found $errorCount errors. Check log: $logAbsolutePath"
        }
        Write-Host "Compile completed successfully."
        Write-Host ($logContent -split "`n" | Select-Object -Last 3 | Out-String)
    }
} else {
    throw "Compile log not found at $logAbsolutePath"
}
