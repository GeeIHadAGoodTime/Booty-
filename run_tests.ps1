#Requires -Version 5.1
<#
.SYNOPSIS
    Runs Unity Test Framework tests for the Booty! project in batch mode.

.DESCRIPTION
    Launches Unity in headless batch mode, executes EditMode and/or PlayMode
    tests via the Unity Test Framework, writes NUnit XML results to a temp
    file, parses the results, and prints a pass/fail summary.

.PARAMETER UnityPath
    Full path to the Unity.exe executable.
    Default: C:\Program Files\Unity\Hub\Editor\6000.0.47f1\Editor\Unity.exe
    Override this if your Unity 6 LTS is installed elsewhere.

.PARAMETER TestPlatform
    Which test suite(s) to run. Accepted values:
        EditMode  — editor-only tests (no scene loading, fastest)
        PlayMode  — tests that require the full runtime (slower, needs GPU or -nographics)
        All       — runs EditMode first, then PlayMode; exits 1 if either fails

    Default: EditMode

.EXAMPLE
    # Run with defaults (EditMode, standard Unity 6 LTS path)
    .\run_tests.ps1

.EXAMPLE
    # Specify a custom Unity path and run PlayMode tests
    .\run_tests.ps1 -UnityPath "D:\Unity\6000.0.47f1\Editor\Unity.exe" -TestPlatform PlayMode

.EXAMPLE
    # Run both suites
    .\run_tests.ps1 -TestPlatform All

.NOTES
    Exit codes:
        0  — all tests passed (or no tests found)
        1  — one or more tests failed, Unity failed to launch, or XML was unreadable

    Unity writes NUnit 3 XML.  The script uses [xml] to parse it without
    requiring any external tools.

    Log files are written next to the XML results in $env:TEMP so they
    survive a run but are cleaned up automatically by the OS over time.
#>

param(
    [string]$UnityPath    = "C:\Program Files\Unity\Hub\Editor\6000.2.13f1\Editor\Unity.exe",
    [ValidateSet("EditMode", "PlayMode", "All")]
    [string]$TestPlatform = "EditMode"
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

function Write-Step([string]$text) {
    Write-Host "[*] $text" -ForegroundColor Yellow
}

function Write-OK([string]$text) {
    Write-Host "[+] $text" -ForegroundColor Green
}

function Write-Fail([string]$text) {
    Write-Host "[-] $text" -ForegroundColor Red
}

# ---------------------------------------------------------------------------
# Validate environment
# ---------------------------------------------------------------------------

$ProjectPath = $PSScriptRoot   # run_tests.ps1 lives at project root

Write-Header "Booty! — Unity Test Runner"
Write-Step "Project path : $ProjectPath"
Write-Step "Unity path   : $UnityPath"
Write-Step "Test platform: $TestPlatform"

if (-not (Test-Path $UnityPath)) {
    Write-Fail "Unity.exe not found at: $UnityPath"
    Write-Host ""
    Write-Host "  Fix: pass the correct path via -UnityPath, e.g.:" -ForegroundColor Gray
    Write-Host '  .\run_tests.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.0.X\Editor\Unity.exe"' -ForegroundColor Gray
    Write-Host ""
    Write-Host "  To find your Unity installation, open Unity Hub → Installs → right-click → 'Show in Explorer'." -ForegroundColor Gray
    exit 1
}

if (-not (Test-Path $ProjectPath\Assets)) {
    Write-Fail "Assets folder not found — is ProjectPath correct? ($ProjectPath)"
    exit 1
}

# ---------------------------------------------------------------------------
# Run one test platform; return $true on success
# ---------------------------------------------------------------------------

function Invoke-UnityTests([string]$Platform) {
    $Timestamp  = Get-Date -Format "yyyyMMdd_HHmmss"
    $ResultsXml = Join-Path $env:TEMP "booty_test_${Platform}_${Timestamp}.xml"
    $LogFile    = Join-Path $env:TEMP "booty_test_${Platform}_${Timestamp}.log"

    Write-Step "Running $Platform tests..."
    Write-Step "  Results XML : $ResultsXml"
    Write-Step "  Unity log   : $LogFile"

    # Unity batch-mode test command
    # -batchmode       — no GUI
    # -nographics      — skip GPU initialisation (safe for EditMode; PlayMode may need a display)
    # -runTests        — activate Test Framework runner
    # -testPlatform    — EditMode or PlayMode
    # -testResults     — output NUnit XML path
    # -logFile         — write Unity editor log here
    # -quit            — exit after tests complete
    $Arguments = @(
        "-batchmode",
        "-nographics",
        "-projectPath", "`"$ProjectPath`"",
        "-runTests",
        "-testPlatform", $Platform,
        "-testResults",  "`"$ResultsXml`"",
        "-logFile",      "`"$LogFile`"",
        "-quit"
    )

    Write-Step "Launching Unity (this may take 30-120 seconds on first run)..."

    $proc = Start-Process `
        -FilePath $UnityPath `
        -ArgumentList $Arguments `
        -PassThru `
        -NoNewWindow `
        -Wait

    $UnityExitCode = $proc.ExitCode
    Write-Step "Unity exited with code $UnityExitCode"

    # Unity exits 0 even when tests fail (it records failures in the XML).
    # Non-zero exit usually means the editor failed to start or crashed.
    if ($UnityExitCode -ne 0) {
        Write-Fail "Unity process returned exit code $UnityExitCode — editor may have crashed."
        if (Test-Path $LogFile) {
            Write-Host ""
            Write-Host "  Last 30 lines of Unity log:" -ForegroundColor Gray
            Get-Content $LogFile -Tail 30 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
        }
        return $false
    }

    # ---------------------------------------------------------------------------
    # Parse NUnit XML results
    # ---------------------------------------------------------------------------

    if (-not (Test-Path $ResultsXml)) {
        Write-Fail "Results XML not created: $ResultsXml"
        Write-Host "  This usually means Unity failed to load the project or find any tests." -ForegroundColor Gray
        Write-Host "  Check the Unity log for errors: $LogFile" -ForegroundColor Gray
        return $false
    }

    try {
        [xml]$xml = Get-Content $ResultsXml -Raw
    } catch {
        Write-Fail "Failed to parse results XML: $_"
        return $false
    }

    # NUnit 3 root element is <test-run>; NUnit 2 uses <test-results>
    $TestRun = $xml.'test-run'
    if (-not $TestRun) {
        # Fallback: try NUnit 2 schema
        $TestRun = $xml.'test-results'
    }

    if (-not $TestRun) {
        Write-Fail "Unexpected XML schema — cannot find <test-run> or <test-results> element."
        return $false
    }

    # Attribute names differ slightly between NUnit 2 and 3
    $Total    = [int]($TestRun.total    ?? $TestRun.total    ?? 0)
    $Passed   = [int]($TestRun.passed   ?? $TestRun.passed   ?? 0)
    $Failed   = [int]($TestRun.failed   ?? $TestRun.failed   ?? 0)
    $Skipped  = [int]($TestRun.skipped  ?? $TestRun.skipped  ?? 0)
    $Errors   = [int]($TestRun.errors   ?? 0)
    $Duration = $TestRun.duration ?? "?"

    Write-Host ""
    Write-Host "  ---- $Platform Results ----" -ForegroundColor Cyan
    Write-Host "  Total   : $Total"
    Write-Host "  Passed  : $Passed"   -ForegroundColor Green
    if ($Failed -gt 0) {
        Write-Host "  Failed  : $Failed" -ForegroundColor Red
    } else {
        Write-Host "  Failed  : $Failed"
    }
    if ($Errors -gt 0) {
        Write-Host "  Errors  : $Errors"  -ForegroundColor Red
    }
    Write-Host "  Skipped : $Skipped"
    Write-Host "  Duration: ${Duration}s"

    # Print names of failed tests
    if ($Failed -gt 0 -or $Errors -gt 0) {
        Write-Host ""
        Write-Host "  FAILED TESTS:" -ForegroundColor Red

        # XPath-style: find all test-case elements where result="Failed"
        $FailedCases = $xml.SelectNodes("//test-case[@result='Failed']")
        if ($FailedCases.Count -eq 0) {
            # NUnit 2 uses result="Failure"
            $FailedCases = $xml.SelectNodes("//test-case[@result='Failure']")
        }
        foreach ($tc in $FailedCases) {
            $Name    = $tc.fullname ?? $tc.name ?? "(unknown)"
            $Message = $tc.failure.message.'#text' ?? $tc.failure.message ?? ""
            Write-Host "    FAIL: $Name" -ForegroundColor Red
            if ($Message) {
                # Truncate long messages for readability
                $Short = if ($Message.Length -gt 200) { $Message.Substring(0,200) + "..." } else { $Message }
                Write-Host "          $Short" -ForegroundColor Gray
            }
        }
        Write-Host ""
        return $false
    }

    Write-OK "All $Total $Platform tests passed."
    return $true
}

# ---------------------------------------------------------------------------
# Main execution
# ---------------------------------------------------------------------------

$OverallSuccess = $true

if ($TestPlatform -eq "All") {
    $Platforms = @("EditMode", "PlayMode")
} else {
    $Platforms = @($TestPlatform)
}

foreach ($p in $Platforms) {
    $ok = Invoke-UnityTests -Platform $p
    if (-not $ok) {
        $OverallSuccess = $false
    }
}

# ---------------------------------------------------------------------------
# Final summary
# ---------------------------------------------------------------------------

Write-Header "Summary"

if ($OverallSuccess) {
    Write-OK "All test suites passed."
    Write-Host ""
    exit 0
} else {
    Write-Fail "One or more test suites failed. See output above for details."
    Write-Host ""
    exit 1
}
