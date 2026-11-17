@echo off
setlocal
set "ROOT=%~dp0"
set "SRC=%ROOT%src\Program.cs"
set "OUT=%ROOT%app\milkpad.exe"
set "ICON=%ROOT%assets\milkpad.ico"
set "MANIFEST=%ROOT%assets\milkpad.manifest"
if not exist "%ROOT%app" mkdir "%ROOT%app"
copy /y "%ROOT%assets\milkpad.exe.config" "%OUT%.config" >nul
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /win32icon:"%ICON%" /win32manifest:"%MANIFEST%" /out:"%OUT%" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "%SRC%"
if errorlevel 1 exit /b %errorlevel%
echo Built "%OUT%"
