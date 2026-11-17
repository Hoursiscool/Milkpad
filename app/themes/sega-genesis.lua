-- Sega Genesis — black hardware, Sega blue, and white lettering.
-- Complete and self-contained: no other preset is required.

local accent = "#1E90FF"
local panel = "#101820"

theme.color("chrome", "#05070A")
theme.color("panel", panel)
theme.color("page", "#000000")
theme.color("text", "#F2F6FA")
theme.color("muted", "#8A9BAA")
theme.color("border", "#1E5FA8")
theme.color("accent", accent)
theme.color("editor", "#000000")
theme.color("editorfg", "#F5F5F5")

theme.font("Consolas", 11)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- 16-bit blue carries into the program shelf.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#0B1420")
