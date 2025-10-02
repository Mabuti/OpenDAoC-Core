param(
    [ValidateNotNullOrEmpty()]
    [ValidateSet("Debug", "Trace")]
    [string]$LogLevel = "Debug"
)

$logConfigPath = Join-Path (Join-Path $PSScriptRoot 'GameServer') 'config/logconfig.xml'
if (-not (Test-Path $logConfigPath)) {
    Write-Error "Log configuration file not found at $logConfigPath"
    exit 1
}

$logConfig = Get-Content -Path $logConfigPath -Raw
$replacement = "minLevel=""$LogLevel"""
$updatedLogConfig = [System.Text.RegularExpressions.Regex]::Replace(
    $logConfig,
    'minLevel="(Trace|Debug)"',
    $replacement)

if ($updatedLogConfig -ne $logConfig) {
    Set-Content -Path $logConfigPath -Value $updatedLogConfig -Encoding UTF8
    Write-Host "Updated logging level in logconfig.xml to $LogLevel"
} else {
    Write-Host "Logging level already set to $LogLevel"
}

docker build --no-cache -t opendaoc-singleplayerbots:latest .
docker tag opendaoc-singleplayerbots:latest localhost:5000/opendaoc-singleplayerbots:latest
docker push localhost:5000/opendaoc-singleplayerbots:latest
docker compose pull
docker compose up -d
