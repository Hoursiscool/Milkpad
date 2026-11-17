-- Forest Shrine — moss, old wood, and a single warm lantern.
-- Complete and self-contained: no other preset is required.

local accent = "#D4A94E"
local panel = "#1B2A20"

theme.color("chrome", "#0E1712")
theme.color("panel", panel)
theme.color("page", "#0A120D")
theme.color("text", "#E3F0E2")
theme.color("muted", "#91A995")
theme.color("border", "#35543D")
theme.color("accent", accent)
theme.color("editor", "#0D1810")
theme.color("editorfg", "#D8EAD6")

theme.font("Consolas", 11)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- Lantern light reaches the program shelf too.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#24382B")
