param(
    [string]$TerminalId = ""
)

# This wrapper simply calls the existing helper scripts in order:
# 1. compile-ea.ps1 – compiles the MQL5 source locally
# 2. deploy-to-mt5.ps1 – copies both source and compiled binaries to the
#    MT5 terminal (it also optionally invokes compilation again inside MT5).

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "Running build-and-deploy wrapper..." -ForegroundColor Cyan

# compile first
& "$PSScriptRoot\compile-ea.ps1" -EaPath "$PSScriptRoot\ExpertAdvisor.mq5" -LogPath "$PSScriptRoot\build\metaeditor-compile.log"

# then deploy (no need to recompile inside MT5 since we compiled above)
& "$PSScriptRoot\deploy-to-mt5.ps1" -TerminalId $TerminalId


