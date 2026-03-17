<#
MakeBootyDebugZip.ps1
Creates a cleaned debug ZIP of the Unity project for sharing/debugging.
Place this file in the Unity project root (same folder as Assets/ and ProjectSettings/).
#>

# Make errors obvious
$ErrorActionPreference = "Stop"

# Project root = folder where this script lives
$projectRoot = $PSScriptRoot

# Output zip path (on Desktop, timestamped)
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$desktop   = [Environment]::GetFolderPath("Desktop")
$zipPath   = Join-Path $desktop "Booty_debug_$timestamp.zip"

# Temp folder for filtered copy
$tempRoot  = Join-Path $env:TEMP "BootyDebug_$timestamp"

Write-Host "Project root: $projectRoot"
Write-Host "Temp copy:    $tempRoot"
Write-Host "Output zip:   $zipPath"
Write-Host ""

# 1) Clean temp dir
if (Test-Path $tempRoot) {
    Write-Host "Removing old temp folder..."
    Remove-Item -Recurse -Force $tempRoot
}
New-Item -ItemType Directory -Path $tempRoot | Out-Null

# 2) Items we actually want to include
$itemsToInclude = @(
    "Assets",
    "ProjectSettings",
    "Packages",
    ".cursor",
    "boot.ps1",
    "MasterPRDBooty.md",
    "SubPRDVerticalSlice.md",
    "P1_README.md"
)

foreach ($item in $itemsToInclude) {
    $src = Join-Path $projectRoot $item
    if (-not (Test-Path $src)) {
        Write-Host "Skipping missing item: $item"
        continue
    }

    $dst = Join-Path $tempRoot $item
    $srcItem = Get-Item $src

    if ($srcItem.PSIsContainer) {
        # Directory → use robocopy to preserve structure
        Write-Host "Copying directory: $item"

        New-Item -ItemType Directory -Path $dst -Force | Out-Null

        robocopy $src $dst /E `
            /XD Library Temp Logs Obj Build Builds .git .vs .idea node_modules .venv .vscode `
            /XF *.exe *.dll *.pdb > $null
    }
    else {
        # Single file
        Write-Host "Copying file: $item"
        $dstDir = Split-Path $dst
        New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
        Copy-Item $src $dst -Force
    }
}

# 3) Create the zip
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host ""
Write-Host "Creating zip..."
Compress-Archive -Path (Join-Path $tempRoot '*') -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "Done."
Write-Host "Created archive:"
Write-Host "  $zipPath"
Write-Host ""
Write-Host "You can now upload that ZIP."
