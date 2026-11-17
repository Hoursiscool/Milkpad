# Milkpad user manual

Milkpad is a small, fast Windows editor that keeps everything on your own computer. It
looks and behaves like Notepad, adds tabs, formatting and live sessions, and never asks
for an account.

Open the manual from inside the app at any time with **Help → User manual** or **F1**.

The manual window has a **Save as...** button: it exports this guide as PDF, Word
(.docx), RTF, HTML, Markdown or plain text.

---

## 1. The window

| Area | What it does |
| --- | --- |
| Tab strip | One tab per open document. `+` opens a new one, `‹ ›` appear when the tabs no longer fit, `×` on the active tab closes it. |
| Menu bar | File, Edit, View, Insert, Format, Tools, QoL, Roll The Dice, Themes and Help. Every menu is keyboard reachable with Alt + the underlined letter. |
| Toolbar | Text style, font family, font size, bold, italic, underline, strikethrough, text colour, highlight colour, lists, indent, alignment, link, table. `»` on the right lists the tools that do not fit. |
| Page | The document itself, edge to edge like Notepad. |
| Status bar | Caret line and column, character count, format, read-only flag, live session state, zoom, line endings and encoding. |

The theme toggle lives in **View → Light theme / Dark theme** and is remembered between
launches. **View → Customize…** (Ctrl+,) opens the full theme studio described in
section 8, where you can change individual colours, the editor font, tab width and
automatic indentation.

Milkpad remembers its window size, position and maximized state between launches. The
executable is per-monitor DPI aware and scales its layout with Windows, so at 125% or
150% display scaling its controls and text stay the same visual size as Notepad's.

## 2. Files

- **File → New document** (Ctrl+N) starts a new tab.
- **File → New window** opens a second Milkpad window.
- **File → Open…** (Ctrl+O) opens .txt, .md, .rtf and .docx.
- **File → Open recent** lists your last ten documents.
- **File → Save** (Ctrl+S), **Save as…** (Ctrl+Shift+S), **Save a copy…** (writes a separate
  file and leaves the open document pointing at its original path).
- **File → Save all** (Ctrl+Alt+S) saves every changed tab.
- Right-click a tab for **Rename…**, **Copy file path**, **Open containing folder**,
  **Close**, **Close other tabs** and **Close all tabs**.
- **Ctrl+Shift+T** reopens the most recently closed tab. Milkpad remembers up to 12
  closed tabs for the current session.
- **File → Download as RTF…** exports the formatted document.
- Drag files from Explorer onto the window to open them.
- Saving is atomic: Milkpad writes a temporary file, swaps it in and keeps the previous
  version as `<name>.bak`, so a crash cannot leave a half-written document.

## 3. Editing

- Undo, redo, cut, copy, paste, select all — Ctrl+Z, Ctrl+Y, Ctrl+X, Ctrl+C, Ctrl+V, Ctrl+A.
- **Edit → Go to line…** (Ctrl+G) jumps to a line number.
- **Ctrl+D** duplicates the current line (or the selected lines).
- **Ctrl+Shift+K** deletes the current line (or the selected lines).
- **Ctrl+J** joins the selected lines into one.
- **Tab** inserts the configured indent at the cursor (four spaces by default), or indents
  the selected line(s); **Shift+Tab** outdents the current line or selection. Inside a
  bulleted or numbered list, Tab nests the item and Shift+Tab moves it back out. The **Tab
  width** and **Insert spaces instead of tab characters** choices in View → Customize…
  control the indent step.
- Pressing Enter keeps the indentation of the line above while **Tools → Text tools → Auto
  indent** is on.
- **Tools → Text tools** also holds sort A→Z and Z→A, trim trailing spaces, remove blank
  lines, UPPERCASE, lowercase, Title Case and Sentence case.
- **Ctrl+Shift+C** copies the selection without formatting.

### QoL tools

**QoL** collects the small, repetitive jobs that would otherwise interrupt writing:

- **Tabs and windows** — duplicate the current tab, move a tab left or right, copy its
  file path, open its containing folder, close other/all tabs, or open a new window.
- **Clean up text** — collapse repeated spaces, trim leading or trailing spaces, convert
  tabs to spaces or back, remove duplicate or blank lines, reverse or shuffle lines,
  number lines, sort by line length, wrap at 80 columns, unwrap paragraphs and straighten
  curly quotes. With no selection, a text command applies to the whole document.
- **Change case** — camelCase, PascalCase, snake_case, kebab-case, CONSTANT_CASE, dot.case
  and url-slug.
- **Select** — select the current line, paragraph or word.
- **Lines** — add bullets or quote marks, duplicate/delete a line, indent or outdent.
- **Quick insert** — a horizontal rule, checklist item, ISO or long date, 24-hour time,
  UTC timestamp or the current file name.
- **Editor** — 100/125/150/200% zoom presets, auto-indent, spaces-for-tabs, tab width
  2/4/8 and read-only mode. **Search fonts…** jumps straight to the font box.

## 4. Formatting

Bold (Ctrl+B), italic (Ctrl+I), underline (Ctrl+U), strikethrough, text colour and
highlight colour all apply to the selection or to what you type next. **Format → Font…**
opens the full font picker. The toolbar's font box is editable: start typing any part of a
font name to filter the installed and imported families, press **Enter** to apply the
highlighted or typed font, and press **Escape** to put the current font back. Style presets
(Title, Subtitle, Heading 1–3, Normal text) sit in the toolbar's first box.

**Format → Word wrap** and **View → Word wrap** are the same switch. Links can be inserted
with Ctrl+K, tables through **Insert → Table…**, and images through **Insert → Image…**.
**Insert → Emoji…** (Ctrl+E) or the ☺ toolbar button opens an emoji picker: choose a
group, click a character, and it is written at the cursor. Pictures inserted this way are
embedded in the document and are kept when you save as
`.docx`.

To use a font that isn't installed, choose **Format → Import font…** and pick a `.ttf` or
`.otf` file. Milkpad remembers it in `fonts.txt` beside your settings and adds its family to
the toolbar font list.

## 5. Finding text

**Edit → Find…** (Ctrl+F) opens the find bar; **Ctrl+H** adds a replace box. Matches are
highlighted as you type, **Enter** or **F3** steps forward, **Shift+F3** steps back,
**Esc** closes the bar and restores the original highlighting. **Replace** swaps one
match, **All** swaps every match.

## 6. Bookmarks

- **Tools → Bookmarks → Toggle bookmark** (Ctrl+F2) marks the current line.
- **Next bookmark** (F4) jumps to the following mark, wrapping at the end.
- **Clear bookmarks** removes them all. Bookmarks last for the session.

## 7. Sharing a document (live sessions)

The pink **Share** button at the right of the tab strip is the one place for working
together. It opens a side panel that stays live while you edit.

- **Start sharing** opens a port (45678 by default, moving to the next free one) and puts
  a short code such as `192.168.1.20:45678` in the panel. **Copy code** copies just the
  code; **Copy invite** copies a short message with the code and the steps for the other
  person.
- **Join session** takes a code, or the whole invite message pasted in. Spaces and extra
  text around the code are fine.
- The **People** list shows everyone with a coloured avatar, their caret line and column,
  and an `editing...` hint while they type. **Click a person to jump to their cursor.**
- The thin coloured strip beside the page marks the line each other person is on, in the
  same colour as their avatar.
- **Stop sharing** (or **Leave session** when you joined someone else) ends the session.
  If the host stops, everyone else is told the session ended.

Typing travels as one small insert or delete per keystroke, re-anchored by the text
around it, never as a copy of the whole document, so two people typing in different
places both keep their text, their caret and their scroll position. Simultaneous inserts
at the very same character keep their own order on each screen. Formatting changes travel
as a document snapshot.

The same code works across the internet if the host forwards that TCP port on their
router and hands out their public address instead of the local one. The link is plain TCP
without encryption or authentication: use it with people you trust, and never send
passwords or personal data over it. Documents are never uploaded to any server - the
peers talk to each other directly.

The first time you start sharing, Windows may ask whether Milkpad can use the network.
Allow access on private networks so the other person can connect.

## 8. View and zoom

- **View → Zoom in / Zoom out / Reset zoom** (Ctrl++, Ctrl+-, Ctrl+0), drag the
  slider in the status bar, or hold **Ctrl** and turn the mouse wheel over the page.
  The View menu shows readable shortcut labels instead of WinForms names such as
  `Ctrl+Oemplus`.
- Milkpad saves the window's normal size and position, then restores them next launch;
  a maximized window reopens maximized. Off-screen positions are brought back onto a
  visible monitor.
- **View → Status bar**, **Menu bar** and **Toolbar** hide or show those strips.
- **View → Customize… → Layout** sets the page margin, window opacity, and whether
  the menu bar and formatting toolbar are visible.
- **View → Customize… → Interface** changes the font used by menus, tabs, toolbars
  and status text, the caret color, and the selection color. The caret color replaces
  the native blinking caret with a colored bar; Reset returns to the native caret. The
  selection color draws a thin outline around selected text, because the Windows
  RichEdit control keeps its fill highlight tied to the system selection color.
- **View → Full screen** (F11) hides the window frame.
- **View → Always on top** keeps Milkpad above other windows.
- **View → Read-only mode** locks the current tab.
- **View → Customize…** (Ctrl+,) opens the theme studio. Choose a basic preset (Dark,
  Light, Midnight, Sepia or Aurora), or click any colour swatch to pick your own window
  chrome, panel/menu, page, text, muted text, border, accent, editor background or
  editor text colour. The same dialog sets the default editor font and size, tab width,
  spaces-instead-of-tabs, auto-indent, word wrap, status bar, always-on-top, page
  margin, window opacity, menu bar, toolbar, UI font, caret color and selection
  outline color. **Apply**
  saves immediately; **Reset** restores the built-in Dark or Light palette and normal
  editor defaults without applying until you press Apply. The default font and editor
  colours apply to new typing and new documents; text that already has its own rich-text
  formatting keeps that formatting, and can be changed with the normal Format → Font and
  text-colour commands.
- **Sega Genesis installer:** **Tools → Install Sega Genesis theme…** previews the
  bundled Sega-blue theme, copies it to `%APPDATA%\Milkpad\themes`, and applies it.
  It then appears in the Preset list on later launches.
- **Theme files:** basic `themes\*.cfg` files beside `milkpad.exe` and in
  `%APPDATA%\Milkpad\themes` join the Preset list after a restart. Every bundled
  preset ships with a matching Lua recipe. An optional `[Dice]` section in a theme
  file sets the Roll The Dice panel width, die sides, card limit, roll speed and
  shelf colours. `themes\README.md` documents the sections and keys.
- **Lua-style scripts:** **Load Lua script…** in the Customize footer opens a `.lua`
  recipe from `themes\`. Milkpad's script layer is intentionally safe and declarative:
  it supports `theme.preset`, `theme.color`, `theme.font`, `theme.tabs`, `theme.flag`
  and `theme.dice`, simple `local` values and `--` comments — no arbitrary code,
  files, network or input hooks. Every bundled preset has a complete, self-contained
  recipe, including the four Fun Gang character themes and the Roll The Dice shelf
  colours; `themes\LUA-README.md` has the full API list. Visibility is controlled by
  `lua-scripts` in `features.cfg`.
- **Feature registry:** `features.cfg` and the `FeatureFlag` registry in `src/Program.cs`
  are the extension point for new optional behavior. Add one `RegisterFeature(...)`
  line, read it with `FeatureEnabled(...)`, and add the key to `features.cfg`;
  `FEATURES.md` has the full recipe for new flags, presets and settings.

## 9. Printing and export

**File → Page setup…** and **File → Print…** (Ctrl+P) use the Windows printer dialogs.
**Tools → Print preview** shows the pages before printing, and **Tools → Export as
HTML…** writes a styled web page.

## 10. Sessions, recovery and statistics

Milkpad autosaves every open document every 30 seconds to `%APPDATA%\Milkpad\session`.
If it closes unexpectedly it offers to restore that session on the next start
(**Tools → Restore last session**). Comments, version snapshots and the recent file list
live in the same folder; **Tools → Open data folder** opens it.

**Tools → Document statistics** reports words, characters, characters without spaces,
paragraphs, reading time and selection counts.

## 11. Line endings and encoding

The status bar's line-ending label is a button. Click it to switch between
**Windows (CRLF)** and **Unix (LF)**; plain-text and Markdown saves then use that ending.
Documents are read and written as UTF-8 (without a byte-order mark). The setting lasts for
the current session.

## 12. Keyboard summary

| Shortcut | Action |
| --- | --- |
| Ctrl+N / Ctrl+O / Ctrl+S | New, open, save |
| Ctrl+Shift+S / Ctrl+Alt+S | Save as, save all |
| Ctrl+W / Ctrl+Tab / Ctrl+Shift+T | Close tab, next tab, reopen closed tab |
| Ctrl+mouse wheel | Zoom the editor |
| Ctrl+F / Ctrl+H / F3 | Find, replace, next match |
| Ctrl+G | Go to line |
| Ctrl+D / Ctrl+Shift+K / Ctrl+J | Duplicate, delete, join lines |
| Ctrl+F2 / F4 | Toggle bookmark, next bookmark |
| Ctrl+B / Ctrl+I / Ctrl+U | Bold, italic, underline |
| Ctrl+K | Insert link |
| Ctrl+E | Insert an emoji |
| Ctrl+, | Customize theme and editor |
| F5 | Insert time and date |
| F11 | Full screen |
| F1 | This manual |

## 13. Where things are stored

Everything you create stays under `%APPDATA%\Milkpad`: `settings.txt` (theme, custom
palette colours, editor font and size, tab width, soft tabs, auto-indent, word wrap,
status bar, line endings, always on top, page margin, opacity, menu bar, toolbar,
UI font, caret color, selection outline color, window size/position/maximized state
and every Roll The Dice setting),
`session\` (recovery), `recent.txt`, and `meta\` (comments and version snapshots).
Basic themes live in `themes\` beside the exe or in `%APPDATA%\Milkpad\themes`.
Bundled Lua recipes are factory themes in `themes\` beside the exe; your own
recipes can go in either themes folder. Feature switches are in `features.cfg`
beside the exe or in `%APPDATA%\Milkpad\features.cfg`. Delete the data folder to
reset Milkpad completely. No account, no cloud copy, no telemetry.

## 14. ROLL THE DICE

Open the **ROLL THE DICE** menu in the menu bar and choose **Open ROLL THE DICE**, or
click **ROLL THE DICE** in the tab strip. The panel fits on the right side of the
editor, beside the page, rather than covering it. The feature has its own top-level
menu category instead of living under **Tools**.

- The shelf reads installed applications from the Windows uninstall registry and
  draws a random set of cards (20 by default). **NEW PROGRAM SHELF** reads the
  installed apps again and draws a fresh set; you should not get the same shelf twice
  in a row when another installed app is available to swap in.
- Cards are ranked by the **Windows focus time** Windows records for each app in
  UserAssist. The executable path is shown in the row tooltip when it is available.
- A displayed time is cumulative foreground focus time (in hours), not necessarily
  the app's whole lifetime wall-clock time. Windows only records this for apps it has
  launched and tracked; an app with no record shows `—` rather than a made-up value.
- Press **ROLL THE DICE**. The animation settles on a number from 1 to the die's
  sides (20 by default). Number 1 erases the card with the highest recorded focus
  time, number 2 the next, and so on; if cards have already been erased, the result
  wraps around the remaining cards.
- **ROLL THE DICE → Customize Roll The Dice…** opens the shelf settings in the theme
  studio. Choose the card limit, the number of sides on the die, the panel width,
  and the roll speed; show or hide the Windows focus time; and set the accent, panel
  and card colours. Colours left on **Theme** follow the current Milkpad theme, and
  every value is saved in `settings.txt`. Theme files can set the same values in a
  `[Dice]` section, and Lua recipes can use `theme.dice(…)` and the `diceaccent`,
  `dicepanel` and `dicecard` colours.
- **Run the matching uninstaller** is on by default. When a card is erased, Milkpad
  opens that installed app's own normal uninstaller from its Windows uninstall entry.
  Milkpad does not add silent or forced-removal flags; the uninstaller controls its
  own confirmation or Cancel option.
- If no uninstaller can be opened, the card is still erased virtually and the
  history line says so. Uncheck **Run the matching uninstaller**, or set
  `break-uninstall = off` in `features.cfg`, to keep the shelf entirely virtual.
- The roll and the erased card are recorded in the history line. **NEW PROGRAM
SHELF** reads the computer again and draws another set for a new round.
- Close the panel with **×** or the **ROLL THE DICE** button. Opening **Share**
closes it; opening **ROLL THE DICE** closes Share and Comments.

### milkBall

**ROLL THE DICE → milkBall** opens a small keyboard pinball table inspired by the
hidden game in Word 97. **Z** and **M** work the left and right flippers, **Space**
launches the ball, **R** starts a new game and **Esc** closes the table. Hit the three
bumpers, the M-I-L-K rollover lanes, the two drop-target banks, the spinner, the
slingshots and the centre post for points. Light all four lanes for an extra ball;
clear a target bank to raise the multiplier; chain bumper hits for a combo. You start
with three balls. The table is drawn and simulated inside Milkpad, so it needs no
image files, network access or extra downloads.

## 15. Installing and folder layout

A prebuilt portable copy lives in `app\`. Double-click `app\milkpad.exe` to run it
without installing, or double-click **`installer\Milkpad-Setup.exe`** to open the
per-user setup wizard. Keep `milkpad.exe.config` beside the executable when you copy
the portable app elsewhere; it supplies the per-monitor DPI settings. The setup embeds the app, this manual, the bundled themes and
the uninstaller. It installs into `%LOCALAPPDATA%\Programs\Milkpad` by default,
lets you choose another folder and whether to create a Desktop shortcut, shows
progress, and can launch Milkpad when it finishes. It creates Start Menu shortcuts
and registers a per-user uninstall entry in **Settings → Apps → Installed apps**
(Apps & Features). No administrator rights are needed, and your documents and
settings stay under `%APPDATA%\Milkpad`.

The setup also accepts `--target <folder>` to start with another install folder,
`--no-shortcuts` to skip the Start Menu and Desktop shortcuts, and
`--silent --launch` for an unattended install that starts Milkpad afterwards.

To remove the installed copy, use **Settings → Apps → Installed apps → Milkpad →
Uninstall**, or the **Uninstall Milkpad** shortcut in the Milkpad Start Menu folder.
The uninstaller offers to remove the program and shortcuts and, separately, your
documents and settings.

To rebuild the setup after changing the app, run **`installer\build-installer.cmd`**.
It rebuilds `app\milkpad.exe`, compiles the uninstaller, packs the current app,
docs and themes into the embedded payload, and writes a fresh
`installer\Milkpad-Setup.exe`. The setup source is `installer\Setup.cs`; details are
in `installer\README.md`.

Development files are separated by purpose: `src\Program.cs` is the single-file
application source, `build.cmd` rebuilds `app\milkpad.exe`, `assets\` holds the
icons and DPI manifest, `docs\` holds this manual and the project notes,
`app\themes\` holds runtime theme customizations, and `installer\` holds the setup
sources and build script.
