# Milkpad for Windows

**Milkpad-Setup.exe** is the Windows installer for Milkpad 1.1.0.

## Install

1. Double-click `Milkpad-Setup.exe`.
2. Choose an install folder (the default is `%LOCALAPPDATA%\Programs\Milkpad`) and whether to create a Desktop shortcut.
3. Select **Install**. The wizard creates Start Menu shortcuts and a per-user entry in Windows **Settings → Apps → Installed apps**.

No administrator rights are required. The installer is per-user and can be removed from Apps & Features or with the **Uninstall Milkpad** Start Menu shortcut.

## Requirements

- Windows 10 or Windows 11 (64-bit or 32-bit).
- .NET Framework 4.7 or newer. Windows 10 1803+ and Windows 11 include .NET Framework 4.8, so no separate download is normally needed.

## Unsigned build

This is a locally compiled, unsigned executable. Windows SmartScreen may show a warning the first time it runs. Choose **More info → Run anyway** if you trust the file. The source is included in the repository and `build.cmd` / `installer\build-installer.cmd` reproduce the binary.

## Command-line setup

The installer also accepts:

```text
Milkpad-Setup.exe --silent --target "%LOCALAPPDATA%\Programs\Milkpad" --launch
Milkpad-Setup.exe --no-shortcuts
```

See `installer\README.md` in the repository for the full command-line options.
