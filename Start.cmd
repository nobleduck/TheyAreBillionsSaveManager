@echo off
set "app=%~dp0dist\TheyAreBillionsSaveManager.exe"
if not exist "%app%" (
    echo Please run build.ps1 to build the application first.
    pause
    exit /b 1
)
start "" "%app%"
