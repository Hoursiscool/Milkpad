-- Midnight — a calm, high-contrast editor for late sessions.
-- Complete and self-contained: no other preset is required.

local accent = "#58A6FF"
local panel = "#161B22"

theme.color("chrome", "#0B0E13")
theme.color("panel", panel)
theme.color("page", "#0E1116")
theme.color("text", "#E6EDF3")
theme.color("muted", "#8B949E")
theme.color("border", "#30363D")
theme.color("accent", accent)
theme.color("editor", "#0D1117")
theme.color("editorfg", "#E6EDF3")

theme.font("Consolas", 11)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- Roll The Dice follows the same quiet contrast.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#1C222B")
