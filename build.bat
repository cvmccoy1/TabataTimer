@echo off
echo ========================================
echo  Tabata Timer - Build Script
echo ========================================
echo.

REM Check for dotnet
where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: .NET SDK not found.
    echo Please install .NET 8 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Restoring packages...
dotnet restore TabataTimer\TabataTimer.csproj
if %errorlevel% neq 0 ( echo Restore failed. & pause & exit /b 1 )

echo.
echo Building and publishing single-file executable...
dotnet publish TabataTimer\TabataTimer.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o .\publish

if %errorlevel% neq 0 ( echo Build failed. & pause & exit /b 1 )

echo.
echo ========================================
echo  BUILD SUCCESSFUL!
echo  Output: .\publish\TabataTimer.exe
echo ========================================
echo.
pause
