-- Game Boy — four tones, no distractions.
-- Complete and self-contained: no other preset is required.

local accent = "#9BBC0F"
local panel = "#1B2B17"

theme.color("chrome", "#0F1A0D")
theme.color("panel", panel)
theme.color("page", "#0B1309")
theme.color("text", "#C7E8B0")
theme.color("muted", "#7E9C68")
theme.color("border", "#36502B")
theme.color("accent", accent)
theme.color("editor", "#0E1A0A")
theme.color("editorfg", accent)

theme.font("Consolas", 11)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- Keeps the shelf on the same four-tone screen.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#243A1D")
