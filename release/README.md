# Milkpad 1.1.0 release

| Folder | Platform | Contents |
| --- | --- | --- |
| `windows/` | Windows 10/11 | `Milkpad-Setup.exe` and Windows install notes |
| `macos/` | macOS | Porting notes; no native installer is possible from this WinForms codebase |

Start with **`windows/Milkpad-Setup.exe`** for the supported build. It is a per-user installer and needs no administrator rights. See `windows/README.md` for requirements and SmartScreen information.

The macOS folder deliberately contains no `.exe` or `.app`: Milkpad is a Windows Forms / .NET Framework application, so a macOS build requires a native port rather than a renamed Windows binary. See `macos/README.md` for the options.
