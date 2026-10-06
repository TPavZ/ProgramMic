@echo off
setlocal
set "PROGRAMMIC_DEV_UI=%~dp0UI"
dotnet build "%~dp0ProgramMic.csproj" -c Release
if errorlevel 1 (
  pause
  exit /b 1
)
start "" "%~dp0bin\Release\net9.0-windows\ProgramMic.exe"
