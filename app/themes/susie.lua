-- Susie — bruised plum and hot magenta, all bite.
-- Complete and self-contained: no other preset is required.

local accent = "#FF4FA3"
local panel = "#2C1431"

theme.color("chrome", "#1A0E1E")
theme.color("panel", panel)
theme.color("page", "#0F0713")
theme.color("text", "#FCEBFF")
theme.color("muted", "#C9A2CF")
theme.color("border", "#74337B")
theme.color("accent", accent)
theme.color("editor", "#120714")
theme.color("editorfg", "#F8DCFF")

theme.font("Consolas", 11)
theme.tabs(4, true)
theme.flag("wrap", true)
theme.flag("autoindent", true)
theme.flag("statusbar", true)

-- The shelf gets the same hot-magenta edge.
theme.color("diceaccent", accent)
theme.color("dicepanel", panel)
theme.color("dicecard", "#3A1B40")
