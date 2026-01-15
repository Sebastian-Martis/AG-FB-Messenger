@echo off
echo ============================================
echo   AG Messenger - Build and Package
echo ============================================
echo.

echo [1/3] Czyszczenie poprzednich buildow...
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"
if exist "publish" rmdir /s /q "publish"

echo [2/3] Kompilacja Release...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BLAD: Kompilacja nie powiodla sie!
    pause
    exit /b 1
)

echo [3/3] Kopiowanie Assets...
if exist "Assets" xcopy /E /I /Y "Assets" "publish\Assets"

echo Kopiowanie instalatora...
copy /Y "install.ps1" "publish\install.ps1"

echo.
echo ============================================
echo   Build zakonczony pomyslnie!
echo ============================================
echo.
echo Pliki gotowe w katalogu: publish\
echo.
echo Aby zainstalowac, uruchom PowerShell jako Administrator i wpisz:
echo   cd publish
echo   .\install.ps1
echo.
pause
