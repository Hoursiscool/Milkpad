# Milkpad setup

`Milkpad-Setup.exe` is a per-user Windows setup wizard. It installs Milkpad into
`%LOCALAPPDATA%\Programs\Milkpad` by default, creates Start Menu and Desktop
shortcuts, registers an entry in **Settings → Apps → Installed apps**, and can
start Milkpad when it finishes. No administrator rights are required.

## Rebuilding

Run **`installer\build-installer.cmd`**. It:

1. Rebuilds `app\milkpad.exe` with `build.cmd`, including its manifest and `milkpad.exe.config` DPI settings.
2. Compiles `Uninstaller.cs` into `Uninstall-Milkpad.exe`.
3. Stages the app, `milkpad.exe.config`, `features.cfg`, all themes, the docs, README and licence.
4. Packs that staging folder into the embedded `Milkpad.Payload.zip`.
5. Compiles `Setup.cs` into `installer\Milkpad-Setup.exe` with the current icon
   and manifest.

The payload is embedded, so the finished setup is a single file.

## Options

```text
Milkpad-Setup.exe --target "D:\Apps\Milkpad"
Milkpad-Setup.exe --no-shortcuts
Milkpad-Setup.exe --silent --target "%LOCALAPPDATA%\Programs\Milkpad" --launch
```

- `--target <folder>` starts the wizard with another install folder.
- `--no-shortcuts` skips the Start Menu and Desktop shortcuts.
- `--silent` installs without showing the wizard.
- `--launch` starts Milkpad after a silent install.

Documents, settings, user themes and session data under `%APPDATA%\Milkpad` are
never removed by an upgrade. The bundled `Uninstall-Milkpad.exe` removes the
program and shortcuts, with an option to remove the data folder as well.
