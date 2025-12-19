$ErrorActionPreference = "Stop"

# Function to find Visual Studio and build with it
function Build-WithVsEnvironment {
    param(
        [string]$NativeDir,
        [string]$VcvarsPath
    )
    
    Write-Host "Found Visual Studio at: $VcvarsPath" -ForegroundColor Green
    Write-Host "Building with VS Developer environment..." -ForegroundColor Cyan
    
    # Escape paths for cmd - need to handle paths with spaces
    $nativeDirEscaped = $NativeDir -replace '"', '""'
    $vcvarsEscaped = $VcvarsPath -replace '"', '""'
    
    # Build command that runs vcvarsall.bat and then cargo build in the same cmd session
    $buildCmd = @"
@echo off
echo Initializing Visual Studio environment...
call "$vcvarsEscaped" x64
if errorlevel 1 (
    echo [ERROR] Failed to initialize Visual Studio environment
    echo Make sure Visual Studio C++ tools are installed with Windows SDK
    exit /b 1
)
echo Visual Studio environment initialized successfully
cd /d "$nativeDirEscaped"
if errorlevel 1 (
    echo [ERROR] Failed to change to directory: "$nativeDirEscaped"
    exit /b 1
)
echo.
echo Building Rust native module...
cargo build --release --target x86_64-pc-windows-msvc
exit /b %ERRORLEVEL%
"@
    
    # Write command to temp batch file
    $tempBatFile = [System.IO.Path]::GetTempFileName() -replace '\.tmp$', '.bat'
    [System.IO.File]::WriteAllText($tempBatFile, $buildCmd, [System.Text.Encoding]::ASCII)
    
    try {
        Write-Host "Executing build..." -ForegroundColor Yellow
        $process = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", "`"$tempBatFile`"" -Wait -PassThru -NoNewWindow
        $buildSuccess = $process.ExitCode -eq 0
        
        if (-not $buildSuccess) {
            Write-Host "Build failed with exit code: $($process.ExitCode)" -ForegroundColor Red
        }
        
        return $buildSuccess
    }
    catch {
        Write-Error "Failed to execute build: $_"
        return $false
    }
    finally {
        if (Test-Path $tempBatFile) {
            Remove-Item $tempBatFile -ErrorAction SilentlyContinue
        }
    }
}

# Function to find Visual Studio installation
function Find-VisualStudio {
    # Check if already in VS dev environment
    if ($env:VCINSTALLDIR -and $env:WindowsSdkDir) {
        Write-Host "Visual Studio Developer environment already initialized" -ForegroundColor Green
        return $null
    }

    Write-Host "Searching for Visual Studio installation..." -ForegroundColor Yellow

    # Try vswhere.exe first
    $vswherePaths = @(
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\Installer\vswhere.exe"
    )

    foreach ($vswhereExe in $vswherePaths) {
        if (Test-Path $vswhereExe) {
            try {
                $vsPath = & $vswhereExe -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null
                if ($vsPath -and (Test-Path $vsPath)) {
                    $vcvarsPath = Join-Path $vsPath "VC\Auxiliary\Build\vcvarsall.bat"
                    if (Test-Path $vcvarsPath) {
                        Write-Host "Found VS using vswhere: $vcvarsPath" -ForegroundColor Green
                        return $vcvarsPath
                    }
                }
            }
            catch {
                # Fall through to hardcoded paths
            }
            break
        }
    }

    # Fallback to common installation paths
    $vcvarsPaths = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvarsall.bat",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvarsall.bat",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvarsall.bat",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat",
        "${env:ProgramFiles}\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvarsall.bat"
    )

    foreach ($vcvarsPath in $vcvarsPaths) {
        if (Test-Path $vcvarsPath) {
            Write-Host "Found VS at: $vcvarsPath" -ForegroundColor Green
            return $vcvarsPath
        }
    }

    return $null
}

# Define paths
$NativeDir = $PSScriptRoot
$ProjectRoot = Split-Path $NativeDir -Parent
$UnityPackageDir = Join-Path $ProjectRoot "UnityTessera"
$PluginsDir = Join-Path $UnityPackageDir "Runtime\Plugins\x86_64"

Write-Host "`n=== Tessera Native Build Script ===" -ForegroundColor Cyan
Write-Host "Building for Unity integration`n" -ForegroundColor Cyan

# Ensure Plugins directory exists
if (-not (Test-Path $PluginsDir)) {
    Write-Host "Creating directory: $PluginsDir" -ForegroundColor Yellow
    New-Item -ItemType Directory -Force -Path $PluginsDir | Out-Null
}

# Check if VS environment is already set up, or find VS installation
$vcvarsPath = Find-VisualStudio
$buildSuccess = $false

if ($null -eq $vcvarsPath) {
    # VS environment already set up - try normal build
    Write-Host "Building Rust native module..." -ForegroundColor Cyan
    Set-Location $NativeDir
    cargo build --release --target x86_64-pc-windows-msvc
    $buildSuccess = $LASTEXITCODE -eq 0
}
else {
    # Build using VS environment
    $buildSuccess = Build-WithVsEnvironment -NativeDir $NativeDir -VcvarsPath $vcvarsPath
}

if (-not $buildSuccess) {
    Write-Host "`nBUILD FAILED!" -ForegroundColor Red
    Write-Host "Please run this script from Developer PowerShell for VS 2022" -ForegroundColor Yellow
    Write-Host "Search for 'Developer PowerShell' in Windows Start Menu`n" -ForegroundColor Yellow
    Write-Error "Build failed - see instructions above."
    exit 1
}

# Copy DLL to Unity Plugins folder
$SourceDll = Join-Path $NativeDir "target\x86_64-pc-windows-msvc\release\tessera_native.dll"

if (-not (Test-Path $SourceDll)) {
    $SourceDll = Join-Path $NativeDir "target\release\tessera_native.dll"
}

if (Test-Path $SourceDll) {
    Write-Host "`nCopying DLL to Unity Plugins..." -ForegroundColor Green
    Copy-Item -Path $SourceDll -Destination $PluginsDir -Force
    Write-Host "Build and copy successful!`n" -ForegroundColor Green
}
else {
    Write-Error "Could not find built DLL"
}
