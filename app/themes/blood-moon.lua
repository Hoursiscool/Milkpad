-- Blood Moon — a night-stage palette with a crimson accent.
-- Complete and self-contained: no other preset is required.

local accent = "#E13B5D"
local panel = "#26111D"

theme.color("chrome", "#160B12")
theme.color("panel", panel)
theme.color("page", "#0D070B")
theme.color("text", "#F2E3E8")
theme.color("muted", "#B58A9B")
theme.color("border", "#53253A")
theme.color("accent", accent)
theme.color("editor", "#11090E")
theme.color("editorfg", "#F3D7DF")

theme.font("Consolas", 11)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- The shelf carries the same crimson edge.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#331826")
