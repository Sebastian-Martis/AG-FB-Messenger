# AG-Messenger Installer Script
# Run as Administrator for system-wide install, or as normal user for user install

param(
    [switch]$Uninstall
)

$AppName = "AG Messenger"
$ExeName = "AG-Messenger.exe"
$Publisher = "JaRoD-CENTER"
$InstallDir = "$env:LOCALAPPDATA\Programs\AGMessenger"
$StartMenuDir = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs"
$ShortcutPath = "$StartMenuDir\$AppName.lnk"
$SourceDir = Split-Path -Parent $MyInvocation.MyCommand.Path

if ($Uninstall) {
    Write-Host "Odinstalowywanie $AppName..." -ForegroundColor Yellow
    
    # Remove shortcut
    if (Test-Path $ShortcutPath) {
        Remove-Item $ShortcutPath -Force
        Write-Host "  Usunieto skrot z Menu Start" -ForegroundColor Green
    }
    
    # Remove install directory
    if (Test-Path $InstallDir) {
        Remove-Item $InstallDir -Recurse -Force
        Write-Host "  Usunieto pliki aplikacji" -ForegroundColor Green
    }
    
    Write-Host "`n$AppName zostal odinstalowany!" -ForegroundColor Green
    exit 0
}

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "   $AppName - Instalator" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Check if source files exist
$SourceExe = Join-Path $SourceDir $ExeName
if (-not (Test-Path $SourceExe)) {
    Write-Host "BLAD: Nie znaleziono $ExeName w katalogu instalatora!" -ForegroundColor Red
    Write-Host "Uruchom 'dotnet publish' przed instalacja." -ForegroundColor Yellow
    exit 1
}

# Create install directory
Write-Host "Tworzenie katalogu instalacji..." -ForegroundColor Yellow
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# Copy files
Write-Host "Kopiowanie plikow..." -ForegroundColor Yellow
Copy-Item -Path "$SourceDir\*" -Destination $InstallDir -Recurse -Force -Exclude "install.ps1"

# Create Start Menu shortcut
Write-Host "Tworzenie skrotu w Menu Start..." -ForegroundColor Yellow
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($ShortcutPath)
$Shortcut.TargetPath = Join-Path $InstallDir $ExeName
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Description = $AppName
$Shortcut.IconLocation = Join-Path $InstallDir $ExeName

# Check for icon file
$IconPath = Join-Path $InstallDir "Assets\app.ico"
if (Test-Path $IconPath) {
    $Shortcut.IconLocation = $IconPath
}

$Shortcut.Save()

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "   Instalacja zakonczona pomyslnie!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Mozesz teraz uruchomic '$AppName' z Menu Start." -ForegroundColor Cyan
Write-Host ""
Write-Host "Aby odinstalowac, uruchom: .\install.ps1 -Uninstall" -ForegroundColor Gray

# Ask to run
$response = Read-Host "Czy chcesz uruchomic $AppName teraz? (T/N)"
if ($response -eq "T" -or $response -eq "t") {
    Start-Process (Join-Path $InstallDir $ExeName)
}
