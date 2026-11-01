@echo off
rem Builds the companion with the C# compiler that ships with Windows (.NET Framework 4).
rem No Visual Studio or .NET SDK needed. AuxHeadUnit.exe is the free companion;
rem AuxHeadUnitPro.exe is built too when Pro.cs (not public) is here.
setlocal
cd /d "%~dp0"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set WM=%WINDIR%\System32\WinMetadata

call :build AuxHeadUnit.exe
if errorlevel 1 exit /b 1
if exist Pro.cs (
  call :build AuxHeadUnitPro.exe Pro.cs
  if errorlevel 1 exit /b 1
)
exit /b 0

:build
"%FW%\csc.exe" -nologo -optimize -platform:x64 -out:%1 ^
  -r:"%WM%\Windows.Foundation.winmd" -r:"%WM%\Windows.Media.winmd" ^
  -r:"%WM%\Windows.Devices.winmd" -r:"%WM%\Windows.Storage.winmd" ^
  -r:"%FW%\System.Runtime.dll" -r:"%FW%\System.Runtime.InteropServices.WindowsRuntime.dll" ^
  -r:System.Web.Extensions.dll -r:System.Drawing.dll ^
  Program.cs Media.cs Bluetooth.cs CoreAudio.cs %2
if errorlevel 1 (
  echo Build of %1 failed.
  exit /b 1
)
echo Built %1
exit /b 0
