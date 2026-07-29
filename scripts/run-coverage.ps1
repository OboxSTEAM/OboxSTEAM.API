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
Write-Host "Seed/Media/PersonalVideo services and their dedicated DTOs/helpers are excluded." -ForegroundColor DarkGray

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

# NOTE: Do NOT use -**/OboxSteam.API/** in filefilters — it matches OboxSteam.Application
# (prefix collision) and excludes the entire Application assembly from the HTML report.
& reportgenerator `
    "-reports:$($cobertura.FullName)" `
    "-targetdir:$reportDir" `
    "-reporttypes:Html;TextSummary" `
    "-classfilters:-OboxSteam.Application.Services.SeedService;-OboxSteam.Application.Services.MediaService;-OboxSteam.Application.Services.PersonalVideoService;-OboxSteam.Application.DTOs.MediaDTO.*;-OboxSteam.Application.Interfaces.ISeedService;-OboxSteam.Application.Interfaces.IMediaService;-OboxSteam.Application.Interfaces.IPersonalVideoService;-OboxSteam.Application.Interfaces.IPersonalVideoQueue;-OboxSteam.Application.Interfaces.PersonalVideoJob;-OboxSteam.Application.Interfaces.PersonalVideoJobKind;-OboxSteam.Application.Interfaces.IVideoConverterService;-OboxSteam.Application.Interfaces.ClipInput;-OboxSteam.Application.Interfaces.TimeClip;-OboxSteam.Application.Interfaces.IStrengthMatchService;-OboxSteam.Application.Interfaces.StrengthMatchResult;-OboxSteam.Application.Interfaces.MatchedSegment;-OboxSteam.Application.Utils.HighlightVideoManifestHelper;-OboxSteam.Application.Utils.HighlightVideoClipMergeHelper;-OboxSteam.Application.Utils.HighlightVideoTimeHelper;-OboxSteam.Application.Utils.HighlightVideoConstants;-OboxSteam.Application.Utils.HighlightClipMediaPair;-OboxSteam.Application.Utils.ParsedHighlightManifest;-OboxSteam.Application.Utils.HighlightSourceManifestDocument;-OboxSteam.Application.Utils.HighlightSourceSegmentMs;-OboxSteam.Application.Utils.HighlightSourceClipGroup;-OboxSteam.Application.Services.NotificationPublisher;-OboxSteam.Application.Services.NotificationRecipientResolver;-OboxSteam.Application.Services.PortfolioHtmlSanitizer;-OboxSteam.Application.Commons.ClaimsService;-OboxSteam.Application.Commons.PaginationParameter;-OboxSteam.Application.Commons.StripeSettings;-OboxSteam.Application.Utils.ApiResult;-OboxSteam.Application.Utils.ApiResult*;-OboxSteam.Application.Utils.ErrorContent;-OboxSteam.Application.Utils.ResponseContent;-OboxSteam.Application.Utils.ResponseDataContent*;-OboxSteam.Application.Utils.ExceptionUtils;-OboxSteam.Application.Utils.HashHelper;-OboxSteam.Application.Utils.ResourceHelper;-OboxSteam.Application.Utils.StringExtensions;-OboxSteam.Application.Utils.StringTools;-OboxSteam.Application.Validation.JsonObjectAttribute;-OboxSteam.Application.Exceptions.AppException;-OboxSteam.Domain.Entities.FaceEmbedding;-OboxSteam.Domain.Entities.MediaTag;-OboxSteam.Domain.Entities.MediaAsset;-OboxSteam.Domain.Entities.ActivityBooking;-OboxSteam.Domain.Entities.CourseEnrollment;-OboxSteam.Domain.Entities.StandardizedTest;-OboxSteam.Domain.Entities.StudentProfile;-OboxSteam.Domain.Entities.StudentSkill;-OboxSteam.Domain.Entities.StudentSkillEvidence;-OboxSteam.Domain.Entities.HighlightVideoItem;-OboxSteam.Application.DTOs.AuthDTO.ForgotPasswordRequestDto;-OboxSteam.Application.DTOs.AuthDTO.ResendOtpRequestDto;-OboxSteam.Application.DTOs.AuthDTO.ResetPasswordDto;-OboxSteam.Application.DTOs.AuthDTO.VerifyOtpDto;-OboxSteam.Application.DTOs.PaymentDTO.CheckoutRequestDto;-OboxSteam.Application.DTOs.PaymentDTO.ModuleRetakeCheckoutRequestDto;-OboxSteam.Application.DTOs.PaymentDTO.ParentCheckoutRequestDto;-OboxSteam.Application.DTOs.PaymentDTO.ParentModulePaymentRequestDto;-OboxSteam.Application.DTOs.PaymentDTO.ParentPaymentRequestDto;-OboxSteam.Application.DTOs.EmailDTO.EnrollmentConfirmationEmailDto;-OboxSteam.Application.DTOs.EmailDTO.SendEmailDto;-OboxSteam.Application.DTOs.NotificationDTO.MarkNotificationReadDto;-OboxSteam.Application.DTOs.ActivityProgressDTO.ForceCompleteActivityRequestDto;-OboxSteam.Application.DTOs.BankQuestionDTO.UpdateBankQuestionOptionRequestDto;-OboxSteam.Application.DTOs.BankQuestionDTO.UpdateBankQuestionRequestDto;-OboxSteam.Application.DTOs.QuestionBankDTO.UpdateQuestionBankRequestDto;-OboxSteam.Application.DTOs.ProgramDTO.ProgramCurriculumMaterialDto;-OboxSteam.Application.DTOs.ProgramDTO.ProgramCurriculumMilestoneDto;-OboxSteam.Application.DTOs.PortfolioDTO.UpdatePortfolioSettingsRequestDto;-OboxSteam.Application.Interfaces.FaceMatchResult;-OboxSteam.Application.Interfaces.FaceTimestampSegment;-OboxSteam.Application.Interfaces.LabelDetectionEntry;-OboxSteam.Application.Interfaces.VideoFaceSearchResult;-OboxSteam.Application.Interfaces.VideoFaceTimelineResult;-OboxSteam.Application.Notifications.NotificationCatalog;-OboxSteam.Application.Notifications.NotificationCommand;-OboxSteam.Application.Notifications.NotificationAudience;-OboxSteam.Application.Notifications.NotificationPayload;-OboxSteam.Application.Notifications.INotificationPublisher;-OboxSteam.Application.Commons.ProgramCurriculumTreeLoader;-OboxSteam.Application.Commons.ProgramCurriculumTreeLoader.*;-OboxSteam.Application.Utils.TokenTools;-System.Text.RegularExpressions.Generated.*" `
    "-filefilters:+**/OboxSteam.Application/**;+**/OboxSteam.Domain/**;-**/obj/**;-**/Migrations/**;-**/Services/SeedService.cs;-**/Services/Seed/**;-**/Services/MediaService.cs;-**/Services/PersonalVideoService.cs;-**/DTOs/MediaDTO/**;-**/Interfaces/ISeedService.cs;-**/Interfaces/IMediaService.cs;-**/Interfaces/IPersonalVideoService.cs;-**/Interfaces/IPersonalVideoQueue.cs;-**/Interfaces/IVideoConverterService.cs;-**/Interfaces/IStrengthMatchService.cs;-**/Utils/HighlightVideoManifestHelper.cs;-**/Utils/HighlightVideoClipMergeHelper.cs;-**/Utils/HighlightVideoTimeHelper.cs;-**/Utils/HighlightVideoConstants.cs;-**/Services/NotificationPublisher.cs;-**/Services/NotificationRecipientResolver.cs;-**/Services/PortfolioHtmlSanitizer.cs;-**/Commons/ClaimsService.cs;-**/Commons/PaginationParameter.cs;-**/Commons/StripeSettings.cs;-**/Notifications/NotificationCatalog.cs;-**/Commons/ProgramCurriculumTreeLoader.cs"

Write-Host ""
Write-Host "HTML report: $reportDir\index.html" -ForegroundColor Green
$summary = Join-Path $reportDir "Summary.txt"
if (Test-Path $summary) {
    Write-Host ""
    Get-Content $summary
}
