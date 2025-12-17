@echo off
set PATH=C:\Program Files\nodejs;%PATH%
cd /d "d:\AntiGravity\WIN\AG-Facebook-Messenger"

echo === Cleaning old node_modules ===
rmdir /s /q node_modules 2>nul
del /q package-lock.json 2>nul

echo === Installing dependencies ===
call npm install
if errorlevel 1 (
    echo npm install FAILED
    pause
    exit /b 1
)

echo === Building application ===
call npm run make
if errorlevel 1 (
    echo npm run make FAILED
    pause
    exit /b 1
)

echo === BUILD SUCCESSFUL ===
echo Installer location: out\make\squirrel.windows\x64\J-Connect-Setup.exe
pause
