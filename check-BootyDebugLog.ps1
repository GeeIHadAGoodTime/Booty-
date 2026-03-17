# Check-BootyDebugLog.ps1
# Run from repo root: cd into the Booty! folder then: .\Check-BootyDebugLog.ps1

$ProjectRoot = Get-Location

Write-Host "Scanning C# files under: $ProjectRoot" -ForegroundColor Cyan

# Grab all .cs files
$csFiles = Get-ChildItem -Path $ProjectRoot -Recurse -Filter *.cs -File
if (-not $csFiles) {
    Write-Host "No .cs files found under $ProjectRoot. Check your path." -ForegroundColor Red
    exit 1
}

function Show-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "======== $Title ========" -ForegroundColor Yellow
}

# 1) Look for namespace definitions
Show-Section "Definitions of namespace Booty.Debug"
$nsMatches = Select-String -Path $csFiles.FullName -Pattern 'namespace\s+Booty\.Debug' -ErrorAction SilentlyContinue
if ($nsMatches) {
    $nsMatches | Select-Object Path, LineNumber, Line | Format-Table -AutoSize
} else {
    Write-Host "NONE" -ForegroundColor Red
}

# 2) Look for Log / LogError / LogWarning class definitions
Show-Section "Class definitions named Log / LogError / LogWarning"
$logClassMatches       = Select-String -Path $csFiles.FullName -Pattern 'class\s+Log\b'        -ErrorAction SilentlyContinue
$logErrorClassMatches  = Select-String -Path $csFiles.FullName -Pattern 'class\s+LogError\b'   -ErrorAction SilentlyContinue
$logWarnClassMatches   = Select-String -Path $csFiles.FullName -Pattern 'class\s+LogWarning\b' -ErrorAction SilentlyContinue

if ($logClassMatches -or $logErrorClassMatches -or $logWarnClassMatches) {
    @($logClassMatches + $logErrorClassMatches + $logWarnClassMatches) |
        Where-Object { $_ } |
        Select-Object Path, LineNumber, Line |
        Format-Table -AutoSize
} else {
    Write-Host "NONE" -ForegroundColor Red
}

# 3) Look for call sites Booty.Debug.Log*
Show-Section "Usages of Booty.Debug.Log / LogError / LogWarning (call sites)"
$callPatterns = @(
    'Booty\.Debug\.Log\(',
    'Booty\.Debug\.LogError\(',
    'Booty\.Debug\.LogWarning\('
)

$usageMatches = foreach ($p in $callPatterns) {
    Select-String -Path $csFiles.FullName -Pattern $p -ErrorAction SilentlyContinue
}

if ($usageMatches) {
    $usageMatches |
        Select-Object Path, LineNumber, Line |
        Sort-Object Path, LineNumber |
        Format-Table -AutoSize
} else {
    Write-Host "NONE" -ForegroundColor Green
}

# 4) Quick verdict
Show-Section "Verdict"
$hasNamespace = [bool]$nsMatches
$hasLogClass  = [bool]($logClassMatches -or $logErrorClassMatches -or $logWarnClassMatches)
$hasUsages    = [bool]$usageMatches

Write-Host "Has Booty.Debug namespace: $hasNamespace"
Write-Host "Has Log/LogError/LogWarning class definitions: $hasLogClass"
Write-Host "Has Booty.Debug.Log* usages: $hasUsages"

if (-not $hasNamespace -and -not $hasLogClass -and $hasUsages) {
    Write-Host ""
    Write-Host "LIKELY STATE:" -ForegroundColor Magenta
    Write-Host "  Booty.Debug.Log* wrapper was removed/renamed, but call sites still exist." -ForegroundColor Magenta
    Write-Host "  Fix = recreate wrapper in namespace Booty.Debug OR replace all calls with UnityEngine.Debug.*" -ForegroundColor Magenta
} elseif ($hasNamespace -and -not $hasLogClass -and $hasUsages) {
    Write-Host ""
    Write-Host "Namespace Booty.Debug exists but no Log* class definitions. Check that file for class names/signatures." -ForegroundColor Magenta
} elseif ($hasNamespace -and $hasLogClass -and -not $hasUsages) {
    Write-Host ""
    Write-Host "Logging wrapper exists but is unused. Safe to delete or wire into new code." -ForegroundColor Magenta
} else {
    Write-Host ""
    Write-Host "Mixed case. Inspect the tables above for exact files/lines." -ForegroundColor Magenta
}
