-- Amber Terminal — a warm CRT glow for long editing sessions.
-- Complete and self-contained: no other preset is required.

local accent = "#FFB000"
local panel = "#17150D"

theme.color("chrome", "#0B0B08")
theme.color("panel", panel)
theme.color("page", "#050503")
theme.color("text", "#FFD27A")
theme.color("muted", "#A88C4B")
theme.color("border", "#4C3D1C")
theme.color("accent", accent)
theme.color("editor", "#080805")
theme.color("editorfg", "#FFC24A")

theme.font("Consolas", 12)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- Roll The Dice follows the same CRT palette.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#22180E")
