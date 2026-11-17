-- Neon Alley — neon signs, wet pavement, and a fighting-game city at night.
-- Complete and self-contained: no other preset is required.

local accent = "#FF3CB4"
local panel = "#1D1230"

theme.color("chrome", "#100A18")
theme.color("panel", panel)
theme.color("page", "#09060F")
theme.color("text", "#E9F7FF")
theme.color("muted", "#9B8FB8")
theme.color("border", "#4E2C68")
theme.color("accent", accent)
theme.color("editor", "#0E0817")
theme.color("editorfg", "#7CF7FF")

theme.font("Consolas", 11)
theme.tabs(2, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- The shelf picks up the magenta sign light.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#26183D")
