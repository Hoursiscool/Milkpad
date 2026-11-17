# Adding features to Milkpad

Milkpad keeps optional behavior in a tiny feature registry, so a new feature does
not need a new boolean threaded through the settings loader, Customize dialog,
menus and paint code.

## 1. Register the feature

In `src/Program.cs`, add one line to `LoadFeatureFlags()`:

```csharp
RegisterFeature(
    "my-feature",                 // key used in features.cfg
    "My feature",                 // human-readable name
    "What it changes",            // short description
    false);                       // default when no config exists
```

## 2. Read the flag where it matters

```csharp
if (FeatureEnabled("my-feature"))
{
    // add the menu item, panel, button or behavior
}
```

That is the whole switch. Missing keys fall back to the registered default.

## 3. Add the config key

Add it under `[Features]` in `app/features.cfg` beside `milkpad.exe`, or in
`%APPDATA%\Milkpad\features.cfg` (the user file wins):

```ini
[Features]
lua-scripts = on
my-feature = off
```

Restart Milkpad after changing a flag. `features.cfg` is read before themes and
settings, so startup behavior is deterministic.

## Adding a preset

- **Basic preset:** add one `UiPalette` entry to the `Palettes` list in
  `src/Program.cs`. The Customize dialog, light/dark detection, settings save and
  reset behavior all use the same data shape.
- **Drop-in preset:** copy a `.cfg` into `app/themes/`, edit its `[Info]`,
  `[Colors]`, `[Editor]`, optional `[Dice]` and `[Behavior]` sections, then restart.
  The loader adds it to the Preset list automatically; no feature flag is needed.
  A matching `.lua` recipe can set the same values through the script API.

## Adding a customization control

The Customize dialog is one method (`ShowCustomize`). Add the control there and
read it in the Apply handler. For values that should survive a restart, add a
matching field and a `SaveSettings` / `LoadSettings` pair, then call it from
`ApplySettings`. Keep each new setting independent of the others so existing
`settings.txt` files continue to load. Empty `caretColor` / `selectionColor` values
mean the native caret and native selection appearance; setting either one opts into
the custom overlay while leaving document formatting untouched.

The current layout settings are a template: `pageMargin`, `windowOpacity`,
`showMenu` and `showToolbar` each have a field, a settings key, a control in the
**Layout** section, and one line in `ApplySettings`. The **Interface** section uses
the same pattern for `uiFontName`, `caretColor` and `selectionColor`. UI font is
applied recursively by `ApplyUiFont`; caret and selection colors use the overlay
panels `caretBar` and `selectionOutline`. The **ROLL THE DICE** section follows the
same pattern: `breakSides`, `breakCards`, `breakPanelWidth`, `breakRollSpeed` and
`breakShowHours` live beside `breakAccent`, `breakPanelTint` and `breakCardTint`, and
`ApplyDiceSettings()` pushes all of them onto the shelf.

## Adding a theme installer

A bundled installer theme can live outside the normal scan path, so it does not
appear in the Preset list until installed. Put its `.cfg` in a folder such as
`app/themes/installer/`, read it with `ReadThemeFile`, copy it into
`%APPDATA%\Milkpad\themes`, and apply it with `ApplyPalettePackage`. The Sega
Genesis installer is the working example: `SegaGenesisSource()` looks in
`app/themes/installer/` first and falls back to the bundled `app/themes/` recipe.
