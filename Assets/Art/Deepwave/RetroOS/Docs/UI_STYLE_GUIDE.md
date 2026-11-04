# RetroOS UI style guide

The rules every icon, window and page in the package is held to. Written to be
checked, not admired: each section ends in something a reviewer can measure.

The target is a 1995 desktop — Windows 95, Mac OS Classic, Netscape Navigator
2, WinAmp 1, Explorer, Paint. Not "retro-flavoured modern". The difference is
almost always one of these three:

| 1995 | 2020s (do not do this) |
|---|---|
| One hard light source, top-left, 1px highlight and 1px shadow | Soft ambient occlusion, blurred drop shadows |
| A closed dark outline around every object | Outline-free shapes separated only by fill colour |
| Flat fills, dithered ramps, ≤16 colours per icon | Smooth gradients, alpha glows, hundreds of colours |

---

## 1. Icons

### 1.1 Grid and resolution

Icons are painted in code (`RetroIconLibrary` onto `IconCanvas`), never
imported, so "size" means the design grid and the rasterisation target
separately.

- **Design grid: 32 x 32 units**, origin top-left, `IconCanvas.Design`.
  Every painter is written in these units.
- **Paint the icon at the size it will be drawn.** This is the rule the whole
  set depends on. An icon is painted, not imported, so the right size costs
  one repaint and is then cached; squeezing a 48-pixel painting into a
  16-pixel box drops two rows in every three, and one of them is the outline
  holding the shape together. `RetroUi.Icon` calls `RetroIcons.AtSize` for
  exactly this reason, and anything assigning `Image.sprite` directly must do
  the same.
- **Prefer 32 and 64.** Both are whole multiples of the grid, so a 1-unit line
  lands on exactly 1 or exactly 2 device pixels; 16 is the exact half and is
  what the chrome uses. Sizes that are not multiples of 32 (a 22px throbber)
  are legal because the painter rounds each primitive to whole device pixels,
  but they lose detail, so do not invent new ones.
- **Never sample an icon with a filter.** `FilterMode.Point`, `wrapMode`
  `Clamp`, no mipmaps, no compression.
- **Every primitive rounds to whole device pixels, and nothing the caller
  asked for rounds away to nothing.** This is what makes a size off the grid
  legal, and it has to be true in the primitive rather than hoped for. At 16
  the scale is a half: a 1-unit line whose two edges round to the same column
  vanished outright, and a disc drawn inside another disc a unit larger left
  a half-pixel rim that appeared only where the integer lattice fell inside
  it — which is why every round icon in the chrome once had a dotted outline.
  `Fill` guarantees a pixel; `Disc`, `Ring` and `DitherRing` round the radius
  before they measure anything.
- **Say something simpler at 16.** A 32-unit design drawn at 16 has every pair
  of units sharing a pixel, so two overlapping cogs become one grey blob and
  three chalk lines become a smear. `IconCanvas.Small` is true at 20 and
  below; a painter with a simpler form to offer at that size should branch and
  offer it, the way Win95 shipped separate 16-pixel art. Worth doing only for
  an icon that actually appears at 16 - a program's icon, or a file type.

| Where | Size |
|---|---|
| Desktop | 32 |
| Dialog glyph, notification, large list view | 32 |
| Title bar, menu row, button, list row, tree row | 16 |

### 1.2 Silhouette

- The subject occupies a **28 x 28 bounding box** inside the 32-unit grid,
  leaving 2 units of air on each side. A deliberately wide-and-flat subject
  (Keyboard, Battery, Drive) may be shorter but never wider.
- **Every icon carries a closed dark outline** — `Palette.Black`, or
  `Palette.DarkGray` for something white. This is the single rule that makes
  the set work on both the teal desktop and the silver toolbar; an icon
  without one disappears against one of them. Check both sheets, always.
  The sheet run measures this: it reports **rim %**, the fraction of the
  silhouette's edge that is dark, and flags anything under 60. Nine icons
  were found rimmed in `Palette.Gray` on a `Palette.Silver` toolbar, or in
  nothing at all.
- **Fill 30–75% of the grid.** Under 30% the icon reads as a thin sketch next
  to its neighbours; over 75% it reads as a solid block. Outline-only
  treatments (a wire key, a wire eye) are out of period — 95 icons have bodies.
  The one honest exception is a subject that *is* a filled rectangle — a board,
  a screen, a window — where the frame is the silhouette and everything inside
  it is detail. `Board` and `FreeCell` sit just over the line for that reason.
- **A centred round subject measures 29, not 28**, and that is allowed. An
  odd span cannot sit centred on an even grid: a disc of radius 14 lands on
  29 and radius 13.5 on 27, with nothing in between. The check carries one
  unit of slack for this and flags 30 and over.

### 1.3 Light and depth

- One light source, **top-left**. Highlight `Palette.White` on the top and
  left edges, shadow `Palette.Gray` on the bottom and right. `IconCanvas.Bevel`
  does exactly this and is the default way to draw a **screen object** — a
  window, a button, a title bar.
- **A physical object is a three-quarter box, not a bevelled rectangle.**
  `IconCanvas.Box` draws one: a front face, a lid sheared up and to the right
  at 45 degrees, and a right cheek under it, with the whole silhouette laid
  down in black a unit proud first. Light the lid `Palette.LightSilver` or
  `White`, the front `Palette.Silver`, the cheek `Palette.Gray`. Drive,
  Printer, Server, Chip, Computer, Monitor, Book, Phone, Keyboard, Camera,
  Joystick, Network and the CD-ROM drive are all this one call with different
  proportions. A flat bevelled rectangle beside one of them reads as a button.
- **Drop shadows are hard-edged and follow the silhouette.** `Shadow` offsets
  a rectangle, which is correct behind a rectangular subject and wrong behind
  a round one — a round icon gets `DiscShadow`. A soft blur, a glow, or a
  square shadow behind a circle is a bug, not a style choice.
  A shadow counts toward the bounding box of 1.2, so a 28-unit subject with a
  shadow under it is already over: give the subject 26 and the shadow the
  rest, or drop the offset to 1.
- Depth comes from bevels, three-quarter faces and dithered ramps. Not from
  opacity.

### 1.4 Colour

Draw from `Palette` only. It is the Windows 95 VGA sixteen plus a short list
of shades the vivid set needs for glass, wood and paper.

- **At most 16 distinct colours in one icon.** More than that and it stopped
  being pixel art. (`RetroOS.IconSheet` prints the count per icon.)
- Prefer the pure system colours for anything structural: `Silver` for a
  chassis, `Black` for outlines, `White` for highlights, `Navy`/`Teal` for
  screens.
- Two-colour ramps are **dithered** (`Gradient`, `Dither`), not smoothed.
- **A colour the sixteen has not got is a checkerboard of two it has.** This
  is not an effect, it is most of why the period set looks the way it does:
  there is no manila, so a folder is `Yellow` checkered with `White`; no pale
  cyan, so a magnifier lens is `Cyan` with `White`; no brushed metal, so a
  disc face and a key bow are `Silver` with `White`. `Dither` and
  `DitherRing` do it. Reach for one of those before inventing a `Color32`.
- Object colours are a vocabulary, and two objects in the same menu should
  not share one: folders and the record drums are yellow, locks and settings
  cogs are brass, keys and chassis are steel, screens are `Navy`, refresh and
  reload are `Blue`, recycling is `DarkGreen`, and an arrow bringing
  something on to this machine is `Red`.

### 1.5 Metaphor

- The subject is a **physical object from 1995**, drawn three-quarters-on
  unless it is a screen object — a window, a page, a card, a diskette — which
  is drawn face on. A phone is a desk phone with a handset. A database is a
  stack of drums. A movie is a clapperboard counting down from three.
- The shape alone must carry the meaning at 32px with the colour removed. If
  two icons in the set have the same silhouette, one of them is wrong.
- **No real logos, trademarks or brand shapes.** Not the four-pane flag, not a
  bitten apple, not a real company's wordmark. Inspired-by only.

### 1.6 One icon per program

Two programs that do different things do not share an icon. The pair to watch
is a gallery and a single-file viewer, or a shell and a drive list.

---

## 2. Desktop, taskbar, Start menu

The numbers live in `Desktop/DesktopIcon.cs` and `RetroSkin`; this is what
they mean and what is allowed to change them.

| Thing | Value | Where |
|---|---|---|
| Desktop cell | 76 x 78 px | `DesktopIcon.CellW/CellH` |
| Desktop margin | 8 px from the work-area edge | `DesktopIcon.Margin` |
| Icon art in a cell | 32 px, centred horizontally | `DesktopIcon` |
| Label | 2 lines maximum, centred, clipped after | `DesktopIcon` |
| Taskbar height | `RetroSkin.TaskbarHeight` | theme |
| Title bar height | `RetroSkin.TitleBarHeight` | theme |
| Menu row spacing | 1 px | `StartMenu` |

- Icons fill **column-major**: down the left edge first, then a new column.
  That is what Win95 did, and it is what `DesktopIcon.Arrange` does.
- A cell is a cell at every resolution. Positions are remembered as **cell
  indices**, never pixels, so a resolution change does not scatter the
  desktop.
- Nothing overlaps the taskbar: the desktop work area is
  `screen height - TaskbarHeight`.

### 2.1 How the interface is scaled

**Whole numbers only.** `RetroScreenHost.PixelPerfect` puts the canvas on
`ConstantPixelSize` with a factor of
`floor(min(width / logical width, height / logical height))`, never less
than 1.

This is the difference between a pixel-art interface and a photograph of one.
`ScaleWithScreenSize` on its own gives a factor like 1.62 on an ordinary
display, and at 1.62 the same one-pixel border is resampled to one pixel along
part of its length and two along the rest. It also undoes the work of painting
each icon at its own size, because whatever the sprite is, the canvas
resamples it again.

What follows from it:

- A larger display shows **more desktop**, not a bigger one, until it is big
  enough for the next whole factor. That is what a real machine did when you
  raised the resolution.
- The factor is the same whether the OS is projected onto its glass or filling
  the display, because the picture is drawn at the display's size either way.
  Nothing jumps when the player sits down at the machine.
- A display smaller than the logical resolution is the one case that scales
  fractionally: there is no whole factor below 1, and the alternative is
  losing the taskbar off the bottom.

---

## 3. Windows and controls

Model: Explorer, Notepad, Paint and Navigator 2, in that order of authority.

- **Chrome order, top to bottom:** title bar, menu bar, toolbar, client area,
  status bar. Nothing floats over the client area except a menu, a tooltip or
  a modal.
- **Title bar** — icon at the left, title, then Minimize / Maximize / Close as
  three square buttons at the right. The title is clipped, never wrapped
  (`RetroUi.Clipped`).
- **Buttons** are raised bevels that go sunken on press. A disabled label is
  grey with a white 1px offset ghost beneath it.
- **Text never wraps in a control.** `RetroUi.NoWrap`. A caption whose length
  the package does not control goes in a masked rect.
- **Scrollbars** are the classic three-part: an arrow button at each end, a
  dithered trough between them, and a raised thumb the size of what is on
  screen (`RetroScrollBar`, 16px). Every scroll area gets one - a window that
  holds more than it can show has to say so, and the thumb never shrinks below
  the width of the bar or nobody can grab it. It hides itself when the content
  fits, which is what a list box did.
- **Strips whose content sets their width** — tabs, menu titles, browser
  pages, toolbars — use `RetroUi.SoftSize`, never a pinned width.
  `RetroUi.Size` pins `minWidth` as well as preferred, so a strip one item
  too long for its window has no way to give anything back and simply runs
  off the edge. A toolbar button sheds its caption before its icon.
- **A panel flush against the edge of the screen bevels only the edge that
  faces inward** (`RetroUi.BevelEdges`). All four sides on the taskbar put a
  white line down the left of the display, a grey one down the right and a
  grey seam along the bottom row, none of which 95 had.
- **Size a scroll area through `RetroUi.ScrollFrame`, never through the
  `ScrollRect`.** The ScrollRect sits on a client rect *inside* the bevel, so
  a layout element put on it lands on a grandchild no layout group reads and
  the frame collapses to nothing. Its sibling `RetroUi.ScrollBackdrop` is for
  the same reason.
- **A caption in a list cell goes in a masked rect.** `NoWrap` keeps it on
  one line and then lets it run straight through the next column.
- No rounded corners. No translucency. No animation longer than a window
  transition.

---

## 4. The web, HTML 2.0

The browser renders like a 1995 browser or it is wrong.

- **Fonts are the system fonts.** A proportional face for body text, a
  monospace for `<pre>`, `<tt>`, `<code>`. No webfonts, no font stack of
  five fallbacks.
- **Links: blue, underlined. Visited: purple. Active: red.** Always
  underlined — colour alone is not a link in 1995.
- **Layout is tables**, `<center>`, `<hr>` and `<blockquote>`. Not flexbox,
  not float-based columns.
- Backgrounds tile. `<marquee>` and `<blink>`, where used, are used sparingly
  and unapologetically.
- **Nothing rounded, nothing soft.** No `border-radius`, no `box-shadow`, no
  gradient beyond a tiled background image.
- **The browser's own `a:link` rule goes on before the page's styles**, so it
  beats inheritance and still loses to the author. Applied afterwards as
  `style.Color ??= link colour` it can never fire: inheritance has already
  filled `Color` in, and any site whose sheet sets a colour on `body` draws
  every link in the body colour.
- **Underlines are drawn, not tagged.** Legacy uGUI text has no underline
  tag, so `HtmlTextBlock` paints them from the text generator — and its
  character indices count rich-text markup, while a link range counts only
  visible characters. Indexing one with the other misplaces both the rule and
  the link's hit area. Snap the rule to whole canvas pixels: a table gives
  rows fractional heights, and a one-pixel rule at a fractional offset is
  split across two rows and disappears.
- **A picture inside a link is a block, not a run of text.** `<a><img></a>`
  is on every 1995 page; gathered into an inline run it contributes no
  characters and is drawn nowhere at all.
- Images have `WIDTH`/`HEIGHT`, an `ALT`, and a `BORDER` when they are links.
- A missing image is a torn-page placeholder with its alt text beside it, not
  a blank gap.

### 4.1 The sample sites

Five, each parodying a recognisable 1995 site without using its name, logo or
trademark. Fictional names, fictional marks, period-correct layout. Each site
has a working home page, at least two internal pages, and every link on every
page resolves — a dead link in the sample content is a bug of the same weight
as a null reference.

---

## 5. How this is checked

| Check | Command |
|---|---|
| Icon contact sheets, teal and silver | `dotnet run --project Assets/Deepwave/RetroOS/Tests~/IconSheet` |
| Icon fill %, colour count, bounding box | same run, printed table |
| Parser, CSS, storage, terminal, rules, the sample web | `dotnet run --project Assets/Deepwave/RetroOS/Tests~` |
| Icon set integrity, mounts, network | `Deepwave > RetroOS > Self Test` |
| Layout, transitions, scroll bars, the sample web | `Deepwave > RetroOS > Run Smoke Test` (25 shots) |

The two `dotnet` runs work while the Unity editor holds the project. The two
editor commands do not, and the smoke run additionally needs `-batchmode`
**without** `-nographics`.

### 5.1 Driving the running OS over MCP

The offline runs cannot see a layout, and a layout is where most of this
guide is actually broken. To exercise the real thing through the editor:

- `Application.runInBackground = true` **first**. An unfocused editor
  advances about two frames in total, so every click appears to work and
  nothing that needs a frame — a page load, a window transition, a layout
  pass — ever happens. This costs an hour if you do not know it.
- Read the picture out of `RetroScreenHost.Texture` rather than screen-
  grabbing: the OS renders into that RenderTexture whenever it is not
  focused. Set the host's `_forceEveryFrame` while reviewing, or the
  projector runs at `unfocusedFps` and hands back a stale frame.
- Anything driven by a coroutine — a page arriving over the fake modem, a
  window opening — needs the capture in a *later* call than the action.
- A click is worth more than a call to the handler: build a
  `PointerEventData` at the element's own screen point and send it through
  `ExecuteEvents`. That is what caught the link hit areas being wrong.
