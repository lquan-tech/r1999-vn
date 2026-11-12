@echo off
chcp 65001 >nul
cd /d "%~dp0"
title Reverse 1999 Viet Hoa Launcher

echo ========================================================
echo   KHOI DONG REVERSE: 1999 VIET HOA LAUNCHER
echo ========================================================
echo.

if exist "%~dp0release\win-unpacked\Reverse 1999 Việt Hóa Launcher.exe" (
    echo Dang khoi chay ban da dong goi...
    start "" "%~dp0release\win-unpacked\Reverse 1999 Việt Hóa Launcher.exe"
    exit /b 0
)

if not exist "%~dp0dist\index.html" (
    echo Dang bien dich giao dien...
    call npm run build
)

echo Dang khoi chay qua Electron Engine...
call npx electron .
if errorlevel 1 (
    echo.
    echo [LOI] Khoi dong launcher that bai.
    pause
)
