@echo off
setlocal
node "%~dp0Publish-Web.mjs" %*
exit /b %ERRORLEVEL%
