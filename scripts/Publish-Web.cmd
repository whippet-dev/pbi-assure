@echo off
setlocal
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0Publish-Web.ps1" %*
exit /b %ERRORLEVEL%
