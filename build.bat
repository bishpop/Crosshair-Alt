@echo off
cd /d "%~dp0"
echo Building Crosshair Alt ...
echo.
dotnet publish CrosshairAlt.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "%~dp0publish"
if errorlevel 1 goto failed

echo.
echo Done: publish\CrosshairAlt.exe
explorer "%~dp0publish"
pause
exit /b 0

:failed
echo.
echo Build failed. Check the messages above.
pause
exit /b 1
