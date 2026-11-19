@echo off
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0TrayBattery.ps1"
echo.
echo Log: %LOCALAPPDATA%\MCHOSE_X9_BatteryTray\tray-v9.log
pause
