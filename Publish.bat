@echo off
REM ===========================================================
REM Publish script for the Industrial Inspection app.
REM Creates a portable, self-contained folder you can copy to
REM any Windows PC — no .NET installation required there.
REM ===========================================================

echo.
echo =====================================================
echo  Publishing Industrial Inspection App (self-contained)
echo =====================================================
echo.

cd /d "%~dp0"

REM Stop any running instance so the publish can overwrite files
taskkill /F /IM InspectionApp.exe >nul 2>&1

REM Clean previous publish output
if exist "Publish" rmdir /s /q "Publish"

dotnet publish InspectionApp.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=false ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o "Publish"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo =====================================================
    echo  PUBLISH FAILED  see errors above.
    echo =====================================================
    pause
    exit /b 1
)

echo.
echo =====================================================
echo  Publish succeeded!
echo.
echo  Portable app folder:
echo    %~dp0Publish
echo.
echo  How to deploy:
echo    1. Right-click the "Publish" folder ^> Send to ^> Compressed (zipped) folder
echo    2. Copy the .zip to the other PC (USB, network, email)
echo    3. Unzip anywhere on the other PC
echo    4. Double-click InspectionApp.exe inside the unzipped folder
echo.
echo  Press any key to open the Publish folder in Explorer...
echo =====================================================
pause >nul

start "" "%~dp0Publish"
