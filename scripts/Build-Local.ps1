<#
.SYNOPSIS
    Builds a local qbPortWeaver MSI installer for testing purposes.

.DESCRIPTION
    Mirrors the CI build-release workflow locally:
      1. Publishes the .NET app as a self-contained single-file win-x64 executable
      2. Builds the MSI installer using WiX Toolset v4

    Use this script before building the MSI locally — a regular Visual Studio
    Release build does NOT produce the self-contained single-file output that
    the WiX source expects under bin\Release\net10.0-windows\win-x64\publish\.

    WiX Toolset v4 must be installed as a .NET global tool:
      dotnet tool install --global wix
      wix extension add WixToolset.UI.wixext WixToolset.Util.wixext

.PARAMETER Version
    The version string to stamp into the build (e.g. '2.3.0').
    Defaults to the version defined in AppConstants.cs.

.EXAMPLE
    # Build using the default version from AppConstants.cs
    .\scripts\Build-Local.ps1

.EXAMPLE
    # Build with an explicit version override
    .\scripts\Build-Local.ps1 -Version 2.3.0
#>

[CmdletBinding()]
param(
    [string] $Version = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

function Write-Step([string]$msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok([string]$msg)   { Write-Host "    $msg"   -ForegroundColor Green }

# ---------------------------------------------------------------------------
# Step 1: Resolve version — read from AppConstants.cs if not provided
# ---------------------------------------------------------------------------
Write-Step 'Resolving version...'

if (-not $Version) {
    $constantsPath = Join-Path $repoRoot 'AppConstants.cs'
    $match = Select-String -Path $constantsPath -Pattern 'APP_VERSION\s*=\s*"([^"]+)"'
    if (-not $match) {
        Write-Error "Could not find APP_VERSION in AppConstants.cs. Pass -Version explicitly."
        exit 1
    }
    $Version = $match.Matches[0].Groups[1].Value
}

Write-Ok "Version : $Version"

# ---------------------------------------------------------------------------
# Step 2: Publish as self-contained single-file win-x64
#         This matches the CI build-release.yml publish step exactly.
#         Output lands in: bin\Release\net10.0-windows\win-x64\publish\
# ---------------------------------------------------------------------------
Write-Step 'Publishing self-contained single-file executable...'

Push-Location $repoRoot
try {
    dotnet publish qbPortWeaver.csproj `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:Version=$Version `
        -p:FileVersion="$Version.0" `
        -p:AssemblyVersion="$Version.0"

    if ($LASTEXITCODE -ne 0) { Write-Error 'dotnet publish failed.'; exit 1 }
} finally {
    Pop-Location
}

$publishedExe = Join-Path $repoRoot "bin\Release\net10.0-windows\win-x64\publish\qbPortWeaver.exe"
if (-not (Test-Path $publishedExe)) {
    Write-Error "Expected publish output not found: $publishedExe"
    exit 1
}

Write-Ok "Published : $publishedExe"

# ---------------------------------------------------------------------------
# Step 3: Build the MSI installer using WiX Toolset v4
#         Output: installer\qbPortWeaver_{version}_Setup.msi
# ---------------------------------------------------------------------------
Write-Step 'Building MSI installer with WiX Toolset v4...'

# Ensure WiX is installed
if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    Write-Host '    Installing WiX Toolset v4...' -ForegroundColor Yellow
    dotnet tool install --global wix
    if ($LASTEXITCODE -ne 0) { Write-Error 'Failed to install WiX Toolset.'; exit 1 }
}

# Install required extensions (safe to run if already present)
wix extension add WixToolset.UI.wixext WixToolset.Util.wixext 2>&1 | Out-Null

$wxsFile  = Join-Path $repoRoot 'installer\qbPortWeaver.wxs'
$setupMsi = Join-Path $repoRoot "installer\qbPortWeaver_${Version}_Setup.msi"

wix build $wxsFile `
    -ext WixToolset.UI.wixext `
    -ext WixToolset.Util.wixext `
    -d ProductVersion=$Version `
    -out $setupMsi

if ($LASTEXITCODE -ne 0) { Write-Error 'WiX build failed.'; exit 1 }

if (-not (Test-Path $setupMsi)) {
    Write-Error "Expected installer not found: $setupMsi"
    exit 1
}

Write-Ok "Installer : $setupMsi"
Write-Host "`nDone." -ForegroundColor Green
