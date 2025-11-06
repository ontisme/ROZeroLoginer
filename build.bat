@echo off
chcp 65001 >nul
echo 開始編譯 ROZeroLoginer...
echo.

set MSBUILD="C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"

if not exist %MSBUILD% (
    echo 錯誤: 找不到 MSBuild.exe
    echo 路徑: %MSBUILD%
    pause
    exit /b 1
)

echo 使用 MSBuild: %MSBUILD%
echo.

%MSBUILD% ROZeroLoginer.sln /p:Configuration=Debug /p:Platform="Any CPU" /t:Rebuild /verbosity:minimal

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================
    echo 編譯成功!
    echo ============================================
    echo.
) else (
    echo.
    echo ============================================
    echo 編譯失敗! 錯誤代碼: %ERRORLEVEL%
    echo ============================================
    echo.
)

pause
