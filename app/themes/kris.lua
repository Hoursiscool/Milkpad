-- Kris — midnight blue with a bright cyan heart.
-- Complete and self-contained: no other preset is required.

local accent = "#5CC8FF"
local panel = "#13243A"

theme.color("chrome", "#0B1424")
theme.color("panel", panel)
theme.color("page", "#08101B")
theme.color("text", "#EAF4FF")
theme.color("muted", "#8FB4D9")
theme.color("border", "#2F5C8A")
theme.color("accent", accent)
theme.color("editor", "#071019")
theme.color("editorfg", "#DCEEFF")

theme.font("Consolas", 11)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- Roll The Dice keeps the same midnight-blue frame.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#15283F")
