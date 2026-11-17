-- Paper Dojo — warm paper, black ink, and a vermilion stamp.
-- Complete and self-contained: no other preset is required.

local accent = "#B33A2B"
local panel = "#FBF5E8"

theme.color("chrome", "#E8DCC5")
theme.color("panel", panel)
theme.color("page", "#F2E8D4")
theme.color("text", "#2D2924")
theme.color("muted", "#7A6E5C")
theme.color("border", "#CDBD9F")
theme.color("accent", accent)
theme.color("editor", "#FFF9ED")
theme.color("editorfg", "#302A23")

theme.font("Consolas", 12)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- The shelf is a clean sheet of paper with a vermilion edge.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#FFFDF7")
