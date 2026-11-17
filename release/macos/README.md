# Milkpad for macOS

There is **no macOS `.exe` or `.app` installer in this folder**, and there cannot be one from the current codebase.

Milkpad is a native **Windows Forms** application built on **.NET Framework 4.7/4.8** and compiled with the Windows C# compiler. Windows Forms and the .NET Framework runtime do not exist on macOS, and a Windows `.exe` cannot run natively on macOS. Shipping a renamed or wrapped Windows binary here would be a broken download, so this release intentionally contains only this note.

## Options

1. **Run the Windows release on a Mac with a Windows environment.** Use Parallels, VMware Fusion, UTM with a Windows arm64/Intel VM, or a Windows cloud PC, then install `release/windows/Milkpad-Setup.exe` there. This is the only way to run the current Milkpad unchanged.
2. **Port Milkpad to a cross-platform UI toolkit.** A real macOS build needs the UI moved from WinForms to a toolkit such as Avalonia UI or .NET MAUI, with macOS packaging, menu conventions, file dialogs, fonts, permissions and code signing handled separately. That is a porting project, not a rebuild of the existing `.exe`.
3. **Use the source as the starting point.** All application code is in `src/Program.cs`; the Windows build scripts document the assets, manifest and installer payload. A port would keep the local-first behaviour, document model, text tools and milkBall table while replacing the WinForms shell.

The Windows build remains the supported and tested release.
