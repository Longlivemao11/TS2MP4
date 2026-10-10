@echo off
call "%~dp0source\Build-Windows.cmd" %*
exit /b %ERRORLEVEL%
