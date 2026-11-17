@echo off
setlocal
set "ROOT=%~dp0.."
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "STAGE=%TEMP%\milkpad-setup-payload"
set "PAYLOAD=%TEMP%\milkpad-payload.zip"

call "%ROOT%\build.cmd"
if errorlevel 1 exit /b %errorlevel%

if exist "%STAGE%" rmdir /s /q "%STAGE%"
mkdir "%STAGE%\themes" "%STAGE%\docs"

copy /y "%ROOT%\app\milkpad.exe" "%STAGE%\milkpad.exe" >nul
copy /y "%ROOT%\app\milkpad.exe.config" "%STAGE%\milkpad.exe.config" >nul
copy /y "%ROOT%\app\features.cfg" "%STAGE%\features.cfg" >nul
copy /y "%ROOT%\app\themes\*" "%STAGE%\themes\" >nul
copy /y "%ROOT%\docs\*" "%STAGE%\docs\" >nul
copy /y "%ROOT%\README.md" "%STAGE%\README.md" >nul
copy /y "%ROOT%\LICENSE" "%STAGE%\LICENSE" >nul

"%CSC%" /nologo /target:winexe /win32icon:"%ROOT%\assets\milkpad.ico" /win32manifest:"%ROOT%\assets\milkpad.manifest" /out:"%STAGE%\Uninstall-Milkpad.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll "%ROOT%\installer\Uninstaller.cs"
if errorlevel 1 goto :fail

if exist "%PAYLOAD%" del /q "%PAYLOAD%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::CreateFromDirectory('%STAGE%','%PAYLOAD%')"
if errorlevel 1 goto :fail

"%CSC%" /nologo /target:winexe /win32icon:"%ROOT%\assets\milkpad-setup.ico" /win32manifest:"%ROOT%\assets\milkpad.manifest" /resource:"%PAYLOAD%",Milkpad.Payload.zip /out:"%ROOT%\installer\Milkpad-Setup.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Core.dll "%ROOT%\installer\Setup.cs"
if errorlevel 1 goto :fail

rmdir /s /q "%STAGE%"
del /q "%PAYLOAD%"
echo Built "%ROOT%\installer\Milkpad-Setup.exe"
exit /b 0

:fail
rmdir /s /q "%STAGE%" 2>nul
del /q "%PAYLOAD%" 2>nul
echo Installer build failed.
exit /b 1
