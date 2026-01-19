@echo off
REM ==========================================================================
REM MaintenanceAR Automated Builder Script
REM Place this script in the maquette data folder and run it to build the APK
REM ==========================================================================

setlocal enabledelayedexpansion

REM Get the directory where this script is located (the maquette folder)
set "MAQUETTE_PATH=%~dp0"
REM Remove trailing backslash
if "%MAQUETTE_PATH:~-1%"=="\" set "MAQUETTE_PATH=%MAQUETTE_PATH:~0,-1%"

REM Get the maquette name from the folder name
for %%F in ("%MAQUETTE_PATH%") do set "MAQUETTE_NAME=%%~nxF"

REM Unity project path
set "UNITY_PROJECT=C:\Users\nejiy\Documents\Development\UnityAR\MaintenanceRA"

REM Output directory for APKs
set "OUTPUT_DIR=C:\Users\nejiy\Documents\AR APPS"

REM Unity Editor path - adjust this to your Unity version
set "UNITY_PATH=C:\Program Files\Unity\Hub\Editor\6000.2.6f2\Editor\Unity.exe"

echo ==========================================================================
echo MaintenanceAR Automated Builder
echo ==========================================================================
echo.
echo Maquette Path: %MAQUETTE_PATH%
echo Maquette Name: %MAQUETTE_NAME%
echo Unity Project: %UNITY_PROJECT%
echo Output Dir:    %OUTPUT_DIR%
echo.

REM Check if Unity exists
if not exist "%UNITY_PATH%" (
    echo [ERROR] Unity Editor not found at: %UNITY_PATH%
    echo Please update the UNITY_PATH variable in this script.
    pause
    exit /b 1
)

REM Check if Unity is already running
tasklist /FI "IMAGENAME eq Unity.exe" 2>NUL | find /I /N "Unity.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [ERROR] Unity Editor is currently running.
    echo         You MUST close the Unity Editor before running this build script.
    echo         The build process requires exclusive access to the project.
    echo.
    echo         Please close Unity and try again.
    echo.
    pause
    exit /b 1
)

REM Check if required files exist
if not exist "%MAQUETTE_PATH%\Data.json" (
    echo [ERROR] Data.json not found in %MAQUETTE_PATH%
    pause
    exit /b 1
)

echo [1/3] Checking maquette data...
echo       - Data.json: Found

REM Check for GLB file
set "GLB_FOUND=0"
for %%G in ("%MAQUETTE_PATH%\ModelInfos\3DMODEL\*.glb") do (
    echo       - GLB Model: %%~nxG
    set "GLB_FOUND=1"
)
if "%GLB_FOUND%"=="0" (
    echo [ERROR] No .glb file found in %MAQUETTE_PATH%\ModelInfos\3DMODEL\
    pause
    exit /b 1
)

REM Check for QR code
set "QR_FOUND=0"
for %%Q in ("%MAQUETTE_PATH%\ModelInfos\QRCode\QRCode.*") do (
    echo       - QR Code: %%~nxQ
    set "QR_FOUND=1"
)
if "%QR_FOUND%"=="0" (
    echo [ERROR] No QRCode.* file found in %MAQUETTE_PATH%\ModelInfos\QRCode\
    pause
    exit /b 1
)

REM Ensure output directory exists
if not exist "%OUTPUT_DIR%" (
    echo [2/3] Creating output directory...
    mkdir "%OUTPUT_DIR%"
)

echo.
echo [2/3] Starting Unity build process...
echo       This may take several minutes. Please wait...
echo.

REM Run Unity in batch mode
"%UNITY_PATH%" -batchmode -quit -projectPath "%UNITY_PROJECT%" -executeMethod AutomatedBuilder.BuildFromCommandLine -maquettePath "%MAQUETTE_PATH%" -logFile "%OUTPUT_DIR%\build_log_%MAQUETTE_NAME%.txt"

REM Check build result
if %ERRORLEVEL% EQU 0 (
    echo.
    echo [3/3] Build completed successfully!
    
    REM Move the APK to output directory
    if exist "%UNITY_PROJECT%\%MAQUETTE_NAME%.apk" (
        echo       Moving APK to output directory...
        move /Y "%UNITY_PROJECT%\%MAQUETTE_NAME%.apk" "%OUTPUT_DIR%\%MAQUETTE_NAME%.apk"
        echo.
        echo ==========================================================================
        echo BUILD SUCCESSFUL!
        echo APK Location: %OUTPUT_DIR%\%MAQUETTE_NAME%.apk
        echo ==========================================================================
    ) else (
        REM Try without spaces in name
        set "APK_NAME_NOSPACE=%MAQUETTE_NAME: =%"
        if exist "%UNITY_PROJECT%\!APK_NAME_NOSPACE!.apk" (
            move /Y "%UNITY_PROJECT%\!APK_NAME_NOSPACE!.apk" "%OUTPUT_DIR%\%MAQUETTE_NAME%.apk"
            echo.
            echo ==========================================================================
            echo BUILD SUCCESSFUL!
            echo APK Location: %OUTPUT_DIR%\%MAQUETTE_NAME%.apk
            echo ==========================================================================
        ) else (
            echo [WARNING] APK file not found at expected location.
            echo           Check the build log for more details.
            echo           Log: %OUTPUT_DIR%\build_log_%MAQUETTE_NAME%.txt
        )
    )
) else (
    echo.
    echo [ERROR] Build failed with error code: %ERRORLEVEL%
    echo         Check the build log for details:
    echo         %OUTPUT_DIR%\build_log_%MAQUETTE_NAME%.txt
)

echo.
pause
