@echo off
setlocal
cd /d "%~dp0"
set "EXTRA_SPT_PATH=%~1"
if not defined EXTRA_SPT_PATH set "EXTRA_SPT_PATH=C:\EFT .0.16.9.5.40743 - SPT 4.1"
dotnet build .\client\ExtraSpecialSlots.Client.csproj -c Release -p:SPTPath="%EXTRA_SPT_PATH%"
if errorlevel 1 (
  echo Build failed. Read the compiler errors above.
  pause
  exit /b 1
)
echo Built: client\bin\Release\netstandard2.1\BlackHawk-ExtraSpecialSlots.Client.dll
pause
