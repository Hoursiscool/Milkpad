-- Ice Stage — blue glass and a sharp cyan highlight.
-- Complete and self-contained: no other preset is required.

local accent = "#58D6FF"
local panel = "#142A3A"

theme.color("chrome", "#0C1822")
theme.color("panel", panel)
theme.color("page", "#08121A")
theme.color("text", "#E9F8FF")
theme.color("muted", "#8FB2C4")
theme.color("border", "#2E5870")
theme.color("accent", accent)
theme.color("editor", "#0B1923")
theme.color("editorfg", "#DFF6FF")

theme.font("Consolas", 11)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- The shelf sits on the same frozen glass.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#1B3648")
