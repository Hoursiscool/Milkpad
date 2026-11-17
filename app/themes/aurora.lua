-- Aurora — cool sky, green aurora, and quiet chrome.
-- Complete and self-contained: no other preset is required.

local accent = "#5EF2C2"
local panel = "#192636"

theme.color("chrome", "#121A26")
theme.color("panel", panel)
theme.color("page", "#0A1019")
theme.color("text", "#E1F5FF")
theme.color("muted", "#89A0B4")
theme.color("border", "#2B455C")
theme.color("accent", accent)
theme.color("editor", "#090F17")
theme.color("editorfg", "#D8F5FF")

theme.font("Consolas", 11)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- Roll The Dice gets the same night-sky treatment.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#1E3048")
