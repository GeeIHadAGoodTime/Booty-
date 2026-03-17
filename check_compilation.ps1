# check_compilation.ps1 — Run Unity batch compilation check
# Usage: .\check_compilation.ps1
# Returns exit code 0 on success, 1 on errors

param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.2.13f1\Editor\Unity.exe"
)

$ProjectPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$LogFile = Join-Path $env:TEMP "booty_compile_check.log"

Write-Host "Checking Unity compilation for: $ProjectPath"
Write-Host "Unity executable: $UnityPath"
Write-Host "Log file: $LogFile"

if (-not (Test-Path $UnityPath)) {
    Write-Warning "Unity not found at: $UnityPath"
    Write-Warning "Please update the UnityPath parameter or install Unity 6 LTS."
    Write-Warning "Common paths:"
    Write-Warning "  C:\Program Files\Unity\Hub\Editor\6000.0.42f1\Editor\Unity.exe"
    Write-Warning "  C:\Program Files\Unity Hub\Editor\<version>\Editor\Unity.exe"
    exit 1
}

# Run Unity in batch mode to compile
# Note: $args is a reserved PowerShell automatic variable; use $unityArgs instead.
$unityArgs = @(
    "-batchmode",
    "-nographics",
    "-projectPath", "`"$ProjectPath`"",
    "-logFile", "`"$LogFile`"",
    "-quit"
)

Write-Host "Running Unity compilation check..."
$process = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow

# Parse log for errors
if (Test-Path $LogFile) {
    $logContent = Get-Content $LogFile -Raw
    $errors = Select-String -InputObject $logContent -Pattern "error CS\d+|CompilerMessage\(Error\)|Scripts have compiler errors" -AllMatches

    if ($errors.Matches.Count -gt 0) {
        Write-Host "COMPILATION ERRORS FOUND:" -ForegroundColor Red
        $errorLines = $logContent -split "`n" | Where-Object { $_ -match "error CS\d+|error:" }
        $errorLines | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        exit 1
    }
    else {
        Write-Host "COMPILATION SUCCESS — Zero errors." -ForegroundColor Green
        exit 0
    }
}
else {
    Write-Host "Log file not found. Unity may not have run correctly." -ForegroundColor Yellow
    if ($process.ExitCode -eq 0) {
        Write-Host "Unity exited with code 0 — assuming success." -ForegroundColor Green
        exit 0
    }
    exit $process.ExitCode
}
