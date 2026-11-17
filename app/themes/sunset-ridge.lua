-- Sunset Ridge — a ridge at dusk, with orange light and violet shadows.
-- Complete and self-contained: no other preset is required.

local accent = "#FF8A3D"
local panel = "#33201F"

theme.color("chrome", "#1D1018")
theme.color("panel", panel)
theme.color("page", "#140B10")
theme.color("text", "#FFE9D6")
theme.color("muted", "#C09A8C")
theme.color("border", "#6B3A38")
theme.color("accent", accent)
theme.color("editor", "#1A0F13")
theme.color("editorfg", "#FFD9B8")

theme.font("Consolas", 11)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- The shelf catches the last of the sunset.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#422A28")
