# Milkpad themes

Every bundled theme lives in this one folder:

- **Preset themes** are `.cfg` files. They load automatically into **View →
  Customize… → Preset** at startup.
- **Lua themes** are `.lua` files. Load one with **View → Customize… → Load Lua
  script…**, then press **Apply**. The file picker starts here.

Included presets include Dark, Light, Midnight, Sepia and Aurora (built in),
plus the bundled `.cfg` themes: Amber Terminal, Arcade Dojo, Blood Moon, Forest
Shrine, Game Boy, Ice Stage, Mugen Classic, Neon Alley, Paper Dojo, Sega
Genesis, Sunset Ridge, Training Room and Vaporwave. Every bundled `.cfg` has a
matching `.lua` recipe, and every recipe is **complete and self-contained**: it
sets all nine colours, so it does not depend on another preset being installed.
The Fun Gang character recipes — `kris.lua`, `susie.lua`, `ralsei.lua` and
`lancer.lua` — are also self-contained.

You can add your own `.cfg` files here or in `%APPDATA%\Milkpad\themes`. They
appear in the Preset list after a restart. If two files share a name, the later
one is shown as `Name (2)`.

## Format

```ini
[Info]
name = My Motif
dark = 1

[Colors]
chrome = #17110E
panel = #2A1D16
page = #0F0B09
text = #F5E6C8
muted = #B59A72
border = #5A3D27
accent = #F2A33A
editor = #120E0B
editorfg = #F3D9A4

[Editor]
font = Consolas
size = 12
tab = 4
softtabs = 1

[Behavior]
wrap = 1
autoindent = 1
statusbar = 1
ontop = 0

[Dice]
dicesides = 20
dicecards = 20
dicewidth = 292
dicespeed = 55
dicehours = 1
diceuninstall = 1
diceaccent = #F2A33A
dicepanel = #2A1D16
dicecard = #35251A
```

`editor` is the page behind the text. `editorfg` is the text colour. All colour
values accept the normal `#RRGGBB` form or a named colour. `dark` is optional;
Milkpad derives the light/dark mode from the page colour anyway.

The `[Dice]` section is optional and only affects **Roll The Dice**. `dicesides`
sets the die (2–100), `dicecards` the maximum shelf size (4–50), `dicewidth` the
panel width in pixels (240–520) and `dicespeed` the animation delay in
milliseconds (20–200). `dicehours` shows or hides the focus-time column;
`diceuninstall` sets the default for **Run the matching uninstaller**.
`diceaccent`, `dicepanel` and `dicecard` colour the shelf; leave them out to
follow the current theme.

Any missing colour falls back to the built-in Dark palette. The file is
read-only to Milkpad; edit it in a text editor and restart Milkpad to load the
change. Lua recipes can set the same shelf colours with `diceaccent`,
`dicepanel` and `dicecard`; see `LUA-README.md`.
