@echo off
REM ===========================================================
REM Rebuild script for the Industrial Inspection app.
REM Double-click this file to rebuild after changing source code.
REM ===========================================================

echo.
echo ============================================
echo  Rebuilding Industrial Inspection App...
echo ============================================
echo.

cd /d "%~dp0"

REM Stop any running instance so the build can overwrite the EXE
taskkill /F /IM InspectionApp.exe >nul 2>&1

dotnet build InspectionApp.csproj --configuration Debug

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ============================================
    echo  BUILD FAILED — see errors above.
    echo ============================================
    pause
    exit /b 1
)

echo.
echo ============================================
echo  Build succeeded!
echo  Press any key to launch the app...
echo ============================================
pause >nul

start "" "%~dp0bin\Debug\net8.0-windows\InspectionApp.exe"
