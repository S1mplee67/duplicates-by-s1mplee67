@echo off
cd /d "%~dp0"
if not exist "Duplicates-by-S1mplee67.exe" (
    call build.bat
)
start "" "Duplicates-by-S1mplee67.exe"
