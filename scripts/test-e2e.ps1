param()

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $repoRoot "web\KotobaColiseum.Web\KotobaColiseum.Web.csproj"
$testProject = Join-Path $repoRoot "tests\KotobaColiseum.E2E\KotobaColiseum.E2E.csproj"
$appDataRoot = Join-Path $repoRoot "tests\.tmp\app-data"
$logRoot = Join-Path $repoRoot "tests\.tmp\logs"
$serverOutLog = Join-Path $logRoot "web.stdout.log"
$serverErrorLog = Join-Path $logRoot "web.stderr.log"
$port = 5072
$baseUrl = "http://127.0.0.1:$port"

if (Test-Path $appDataRoot) {
    Remove-Item -Recurse -Force $appDataRoot
}

if (Test-Path $logRoot) {
    Remove-Item -Recurse -Force $logRoot
}

New-Item -ItemType Directory -Force -Path $appDataRoot | Out-Null
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null

dotnet build $webProject
dotnet build $testProject

$playwrightScript = Join-Path $repoRoot "tests\KotobaColiseum.E2E\bin\Debug\net10.0\playwright.ps1"
if (-not (Test-Path $playwrightScript)) {
    throw "Playwright install script not found at $playwrightScript"
}

powershell -ExecutionPolicy Bypass -File $playwrightScript install chromium

Get-ChildItem (Join-Path $repoRoot "tests\KotobaColiseum.E2E\bin\Debug\net10.0") -File | Unblock-File -ErrorAction SilentlyContinue

$env:ASPNETCORE_URLS = $baseUrl
$env:DOTNET_ENVIRONMENT = "Development"
$env:KOTOBA_COLISEUM_APPDATA_ROOT = $appDataRoot
$env:KotobaColiseum__OpenAi__ForceMockMode = "true"

$serverProcess = Start-Process `
    -FilePath "dotnet" `
    -ArgumentList @("run", "--no-build", "--no-launch-profile", "--project", $webProject) `
    -WorkingDirectory $repoRoot `
    -RedirectStandardOutput $serverOutLog `
    -RedirectStandardError $serverErrorLog `
    -PassThru

try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri "$baseUrl/health" -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -eq 200) {
                $ready = $true
                break
            }
        }
        catch {
        }

        Start-Sleep -Seconds 1
    }

    if (-not $ready) {
        throw "Web app did not become ready. See $serverOutLog and $serverErrorLog"
    }

    $env:KOTOBA_COLISEUM_E2E_BASE_URL = $baseUrl
    dotnet test $testProject --no-build
}
finally {
    if ($serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
    }
}
