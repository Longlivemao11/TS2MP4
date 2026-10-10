@echo off
setlocal DisableDelayedExpansion
set "TSMP4_ROOT=%~dp0.."
set "TSMP4_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%TSMP4_CSC%" set "TSMP4_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%TSMP4_CSC%" (
  echo .NET Framework 4.x compiler was not found.
  goto failed
)
if not exist "%TSMP4_ROOT%\bin" mkdir "%TSMP4_ROOT%\bin"
if not exist "%TSMP4_ROOT%\bin" goto failed
"%TSMP4_CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /utf8output /out:"%TSMP4_ROOT%\bin\TS2MP4.exe" /win32manifest:"%~dp0app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll /reference:System.Core.dll "%~dp0ConversionCore.cs" "%~dp0Desktop.cs" "%~dp0AssemblyInfo.cs"
if errorlevel 1 goto failed
copy /y "%TSMP4_ROOT%\TS2MP4.exe.config" "%TSMP4_ROOT%\bin\TS2MP4.exe.config" >nul
if errorlevel 1 goto failed
echo Built bin\TS2MP4.exe successfully.
if /i not "%~1"=="/nopause" pause
exit /b 0
:failed
echo Build failed. Check .NET Framework and close TS2MP4 before building.
if /i not "%~1"=="/nopause" pause
exit /b 1
