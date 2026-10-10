@echo off
setlocal DisableDelayedExpansion
chcp 65001 >nul
title TS2MP4 启动与诊断
set "TSMP4_STATE=%LOCALAPPDATA%\TS2MP4"
set "TSMP4_APP=%LOCALAPPDATA%\TS2MP4\app-v1.0.2"
set "TSMP4_LOG=%LOCALAPPDATA%\TS2MP4\startup-latest.txt"
if not exist "%TSMP4_APP%" mkdir "%TSMP4_APP%"
if not exist "%TSMP4_APP%" (
  echo 无法创建本地程序目录。请确认当前用户目录可写。
  pause
  exit /b 1
)
> "%TSMP4_LOG%" echo TS2MP4 1.0.2 启动记录
set "TSMP4_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%TSMP4_CSC%" set "TSMP4_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%TSMP4_CSC%" (
  >> "%TSMP4_LOG%" echo 未找到系统 .NET Framework 4.x 编译器。
  goto failed
)
echo 正在用本机 Windows 编译器生成桌面程序…
"%TSMP4_CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /utf8output /out:"%TSMP4_APP%\TS2MP4.exe" /win32manifest:"%~dp0source\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll /reference:System.Core.dll "%~dp0source\ConversionCore.cs" "%~dp0source\Desktop.cs" "%~dp0source\AssemblyInfo.cs" >> "%TSMP4_LOG%" 2>&1
if errorlevel 1 goto failed
copy /y "%~dp0TS2MP4.exe.config" "%TSMP4_APP%\TS2MP4.exe.config" >nul
if errorlevel 1 (
  >> "%TSMP4_LOG%" echo 无法复制启动配置。
  goto failed
)
if exist "%~dp0ffmpeg.exe" copy /y "%~dp0ffmpeg.exe" "%TSMP4_APP%\ffmpeg.exe" >nul
if exist "%~dp0ffprobe.exe" copy /y "%~dp0ffprobe.exe" "%TSMP4_APP%\ffprobe.exe" >nul
echo 正在启动…此窗口会在工具正常关闭后自动退出。
>> "%TSMP4_LOG%" echo 编译成功，正在启动程序。
start "" /wait "%TSMP4_APP%\TS2MP4.exe"
set "TSMP4_EXIT=%ERRORLEVEL%"
>> "%TSMP4_LOG%" echo 程序退出码：%TSMP4_EXIT%
if not "%TSMP4_EXIT%"=="0" goto failed
exit /b 0

:failed
echo.
echo 启动没有成功。以下记录可用于排查：
type "%TSMP4_LOG%"
if exist "%TSMP4_STATE%\startup-runtime.txt" (
  echo.
  echo 程序运行记录：
  type "%TSMP4_STATE%\startup-runtime.txt"
)
echo.
echo 报告问题时请提供已脱敏的错误内容和 startup-latest.txt。
echo 日志目录：%TSMP4_STATE%
pause
exit /b 1
