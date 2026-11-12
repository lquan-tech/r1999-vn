@echo off
chcp 65001 >nul
title Build Installer - Reverse: 1999 Viet Hoa Launcher
cd /d "%~dp0"

echo ========================================================
echo   DONG GOI FILE CAI DAT (.EXE) CHO LAUNCHER VIET HOA
echo ========================================================
echo.

echo [1/4] Bien dich bo khoi chay game dac quyen (game_starter.exe)...
if not exist "%~dp0resources" mkdir "%~dp0resources"
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /optimize+ /platform:anycpu /out:"%~dp0resources\game_starter.exe" "%~dp0src_helper\GameStarter.cs" >nul
copy /y "%~dp0resources\game_starter.exe" "%~dp0public\game_starter.exe" >nul
copy /y "%~dp0resources\game_starter.exe" "%~dp0build\game_starter.exe" >nul

echo.
echo [2/4] Cap nhat icon cho Electron Engine...
call node patch_electron_exe.js

echo.
echo [3/4] Dang bien dich ma nguon Vite va Electron...
call npm run build
if errorlevel 1 (
    echo [LOI] Bien dich ma nguon that bai.
    pause
    exit /b 1
)

echo.
echo [4/4] Dang dong goi bo cai dat NSIS Installer va thu muc Release...
call npx electron-builder --win nsis dir
if errorlevel 1 (
    echo.
    echo [LOI] Dong goi that bai. Vui long kiem tra lai thong bao loi o tren.
) else (
    echo.
    echo ========================================================
    echo  DONG GOI THANH CONG!
    echo  - File cai dat: %~dp0release\Reverse 1999 Viet Hoa Launcher Setup 1.0.0.exe
    echo  - Ban chay truc tiep: %~dp0release\win-unpacked\Reverse 1999 Việt Hóa Launcher.exe
    echo ========================================================
    explorer "%~dp0release"
)

echo.
pause
