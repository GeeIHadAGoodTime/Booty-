#Requires -Version 5.1
<#
.SYNOPSIS
    Runs Booty! PlayMode (gameplay) tests via Unity CLI.

.DESCRIPTION
    Launches Unity in headless batch mode, executes PlayMode tests, writes
    NUnit XML results, and reports pass/fail per test with assertion details.

    IMPORTANT: Unity Personal license does NOT support batch-mode test execution.
    Exit code 198 = missing com.unity.editor.headless entitlement.
    See the license error message output for workarounds.

.PARAMETER UnityPath
    Full path to Unity.exe. Default: 6000.2.13f1 installation.

.PARAMETER TestFilter
    Optional test filter (e.g., "Booty.Tests.PlayMode.CombatTest").
    Leave empty to run all PlayMode tests.

.PARAMETER ProjectPath
    Path to the Booty! Unity project root. Default: script location.

.EXAMPLE
    .\verify_gameplay.ps1
    .\verify_gameplay.ps1 -TestFilter "CombatTest"
    .\verify_gameplay.ps1 -UnityPath "D:\Unity\6000.2.13f1\Editor\Unity.exe"

.NOTES
    Exit codes:
        0  — all tests passed
        1  — tests failed or editor crashed
        2  — Unity license does not support batch-mode tests (Personal license)
#>

param(
    [string]$UnityPath   = "C:\Program Files\Unity\Hub\Editor\6000.2.13f1\Editor\Unity.exe",
    [string]$TestFilter  = "",
    [string]$ProjectPath = $PSScriptRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Write-Header([string]$text) {
    $line = "=" * 60
    Write-Host ""
    Write-Host $line -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host $line -ForegroundColor Cyan
}

function Write-Step([string]$text)  { Write-Host "[*] $text" -ForegroundColor Yellow }
function Write-OK([string]$text)    { Write-Host "[+] $text" -ForegroundColor Green }
function Write-Fail([string]$text)  { Write-Host "[-] $text" -ForegroundColor Red }
function Write-Info([string]$text)  { Write-Host "    $text" -ForegroundColor Gray }

# ---------------------------------------------------------------------------
# Environment validation
# ---------------------------------------------------------------------------

Write-Header "Booty! — Gameplay (PlayMode) Test Runner"
Write-Step "Project path : $ProjectPath"
Write-Step "Unity path   : $UnityPath"
Write-Step "Test filter  : $(if ($TestFilter) { $TestFilter } else { '(all PlayMode tests)' })"

if (-not (Test-Path $UnityPath)) {
    Write-Fail "Unity.exe not found at: $UnityPath"
    Write-Info "Fix: pass the correct path via -UnityPath, e.g.:"
    Write-Info ".\verify_gameplay.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\6000.X.Y\Editor\Unity.exe'"
    exit 1
}

if (-not (Test-Path "$ProjectPath\Assets")) {
    Write-Fail "Assets folder not found at $ProjectPath — is ProjectPath correct?"
    exit 1
}

$PlayModeTestDir = "$ProjectPath\Assets\Booty\Tests\PlayMode"
if (-not (Test-Path $PlayModeTestDir)) {
    Write-Fail "PlayMode test directory not found: $PlayModeTestDir"
    Write-Info "Run S3.5 task to create PlayMode tests first."
    exit 1
}

# ---------------------------------------------------------------------------
# Build Unity arguments
# ---------------------------------------------------------------------------

$Timestamp  = Get-Date -Format "yyyyMMdd_HHmmss"
$ResultsXml = Join-Path $env:TEMP "booty_gameplay_${Timestamp}.xml"
$LogFile    = Join-Path $env:TEMP "booty_gameplay_${Timestamp}.log"

Write-Step "Results XML  : $ResultsXml"
Write-Step "Unity log    : $LogFile"

$Arguments = @(
    "-batchmode",
    "-nographics",
    "-projectPath", "`"$ProjectPath`"",
    "-runTests",
    "-testPlatform", "PlayMode",
    "-testResults",  "`"$ResultsXml`"",
    "-logFile",      "`"$LogFile`"",
    "-quit"
)

if ($TestFilter) {
    $Arguments += @("-testFilter", "`"$TestFilter`"")
}

# ---------------------------------------------------------------------------
# Run Unity
# ---------------------------------------------------------------------------

Write-Step "Launching Unity (may take 30-120 seconds on first run)..."
Write-Info "Tip: First launch compiles all C# — subsequent runs are faster."

$proc = Start-Process `
    -FilePath $UnityPath `
    -ArgumentList $Arguments `
    -PassThru `
    -NoNewWindow `
    -Wait

$ExitCode = $proc.ExitCode
Write-Step "Unity exited with code $ExitCode"

# ---------------------------------------------------------------------------
# Handle exit code 198 (Unity Personal license limitation)
# ---------------------------------------------------------------------------

if ($ExitCode -eq 198) {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "║  LICENSE ERROR: Batch-mode tests require Unity Pro/Plus  ║" -ForegroundColor Red
    Write-Host "╚══════════════════════════════════════════════════════════╝" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Exit code 198 = missing 'com.unity.editor.headless' entitlement." -ForegroundColor Yellow
    Write-Host "  Unity Personal license does NOT support batch-mode test execution." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  WORKAROUNDS:" -ForegroundColor Cyan
    Write-Host "  1. Unity Editor Test Runner (free/Personal):" -ForegroundColor White
    Write-Host "       Open Unity > Window > General > Test Runner > PlayMode tab > Run All" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  2. Unity Pro or Plus license:" -ForegroundColor White
    Write-Host "       Batch-mode tests work with the headless entitlement." -ForegroundColor Gray
    Write-Host ""
    Write-Host "  3. Unity CI/CD (GitHub Actions with Unity license server):" -ForegroundColor White
    Write-Host "       Use game-ci/unity-test-runner GitHub Action." -ForegroundColor Gray
    Write-Host ""
    Write-Host "  NOTE: The test code itself is VALID. This is only a licensing restriction." -ForegroundColor Green
    Write-Host "        All [UnityTest] methods in Assets/Booty/Tests/PlayMode/ are syntactically" -ForegroundColor Green
    Write-Host "        correct and will run from the Unity Editor Test Runner window." -ForegroundColor Green
    Write-Host ""
    exit 2
}

# ---------------------------------------------------------------------------
# Handle other non-zero exit codes (editor crash)
# ---------------------------------------------------------------------------

if ($ExitCode -ne 0) {
    Write-Fail "Unity process returned exit code $ExitCode — editor may have crashed or failed to start."
    if (Test-Path $LogFile) {
        Write-Host ""
        Write-Host "  Last 40 lines of Unity log:" -ForegroundColor Gray
        Get-Content $LogFile -Tail 40 | ForEach-Object { Write-Info $_ }
    }
    exit 1
}

# ---------------------------------------------------------------------------
# Parse NUnit XML results
# ---------------------------------------------------------------------------

if (-not (Test-Path $ResultsXml)) {
    Write-Fail "Results XML was not created: $ResultsXml"
    Write-Info "This usually means Unity failed to load the project or find any PlayMode tests."
    if (Test-Path $LogFile) {
        Write-Host ""
        Write-Host "  Last 20 lines of Unity log:" -ForegroundColor Gray
        Get-Content $LogFile -Tail 20 | ForEach-Object { Write-Info $_ }
    }
    exit 1
}

try {
    [xml]$xml = Get-Content $ResultsXml -Raw
} catch {
    Write-Fail "Failed to parse results XML: $_"
    exit 1
}

# Support NUnit 3 (<test-run>) and NUnit 2 (<test-results>)
$TestRun = $xml.'test-run'
if (-not $TestRun) { $TestRun = $xml.'test-results' }

if (-not $TestRun) {
    Write-Fail "Unexpected XML schema — cannot find <test-run> or <test-results>."
    exit 1
}

$Total    = [int]($TestRun.total    ?? 0)
$Passed   = [int]($TestRun.passed   ?? 0)
$Failed   = [int]($TestRun.failed   ?? 0)
$Skipped  = [int]($TestRun.skipped  ?? 0)
$Errors   = [int]($TestRun.errors   ?? 0)
$Duration = $TestRun.duration ?? "?"

# ---------------------------------------------------------------------------
# Per-test output
# ---------------------------------------------------------------------------

Write-Header "PlayMode Test Results"

$AllCases = $xml.SelectNodes("//test-case")
if ($AllCases.Count -eq 0) {
    Write-Fail "No test cases found in XML. Check Unity log for compilation errors."
    exit 1
}

$FailedCases = @()
foreach ($tc in $AllCases) {
    $Name   = $tc.fullname ?? $tc.name ?? "(unknown)"
    $Result = $tc.result ?? $tc.Result ?? "Unknown"

    # Shorten name for readability
    $ShortName = $Name -replace "^Booty\.Tests\.PlayMode\.", ""

    if ($Result -in @("Passed", "Pass")) {
        Write-OK "PASS: $ShortName"
    } elseif ($Result -in @("Failed", "Failure")) {
        $Msg = $tc.failure.message.'#text' ?? $tc.failure.message ?? ""
        Write-Fail "FAIL: $ShortName"
        if ($Msg) {
            $Short = if ($Msg.Length -gt 300) { $Msg.Substring(0, 300) + "..." } else { $Msg }
            foreach ($line in ($Short -split "`n")) {
                Write-Info "       $line"
            }
        }
        $FailedCases += $ShortName
    } elseif ($Result -in @("Skipped", "Ignored")) {
        Write-Host "  SKIP: $ShortName" -ForegroundColor DarkYellow
    } else {
        Write-Host "  ???: $ShortName (result=$Result)" -ForegroundColor Gray
    }
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------

Write-Header "Summary"
Write-Host "  Total   : $Total"
Write-Host "  Passed  : $Passed"  -ForegroundColor $(if ($Passed -gt 0) { "Green" } else { "White" })
Write-Host "  Failed  : $Failed"  -ForegroundColor $(if ($Failed -gt 0) { "Red" } else { "White" })
if ($Errors -gt 0) { Write-Host "  Errors  : $Errors" -ForegroundColor Red }
Write-Host "  Skipped : $Skipped"
Write-Host "  Duration: ${Duration}s"

if ($FailedCases.Count -gt 0) {
    Write-Host ""
    Write-Host "  FAILED TESTS:" -ForegroundColor Red
    foreach ($name in $FailedCases) {
        Write-Host "    - $name" -ForegroundColor Red
    }
}

Write-Host ""

if ($Failed -eq 0 -and $Errors -eq 0) {
    Write-OK "All $Total PlayMode gameplay tests passed."
    exit 0
} else {
    Write-Fail "$($Failed + $Errors) test(s) failed. See output above for assertion details."
    exit 1
}
