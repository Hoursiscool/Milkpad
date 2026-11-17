-- Mugen Classic — charcoal panels and warm yellow highlights.
-- Complete and self-contained: no other preset is required.

local accent = "#FFD34E"
local panel = "#232323"

theme.color("chrome", "#121212")
theme.color("panel", panel)
theme.color("page", "#0A0A0A")
theme.color("text", "#F0F0F0")
theme.color("muted", "#A0A0A0")
theme.color("border", "#444444")
theme.color("accent", accent)
theme.color("editor", "#0D0D0D")
theme.color("editorfg", "#F4F4F4")

theme.font("Consolas", 11)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- The shelf uses the same charcoal and yellow.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#2C2C2C")
