# Milkpad Lua themes

Milkpad borrows MUGEN's idea of a data layer plus scripts, but keeps scripts
**safe and declarative**: a `.lua` file can describe a theme, not run arbitrary
program code. Every bundled Lua theme lives in this folder. Load one from
**View → Customize… → Load Lua script…**, then press **Apply**.

## A complete recipe

A recipe can set every colour itself, so it works without installing another
preset. This is the shape used by the bundled themes:

```lua
-- My Motif — a short description.
local accent = "#F2A33A"
local panel  = "#2A1D16"

theme.color("chrome", "#17110E")
theme.color("panel", panel)
theme.color("page", "#0F0B09")
theme.color("text", "#F5E6C8")
theme.color("muted", "#B59A72")
theme.color("border", "#5A3D27")
theme.color("accent", accent)
theme.color("editor", "#120E0B")
theme.color("editorfg", "#F3D9A4")

theme.font("Consolas", 12)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- Colour the Roll The Dice shelf from the same palette.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#35251A")
```

## Calls

```lua
theme.preset("Arcade Dojo")          -- optional starting point
theme.color("accent", "#F2A33A")     -- chrome, panel, page, text, muted,
                                     -- border, accent, editor, editorfg,
                                     -- diceaccent, dicepanel, dicecard
theme.font("Consolas", 12)           -- family and optional point size (5–200)
theme.tabs(4, true)                  -- width (1–8) and soft-tabs flag
theme.flag("wrap", true)             -- wrap, autoindent, statusbar, ontop,
                                     -- dicehours, diceuninstall
theme.dice(20, 20, 292, 55)          -- sides, cards, panel width, frame delay
```

`theme.dice(...)` is optional and takes the same ranges as the `[Dice]` section
of a `.cfg` theme: sides 2–100, cards 4–50, width 240–520 and speed 20–200.
Any argument may be omitted. Use `theme.flag("dicehours", false)` to hide the
focus-time column, or `theme.flag("diceuninstall", false)` to keep the shelf
virtual.

Simple `local` string/number/boolean variables and `--` comments are accepted.
A line may contain several calls separated by `;`. There are no loops,
functions, file access, network access or key handlers. Unknown calls, colour
names, flags and out-of-range numbers fail with the file name and line number
instead of being ignored silently.

## Bundled recipes

Every bundled `.cfg` preset has a matching `.lua` recipe, plus the built-in
Aurora and Midnight recipes and the four Fun Gang character themes (`kris.lua`,
`susie.lua`, `ralsei.lua`, `lancer.lua`). All of them are self-contained and
also colour the Roll The Dice shelf. Use them as templates for your own motifs.

Scripts change the Customize dialog's pending values; press **Apply** to keep
them. The button is controlled by `lua-scripts` in `features.cfg`.
