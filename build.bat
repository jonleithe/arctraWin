@echo off
setlocal

if not "%~2"=="" goto invalid
if /i "%~1"=="/?" goto help
if /i "%~1"=="--help" goto help
if /i "%~1"=="-h" goto help

set "configuration=%~1"
if not defined configuration set "configuration=Debug"
if /i "%configuration%"=="debug" goto build
if /i "%configuration%"=="release" goto build
if /i "%configuration%"=="all" goto build
goto invalid

:build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build.ps1" -Configuration "%configuration%"
exit /b %errorlevel%

:invalid
echo Invalid build option. 1>&2
echo Usage: build.bat [all^|release^|debug] 1>&2
exit /b 2

:help
echo Usage: build.bat [all^|release^|debug]
echo Default: debug.  all builds both Debug and Release.
echo Close ArctraWin before building.
exit /b 0
