# What the browser supports

The in-game browser is a **period-appropriate subset**, not a modern engine.
It parses and lays out real `.html` and `.css` files with its own parser and
layout engine (`Runtime/Web/Html`) — there is no webview and no JavaScript.

If you write for 1997, everything works. If you write for 2026, some of it
will not. This page is the exact line.

---

## HTML

### Structure
`html` · `head` · `body` · `title` · `div` · `p` · `br` · `hr` · `span` ·
`center` · `blockquote` · `pre` · `header` · `footer` · `section` · `article` ·
`nav` · `main` · `aside` · `figure` · `figcaption`

Unknown elements are kept and rendered as blocks, so unfamiliar markup
degrades instead of disappearing.

### Text
`h1`–`h6` · `b` · `strong` · `i` · `em` · `u` · `ins` · `s` · `strike` · `del` ·
`small` · `big` · `code` · `kbd` · `samp` · `tt` · `var` · `cite` · `q` ·
`abbr` · `mark` · `font` (with `color` and `size`)

### Links and images
`a` with `href` and `title` — **including links inline in the middle of
wrapped running text**, hit-tested per character.

`img` with `src`, `width`, `height`, `alt`, `align` (`left` / `center` /
`right`) and `border`. Give one dimension and the other is derived from
the picture's aspect ratio; give neither and it draws at its own pixels,
capped at 480 wide. An `img` inside an `a` is clickable and gets the
era's blue border. A picture that will not load becomes the torn-page
placeholder with its `alt` text beside it.

### Lists and tables
`ul` · `ol` · `li` · `dl` · `dt` · `dd`
`table` · `tr` · `td` · `th` with the `border` attribute

**Column widths.** `width` on a `td` or `th` sizes the column: a number of
pixels pins it, a percentage shares the row out in proportion, and a column
that says nothing takes an equal part of what is left. The width belongs to
the column rather than to the cell — the first row that states one sets it for
the whole column, so giving the widths in the header row is enough and the
rows underneath cannot come out ragged. This is how a page of the period built
a sidebar, and it is worth using.

### Media addresses

A url that points at a media file is not a document, and the browser does
not pretend otherwise:

| You open | You get |
|---|---|
| `.png` `.jpg` `.bmp` `.gif` | the picture, with its name and size |
| `.wav` | a small player (other formats need a player app) |
| `.mp4` `.webm` | the video, streamed from the file |
| `.txt` `.log` `.ini` | monospaced text |
| anything else | a note saying there is no viewer |

### Also handled
- HTML entities: named (`&amp;` `&copy;` `&mdash;` `&larr;` …), decimal
  (`&#65;`) and hex (`&#x41;`)
- Comments, doctypes and CDATA are skipped
- Void elements (`br`, `hr`, `img`, `meta`, `link`, …) with or without `/`
- **Unclosed tags**: `p`, `li`, `tr`, `td`, `th`, `dt`, `dd`, `option` close
  implicitly, the way a real browser handles them
- Presentational attributes: `align`, `bgcolor`, `text`, `border`, `width`,
  `height`, and `background` on `<body>` (tiled wallpaper)
- **`link`, `vlink` and `alink` on `<body>`** — how a page of the period set
  its link colours, and the only way to say it here. There is no `:visited` in
  a stylesheet, so a `color` on `a` paints a link you have followed exactly
  like one you have not; put the colours on the body instead and the browser
  keeps the two apart. Say nothing and links are blue, and purple once
  followed.
- `script`, `style`, `textarea` bodies are consumed as raw text

### Not supported
`script` execution (no JavaScript) · `iframe` · `object` · `embed` · `canvas` ·
`svg` · `video` · `audio` · forms and `input` · `select` · `frameset` ·
GIF animation *(use numbered frames instead — see below)*

---

## CSS

Loaded from `<link rel="stylesheet">`, `<style>` blocks and inline `style`
attributes. Rules are resolved by **specificity, then source order**, and text
properties inherit the way they should.

### Selectors
`tag` · `.class` · `#id` · `tag.class` · `#id.class` · comma-grouped
(`h1, h2`) · descendant (`nav a`, `#main p`) · `*`

Pseudo-classes (`:hover`, `:first-child`) and attribute selectors (`[type=x]`)
are parsed and then ignored — the rule still matches on its element part.

### Properties
| Group | Supported |
|---|---|
| Colour | `color`, `background`, `background-color` |
| Text | `font-size`, `font-weight`, `font-style`, `font-family` (monospace detection), `text-decoration`, `text-align`, `text-transform`, `line-height` |
| Box | `margin`, `margin-*`, `padding`, `padding-*`, `width`, `height`, `max-width` |
| Border | `border`, `border-width`, `border-color` |
| Layout | `display: block / inline / none / list-item`, `list-style` |

### Units and colours
`px` · `pt` · `em` · `rem` · `%` · unitless numbers · the keyword sizes
(`small`, `large`, `x-large`, …) · `thin` / `thick`

Colours: `#rgb`, `#rrggbb`, `#rrggbbaa`, `rgb()`, `rgba()`, and the 25 named
colours a 90s page would actually use (`teal`, `navy`, `silver`, `maroon`, …).

### Not supported
flexbox · grid · `position` · `float` · transforms · transitions · animations ·
media queries · custom properties · `calc()` · pseudo-elements · web fonts ·
background images · shadows · gradients · `overflow`

`@media` and `@font-face` blocks are skipped cleanly rather than breaking the
rest of the sheet.

---

## Animated images

There is no GIF decoder. Instead, name the frames in sequence and reference
the first:

```
img/banner.1.png
img/banner.2.png
img/banner.3.png
```

```html
<img src="img/banner.1.png">
```

The loader picks up the rest of the sequence (up to 64 frames) and plays it at
8 fps.

---

## Addresses

| You type | It resolves to |
|---|---|
| `northpoint.local` | `internet/northpoint.local/index.html` |
| `northpoint.local/news/` | `.../news/index.html` |
| `http://northpoint.local/a.html` | scheme stripped, then as above |
| `about.html` (a link on a page) | the same host, relative to the current page |
| `/about.html` | the same host, from its root |
| `../shared/logo.png` | normalised, then resolved |

Index documents tried in order: `index.html`, `index.htm`, `default.html`,
`home.html`.

A path that would escape the host's folder is refused with `403 Forbidden`.
A non-HTML file (a `.txt` on a LAN share) is shown as plain text.

---

## Limits

The things a page can ask for that this engine will not give it. Everything
here fails quietly and predictably - a page never breaks the game.

| Limit | What happens |
|---|---|
| Elements nest at most **256 deep** | Deeper content is kept, flattened into the deepest element still open. The renderer walks the tree recursively and builds a layout group per level, so a runaway document would cost far more to draw than to read. |
| `colspan` / `rowspan` | Ignored. Every cell takes one column; a row with fewer cells is simply shorter. |
| `width` on a `table` | Ignored. A table fills the width it is given; it does not shrink-wrap to its contents. Size the columns instead. |
| `@media print`, `@font-face`, other at-rules | Stepped over. `@media screen`, `@media all` and a bare `@media` are read as ordinary rules. |
| JavaScript | `<script>` content is parsed and discarded. Nothing runs. |
| Floats, absolute positioning, flexbox, grid | Not implemented. Layout is blocks stacked vertically, with inline runs inside them. Use tables the way 1997 did. |
| Forms | `<input>` and friends parse, but nothing is interactive and nothing submits. |
| Animated GIF | No decoder. Use the numbered-frame sequence described above. |
| Web fonts, `@import`, remote URLs | Nothing is fetched. Every file comes from the site's own folder on disk. |

Nested tables and nested lists **are** supported - they are how a page from
that era is laid out.

---

## Asking for more

If a tag or property you need is missing, open a request — several have been
added this way. The goal is not completeness; it is that anything a page from
1997 would use, works.
