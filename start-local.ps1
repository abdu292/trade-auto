param(
    [string]$BrainUrl = "http://127.0.0.1:5000",
    [string]$AiWorkerUrl = "http://127.0.0.1:8001"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$brainProject = Join-Path $root "brain\src\Web\Web.csproj"
$aiWorkerDir = Join-Path $root "aiworker"
$venvPython = Join-Path $aiWorkerDir ".venv\Scripts\python.exe"

if (-not (Test-Path $brainProject)) {
    throw "Brain project not found at: $brainProject"
}

if (-not (Test-Path $aiWorkerDir)) {
    throw "AI Worker directory not found at: $aiWorkerDir"
}

function Test-PortListening {
    param([int]$Port)
    $lines = netstat -ano | Select-String ":$Port\s+.*LISTENING"
    return $null -ne $lines
}

$brainUri = [Uri]$BrainUrl
$aiUri = [Uri]$AiWorkerUrl

$brainCmd = "Set-Location -LiteralPath '$root'; dotnet run --project '$brainProject'"

if (Test-Path $venvPython) {
    $aiCmd = "Set-Location -LiteralPath '$aiWorkerDir'; & '$venvPython' -m uvicorn app.main:app --host 127.0.0.1 --port $($aiUri.Port) --reload"
}
else {
    $aiCmd = "Set-Location -LiteralPath '$aiWorkerDir'; python -m uvicorn app.main:app --host 127.0.0.1 --port $($aiUri.Port) --reload"
}

Write-Host "Starting AI Worker..." -ForegroundColor Cyan
if (Test-PortListening -Port $aiUri.Port) {
    Write-Host "AI Worker appears to be already running on port $($aiUri.Port); skipping launch." -ForegroundColor Yellow
}
else {
    Start-Process -FilePath "powershell" -ArgumentList @("-NoExit", "-Command", $aiCmd) -WindowStyle Normal
}

Write-Host "Starting Brain..." -ForegroundColor Cyan
if (Test-PortListening -Port $brainUri.Port) {
    Write-Host "Brain appears to be already running on port $($brainUri.Port); skipping launch." -ForegroundColor Yellow
}
else {
    Start-Process -FilePath "powershell" -ArgumentList @("-NoExit", "-Command", $brainCmd) -WindowStyle Normal
}

Write-Host "Waiting for services to start..." -ForegroundColor Green

Start-Sleep -Seconds 3

function Test-Url {
    param([string]$Url)
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2
        return $response.StatusCode
    }
    catch {
        return $null
    }
}

$brainHealth = Test-Url "$BrainUrl/health"
$aiHealth = Test-Url "$AiWorkerUrl/health"

$brainStatus = if ($null -eq $brainHealth) { "not ready" } else { "$brainHealth" }
$aiStatus = if ($null -eq $aiHealth) { "not ready" } else { "$aiHealth" }

Write-Host "Health checks:" -ForegroundColor Yellow
Write-Host "  Brain   ($BrainUrl/health): $brainStatus"
Write-Host "  AIWorker($AiWorkerUrl/health): $aiStatus"

Write-Host ""
Write-Host "Tip: If Brain calls AI worker, make sure Brain config External:AIWorkerBaseUrl points to $AiWorkerUrl" -ForegroundColor DarkYellow
