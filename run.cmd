@echo off
setlocal

title X4 Save Zone Cleaner run.cmd

where dotnet >nul 2>&1
if errorlevel 1 (
    echo.
    echo ============================================================
    echo   .NET 10 SDK was not found.
    echo ============================================================
    echo.
    echo Please install the .NET 10 SDK from:
    echo https://dotnet.microsoft.com/download/dotnet/10.0
    echo.
    echo After installing it, run run.cmd again.
    echo.
    pause
    exit /b 1
)

echo.
echo Starting X4 Save Zone Cleaner...
echo.

dotnet run

if errorlevel 1 (
    echo.
    echo ============================================================
    echo   The application could not be started.
    echo ============================================================
    echo.
    echo See the error message above for more information.
    echo.
    pause
)

endlocal