-- Arcade Dojo — cabinet wood, warm bulbs, and a persistent score line.
-- Complete and self-contained: no other preset is required.

local accent = "#F2A33A"
local panel = "#2A1D16"

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

-- The program shelf becomes part of the cabinet.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#35251A")
