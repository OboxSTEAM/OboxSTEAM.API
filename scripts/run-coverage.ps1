#Requires -Version 5.1
<#
.SYNOPSIS
  Runs Application unit tests with Coverlet coverage (excludes seed / media / infra).

.PARAMETER Filter
  Optional xUnit filter, e.g. "FullyQualifiedName~ClassServiceTests"

.PARAMETER SkipReport
  Skip HTML report generation even if reportgenerator is available.

.EXAMPLE
  .\scripts\run-coverage.ps1

.EXAMPLE
  .\scripts\run-coverage.ps1 -Filter "FullyQualifiedName~ProgramServiceTests"
#>
param(
    [string]$Filter = "",
    [switch]$SkipReport
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$resultsDir = Join-Path $root "TestResults\coverage"
$settings = Join-Path $root "coverage.runsettings"
$testProject = Join-Path $root "OboxSteam.Test\OboxSteam.Test.csproj"
$reportDir = Join-Path $root "TestResults\coverage-report"

if (-not (Test-Path $settings)) {
    throw "Missing coverage.runsettings at repo root."
}

if (Test-Path $resultsDir) {
    Remove-Item -Recurse -Force $resultsDir
}

$testArgs = @(
    "test", $testProject,
    "--collect:XPlat Code Coverage",
    "--settings", $settings,
    "--results-directory", $resultsDir,
    "--nologo"
)

if (-not [string]::IsNullOrWhiteSpace($Filter)) {
    $testArgs += @("--filter", $Filter)
}

Write-Host "Running: dotnet $($testArgs -join ' ')" -ForegroundColor Cyan
& dotnet @testArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet test failed with exit code $LASTEXITCODE"
}

$cobertura = Get-ChildItem -Path $resultsDir -Recurse -Filter "coverage.cobertura.xml" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $cobertura) {
    throw "No coverage.cobertura.xml found under $resultsDir"
}

Write-Host ""
Write-Host "Coverage file: $($cobertura.FullName)" -ForegroundColor Green

# Quick sanity: excluded services should not appear
$excludedHits = Select-String -Path $cobertura.FullName -Pattern 'Services\.(Seed|Media|PersonalVideo)Service"' -SimpleMatch:$false
if ($excludedHits) {
    Write-Host "Warning: excluded services still appear in cobertura:" -ForegroundColor Yellow
    $excludedHits | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
}
else {
    Write-Host "Excluded services (Seed/Media/PersonalVideo) not present in report." -ForegroundColor Green
}

# Summarize Application service line-rates
# Package-level line rate (Application assembly after excludes)
$packageLine = Select-String -Path $cobertura.FullName -Pattern 'package name="OboxSteam.Application" line-rate="([^"]+)"' |
    Select-Object -First 1
if ($packageLine -and $packageLine.Line -match 'line-rate="([^"]+)"') {
    $overall = [double]$Matches[1]
    Write-Host ""
    Write-Host ("Overall OboxSteam.Application line coverage: {0:P1}" -f $overall) -ForegroundColor Green
}

Write-Host ""
Write-Host "Application service line coverage (top-level classes only):" -ForegroundColor Cyan
Select-String -Path $cobertura.FullName -Pattern 'class name="OboxSteam\.Application\.Services\.[^"/]+" filename="[^"]+" line-rate="([^"]+)"' |
    ForEach-Object {
        if ($_.Line -match 'Services\.([^"]+)".*line-rate="([^"]+)"') {
            $name = $Matches[1]
            $rate = [double]$Matches[2]
            '{0,-40} {1,6:P1}' -f $name, $rate
        }
    } |
    Sort-Object

Write-Host ""
Write-Host "Note: 0% rows are Application services not yet covered by unit tests (backlog)." -ForegroundColor DarkGray
Write-Host "SeedService / MediaService / PersonalVideoService are excluded from this sample." -ForegroundColor DarkGray

if ($SkipReport) {
    return
}

$reportgenerator = Get-Command reportgenerator -ErrorAction SilentlyContinue
if (-not $reportgenerator) {
    Write-Host ""
    Write-Host "reportgenerator not found. Install once:" -ForegroundColor DarkYellow
    Write-Host "  dotnet tool install -g dotnet-reportgenerator-globaltool" -ForegroundColor DarkYellow
    Write-Host "Then re-run this script, or open the cobertura XML directly." -ForegroundColor DarkYellow
    return
}

if (Test-Path $reportDir) {
    Remove-Item -Recurse -Force $reportDir
}

& reportgenerator `
    "-reports:$($cobertura.FullName)" `
    "-targetdir:$reportDir" `
    "-reporttypes:Html;TextSummary" `
    "-classfilters:-OboxSteam.Application.Services.SeedService;-OboxSteam.Application.Services.MediaService;-OboxSteam.Application.Services.PersonalVideoService;-OboxSteam.Infrastructure.*;-OboxSteam.API.*" `
    "-filefilters:-**/SeedService.cs;-**/MediaService.cs;-**/PersonalVideoService.cs;-**/OboxSteam.Infrastructure/**;-**/OboxSteam.API/**;-**/Migrations/**"

Write-Host ""
Write-Host "HTML report: $reportDir\index.html" -ForegroundColor Green
$summary = Join-Path $reportDir "Summary.txt"
if (Test-Path $summary) {
    Write-Host ""
    Get-Content $summary
}
