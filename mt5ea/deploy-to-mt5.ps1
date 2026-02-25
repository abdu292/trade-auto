param(
    [string]$TerminalId = "",
    [switch]$CompileAfterCopy
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

$terminalPath = Resolve-TerminalPath -Id $TerminalId
if (-not $terminalPath) {
    throw "Could not detect MT5 terminal with MQL5\Experts. Open MT5 once and retry."
}

$targetRoot = Join-Path $terminalPath "MQL5\Experts\TradeAuto"
New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null

Copy-Item -Path (Join-Path $repoEaRoot "ExpertAdvisor.mq5") -Destination $targetRoot -Force
Copy-Item -Path (Join-Path $repoEaRoot "ExpertAdvisor.ex5") -Destination $targetRoot -Force -ErrorAction SilentlyContinue

foreach ($dir in @("Http", "Models", "Services")) {
    $src = Join-Path $repoEaRoot $dir
    $dst = Join-Path $targetRoot $dir
    if (Test-Path $src) {
        if (Test-Path $dst) { Remove-Item $dst -Recurse -Force }
        Copy-Item -Path $src -Destination $dst -Recurse -Force
    }
}

Write-Host "EA deployed to: $targetRoot" -ForegroundColor Green
Write-Host "MT5 Terminal: $terminalPath" -ForegroundColor Green

if ($CompileAfterCopy) {
    $metaEditorCandidates = @(
        (Join-Path $terminalPath "metaeditor64.exe"),
        (Join-Path $terminalPath "metaeditor.exe"),
        "C:\Program Files\MetaTrader 5\metaeditor64.exe",
        "C:\Program Files\MetaTrader 5\metaeditor.exe"
    )

    $metaEditor = $metaEditorCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($metaEditor) {
        $eaPath = Join-Path $targetRoot "ExpertAdvisor.mq5"
        $logPath = Join-Path $repoEaRoot "build\metaeditor-compile.log"
        & $metaEditor /compile:"$eaPath" /log:"$logPath"
        Write-Host "Compile invoked via MetaEditor: $metaEditor" -ForegroundColor Yellow
    }
    else {
        Write-Warning "MetaEditor not found for compile step."
    }
}

Write-Host "Next: In MT5 Navigator, right-click Expert Advisors and click Refresh." -ForegroundColor Cyan
