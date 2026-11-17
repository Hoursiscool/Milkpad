-- Lancer — royal navy with a bright spade-blue accent.
-- Complete and self-contained: no other preset is required.

local accent = "#5AD7FF"
local panel = "#142842"

theme.color("chrome", "#0A1525")
theme.color("panel", panel)
theme.color("page", "#060E18")
theme.color("text", "#EAF6FF")
theme.color("muted", "#93B3D9")
theme.color("border", "#2E5C8E")
theme.color("accent", accent)
theme.color("editor", "#050C16")
theme.color("editorfg", "#DCEEFF")

theme.font("Consolas", 11)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- A navy shelf with a spade-blue edge.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#1A3350")
