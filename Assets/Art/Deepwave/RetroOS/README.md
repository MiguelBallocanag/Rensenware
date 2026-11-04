# RetroOS

A complete 90s operating system that runs inside your Unity game.

Not a UI skin — a machine. Windows that drag and resize. Drives that are
**real folders on disk** you can create, rename and delete from inside the
game, with a LAN made of other machines that have to be switched on. A browser
that renders **your own HTML and CSS**. CCTV wired to live cameras in your 3D
scene. Five period games. And an event for everything the player does, so the
computer can drive your story.

The whole interface is **drawn once and projected onto its screen**, so it can
hang on the glass of a 3D monitor and still be pixel-exact the moment the
player sits down at it.

```
Quick start (2 steps):
  1. Deepwave > RetroOS > Setup  →  tick the programs you want  →  Create
  2. Press Play.

Or by hand:
  empty GameObject → add RetroOsBootstrap (a RetroMachine comes with it),
  then add the app components you want to its children (Add Component,
  then search each one by name).
```

The **RetroOsDemo** scene (`Samples/Scenes`) is a 3D room with the OS live on
the CRT on the desk. Walk up in first person and lean in:

```
WASD / arrows  walk        Mouse  look
E              sit at the computer (when the prompt shows)
Space / Esc    stand back up
```

---

## 1. The file system is a folder

There is no file list to author. A machine's drives are the folders inside its
folder on disk, and everything the OS shows comes from there:

```
<StreamingAssets>/RetroOS/
  machines/
    workstation-04/           one folder per computer in your game
      C/                      -> the C: drive
        Windows/              -> system folder
        Program Files/
        Users/
          operator/           -> one home per account
            Documents/        -> private to that account
            Pictures/
            Desktop/
          msandoval/
      C.rmeta                 -> label: MAIN
      A/                      -> the A: floppy drive (empty = no disk)
      D/                      -> the D: CD-ROM
      D.rmeta                 -> label: EVIDENCE_04
    records-server/           another machine, reachable over the LAN
      C/archive/
      C/archive.rmeta         -> encrypted: true, password: 1958
  internet/
    northpoint.local/         one folder per web site
      index.html
    riverbend-books.com/      five sample sites ship with the package
    kingfisher.com/
    beacon-search.com/
    hometown-pages.net/
    swapmeet.com/
```

- **Drop a `.txt` in and it is in the game.** No import step, no
  ScriptableObject, no code, no Unity restart.
- **A folder named `C` is the C: drive.** `A` and `B` are floppies; everything
  else is a hard disk unless a sidecar says otherwise.
- **An empty floppy or CD folder is a drive with no disk in it**, and reports
  itself that way — exactly like clicking A: on a real machine.
- Content is **loaded lazily** — a thousand documents cost nothing until one is
  opened.
- In the editor these live in your project. **In a build they are copied to the
  player's Documents folder on first run**, so they stay writable and your game
  is moddable for free.

### Each user's files are their own

`C:\Users\<name>\` is that account's home, and **everything inside it is private
to them**. Sign in as `operator` and `msandoval`'s documents are not just
locked - they are not in the listing at all: not in Explorer, not in the
terminal's `ls`, not in the photo gallery. Opening one by typing its path gets
you *Access Denied* and raises `file.denied`.

Ownership is inferred from the folder layout, so there is nothing to configure.
To hand a file to somebody outside a home folder, or give one back to everybody,
use the sidecar:

```
owner: msandoval        # only they can see it
owner: everyone         # visible to all, even inside a home
```

A machine with **no accounts configured has no privacy** - the right behaviour
for a single-user computer, and for a game that never shows a logon screen.

### My Documents

The signed-in user's Documents folder also appears at the top level of **This
Computer**, next to the drives, with a document icon - the shortcut a 90s user
went looking for. It follows Log Off / Log On automatically, and disappears when
nobody is signed in.

### Properties live in a sidecar

Everything the OS needs beyond what the file system already knows goes in a
`<name>.rmeta` file next to the item — `readme.txt.rmeta` for a file,
`Archive.rmeta` for a folder:

```
hidden: true
encrypted: true
password: 1958
readonly: true
label: MAIN
owner: msandoval
note: sealed after the incident
share: ARCHIVE
sharecomment: Incident records. Sealed folders need the year.
```

**Select the file in Unity's Project window and edit these in the Inspector.**
Clear every field and the sidecar deletes itself, so a plain folder of
documents stays a plain folder of documents.

| | |
|---|---|
| `hidden` | Out of the listing until something reveals it. A story rule can, and what it reveals stays revealed. |
| `encrypted` + `password` | The in-fiction lock. See the note below. |
| `readonly` | Nothing under here can be created, renamed, moved or deleted. Inherited by everything inside, so marking a folder marks a disc. |
| `owner` | The account this belongs to. Nobody else can list it or open it. `everyone` gives it back. |
| `label` | A caption shown instead of the file name — a volume label, a friendlier folder title. |
| `note` | Free text, shown in the Properties dialog. |
| `share` | Publishes this folder to the LAN under that name. `sharecomment`, `sharepassword` and `sharehidden` go with it. See §1b. |

> **`encrypted` is a lock, not encryption.** The bytes on disk are the plain
> file, and a player who opens your game's data folder can read them in
> Notepad. It is called `encrypted` because that is what a character in 1995
> calls a file that wants a password, and because the word is what makes the
> fiction land — not because anything is enciphered. Use it for story locks.
> Do not use it for anything a real player must not read.

> It is not called `.meta` because Unity owns that name and rewrites those
> files. The sidecar is the same idea in a file your game can also read at
> runtime, in a build, on any platform.

## 1b. Machines, and a network that is really a network

Each computer in your game is a **RetroMachine** component: a name, an address,
a folder of drives, and a power switch.

```
Retro OS                    (RetroWorld - files, clock and LAN for the game)
├── Internet                (RetroInternet - the line to the outside world)
│   └── northpoint.local    (RetroInternetHost)
├── PC_01                   (RetroMachine "WORKSTATION-04" + RetroOsBootstrap
│   │                        + RetroMonitor - resolution, picture, pointer)
│   └── Programs            the software installed on THIS machine
└── PC_02                   (RetroMachine "RECORDS-01", its own screen, a
                             different six programs)

  (What each machine publishes to the LAN is not in the scene at all: it is
   `share: DOCS` in C/Documents.rmeta, and `share: ARCHIVE` in C.rmeta.)

Workstation                 (the desk the player walks up to)
├── CRT Monitor
│   └── crt_monitor_glass   (a renderer wearing that machine's RenderTexture)
├── Keyboard / Mouse        (meshes)
└── Screen Focus            (the pose the camera glides to)

Workstation_1               (the second desk, same again)
```

**One thing in the game is one component.** A computer, the system installed on
it and the screen its picture comes out on are the same object:
`RetroOsBootstrap` requires a `RetroMachine` and a `RetroMonitor`, so there is
nothing to wire between the three.

**The screen belongs to the computer, not to the furniture.** There is no
**Monitor** field pointing at a piece of glass somewhere else in the scene, and
no search outwards for the nearest one: the monitor is the component beside the
bootstrap, and a machine that has none boots on a default screen and says so in
the console. What hangs in the world is a renderer wearing that monitor's
`RenderTexture` - your material, your shader, as many surfaces as you like -
and `RetroMonitor.Computer` still names the machine that booted on a given
screen, so a prop can ask what is running on it rather than hold a reference to
a computer.

**Every machine is its own computer.** Its accounts, its look, its programs,
its drives and what it remembers between sessions are its own components, its
own folder under `machines/`, and its own `machine.prefs`. Nothing a player
does at one keyboard reaches the other; the two meet on the network and nowhere
else. A machine that nobody sits at needs no bootstrap at all - a
`RetroMachine` whose folder holds a shared folder is a server in a rack, and
adding a bootstrap later turns it into a computer somebody can use without
changing how the building reaches it.

**Which settings live where.** The computer's own name, its accounts and the
BIOS screen it prints on the way up are on `RetroMachine` - they belong to the
hardware and survive the disk being wiped. The system's name, its theme,
wallpaper, sounds and story rules are on `RetroOsBootstrap` - they are the
software. How big its picture is, which part of that picture the OS fills and
how the player points at it are on `RetroMonitor` beside them - the screen is
part of the computer. The date, the data folder and the LAN are on
`RetroWorld`, because the computers in a room are in the same room.

Three things are the world's rather than any machine's, and both components
below are optional - leave them out and a single-machine scene behaves exactly
as it always did:

- **`RetroWorld`** says where `machines/` and `internet/` are on disk, what
  date the fiction starts at, and whether the machines list each other. There
  is one data folder and one clock for the whole game, so neither can be a
  machine's own business; a scene without the component gets a default world
  the first time a machine asks for one.
- **`RetroInternet`** is the line to the outside world, with one switch on it.
  Turn it off and every site is unreachable at once (`No route to ...`) while
  the LAN carries on - a cut cable, not a dead server.

- **A share is a folder on another machine**, published by `share: NAME` in
  that folder's own `.rmeta` sidecar - beside its password and its owner, so a
  folder copied to another machine arrives shared. There is no `lan/` folder to
  author: if no machine in the scene is serving it, it is not on the network.
  The player can publish and unpublish folders at runtime from Explorer's
  right-click **Sharing...**, which writes the same four lines.
- **Switch `poweredOn` or `connected` off and the share disappears** from every
  other machine's Network Neighborhood, mid-game, because that is what pulling
  the plug on a file server does.
- Addresses are **static** or **automatic** (the network hands out the next
  free one, the way a router does).
- `RetroInternetHost` does the same for a web site: a domain, aliases, a
  folder, and an on/off switch. `os.Net.SetOnline("host", false)` kills it
  mid-story. A folder under `internet/` with no component is still published,
  so adding a site can remain "make a folder".
- One site going quiet and the internet going away are different events.
  `os.Net.SetInternetOnline(false)` cuts the line for every machine at once;
  `os.Net.Online = false` unplugs the modem on *this* machine and leaves the
  others browsing.

Drives are reached in code through `os.Drives`:

```csharp
os.Drives.Insert('D', discFolder, "EVIDENCE_04");   // put a disc in
os.Drives.Eject('D');                               // take it out
os.Drives.Refresh();                                // re-read from disk
```

## 2. The browser

It is built like Navigator was: a menu bar (File, Edit, View, Go,
Bookmarks, Options, Directory, Help), a row of navigation buttons
(Back, Forward, Home | Reload, Images, Open, Print, Find | Stop), the
**Netsite:** field with a logo that turns while a page loads, a strip of
directory buttons, and a status bar with the security padlock.

Working: several pages open at once on a strip of folder tabs, each with
its own history — Back in one never walks through another's trail — a
bookmarks window that can go, open in a new page or remove - and a
**Bookmarks** list on the browser component, so a machine belonging to
somebody with a past has bookmarks on it before the player touches it -
Find in page,
View Source, Document Info, and Save As, which writes the page onto the
machine's own disk where every other program can then open it and lists
it under **File → Downloads**. The label switches between **Netsite:** and
**Location:** the way Navigator did. Directory buttons are configured on
the browser component; leave them empty and it lists whatever sites are
actually reachable.

**A page takes a moment to arrive.** `Load Seconds` on the browser
component is how long — a fraction of a second reads as a modem — and it
is what makes the turning logo, the status line and the **Stop** button
mean anything. Stop cancels the page on its way rather than announcing
that it did. Set it to 0 for an instant machine.

### It renders your real web pages

Write a site in any editor, check it in Chrome, drop the folder into
`internet/`. The folder name is the address.

**Five sites come with it**, cross-linked, so the browser has somewhere to go
the first time it opens: a bookshop with a modem (`riverbend-books.com`), a
directory kept by hand (`kingfisher.com`), a full-text search engine
(`beacon-search.com`), a free-space community with a guestbook and a visitor
counter (`hometown-pages.net`), and an auction list (`swapmeet.com`). The
names and the marks are invented; the shapes are the ones the 1995 web had.
Delete the folders you do not want.

Supported: headings, paragraphs, lists, tables, images, links (**including
inline links in the middle of wrapped text**), `<b> <i> <u> <font> <code>
<pre> <blockquote> <center> <hr> <br>`, entities, comments, inline `style`
attributes, `<style>` blocks and linked stylesheets.

CSS: `color`, `background`, `font-size`, `font-weight`, `font-style`,
`font-family` (mono detection), `text-decoration`, `text-align`,
`text-transform`, `line-height`, `margin`, `padding`, `width`, `height`,
`max-width`, `border`, `display`, `list-style` — with tag, `.class`, `#id`,
grouped and descendant selectors, resolved by specificity.

**Not supported, by design:** JavaScript, flexbox, grid, absolute positioning,
floats. It is 1997.

Pictures honour `width`, `height`, `align="left|center|right"` and
`border`, are clickable inside an `<a>`, and fall back to the classic
torn-page placeholder with the `alt` text when a file is missing.
`<body background="tile.gif">` tiles behind the page.

Animated pictures: name the frames `banner.1.png`, `banner.2.png`, … and
reference the first one.

**Opening a media file shows the media.** Browse to a `.png` and you get
the picture, to a `.wav` and you get a player, to a `.mp4` and it plays -
not the file's bytes read as text.

Take a site off the air mid-story with `os.Net.SetOnline("host", false)`.

## 3. Programs

**A program is a component.** Add one to a child of the bootstrap
(**Add Component**, then search for it by name) and it is installed when the
scene boots. Nothing lists them anywhere else, so the software a computer runs is
just what sits under it in the hierarchy — and every program carries its own
UnityEvents.

The demo puts them all under a single `Programs` child:

```
RetroOS                    (RetroOsBootstrap)
└── Programs
    ├── MyComputer         (RetroFileExplorerApp)
    ├── Terminal           (RetroTerminalApp)
    ├── TheInternet        (RetroBrowserApp)
    ├── Inbox              (RetroMailApp)
    ├── Cctv               (RetroCctvApp)
    └── …
```

Every program configures:

| Field | Meaning |
|---|---|
| `appId` | Stable id used by events and rules |
| `title`, `icon` / `builtinIcon` | What the player sees |
| `windowSize`, `minWindowSize`, `resizable`, `startMaximized` | Window behaviour |
| `singleInstance` | Re-focus instead of opening twice |
| `showOnDesktop`, `showInStartMenu` | Placement |
| `launchOnBoot` | Already open when the player sits down |
| `sortOrder` | Order on the desktop and in Start |

…plus its own settings: the CCTV app carries its cameras, the mail app its
messages, the browser its home page and pages, Mines its board size, the
Terminal its own commands, Reminders its list.

**What ships:**

| Program | What it does |
|---|---|
| **My Computer** | Tree + list, three view modes, hidden files, properties, and real file operations: New Folder, New Text Document, Rename, Delete, cut/copy/paste |
| **Notepad** | Edits the machine's real files. New, Open, Save As, Find, word wrap, time-stamp |
| **The Internet** | Renders your own HTML and CSS. Tabbed pages, bookmarks window, history, downloads that write to the machine's disk, and a settable load time so Stop means something |
| **Inbox** | Inbox / Sent / Deleted trays, attachments that open in whichever program handles them, reply, forward, compose |
| **Control Panel** | Appearance (14 of the schemes the machine shipped with), sounds, date and time, passwords, network, storage |
| **CCTV** | Live scene cameras, recorded tapes, and an in-engine DVR ring to scrub back through |
| **Photo Gallery** | Every picture the account can see, with a viewer that pages on the arrow keys |
| **Image Viewer** | Fit, actual size, zoom, quarter turns, drag to pan |
| **CD Player** | The folder is the disc: track list, transport, seek, random and continuous order, and timed lyrics from a `.lrc` beside the track |
| **Media Player** | Playlist, seek, playback speed, repeat, full-window |
| **Terminal** | The shared shell, command history, Tab completion, optional typewriter output, and commands you author in the inspector |
| **Calculator** | Standard view with the memory keys. Folds like a desk calculator: `2 + 3 * 4` is 20 |
| **Reminders** | Timed against the machine's own clock and rung by the program, so one still goes off with the window shut; each raises `reminder.due` |
| **Messenger** | A contact list and scripted conversations - lines arrive after a pause, a line can wait for the player to choose what to say, and a line can send a file. A conversation survives closing the window, and is dropped when somebody else logs on |
| **Games** | Mines (with best times), Solitaire, FreeCell, Reversi, Snake (with a high score) |

And its own **UnityEvents**: opened, closed, focused, minimized, maximized,
plus `On Action` for every interaction inside it and named hooks that match one
action (`won`, `saved`, `visited`).

### Writing your own

```csharp
public class MyApp : RetroAppBehaviour
{
    public override void BuildContent(AppContext ctx)
    {
        RetroUi.VLayout(ctx.Content.gameObject, 4, 4);
        RetroUi.Label(ctx.Content, "Hello from 1995!");
        RetroUi.PushButton(ctx.Content, "Do thing", () =>
        {
            ctx.Os.Audio.PlayUi(UiSfx.Notify);   // sound
            ctx.Raise("thing", "done");          // -> myapp.thing, app.action
        });
    }

    public override void ApplyDefaults()
    {
        app.appId = "myapp";              // what rules and events call it
        app.title = "My App";
        app.builtinIcon = BuiltinIcon.Computer;
        app.category = "Accessories";     // empty = straight under Programs
    }
}
```

See `Samples/Scripts/Apps/ClockApp.cs` for a complete commented example.

**No code?** Add the **RetroPrefabApp** component: point it at a uGUI prefab.
Components implementing `IRetroAppView` receive the `AppContext` on open.

Useful inside an app:
- `ctx.Os` — every service: `Fs`, `Net`, `Auth`, `Audio`, `Events`, `Flags`,
  `Clock`, `Mail`, `Apps`, `Windows`, `Disc`
- `ctx.Window` (title, close, events), `ctx.Content` (your RectTransform)
- `ctx.File` — set when the app was opened via a file association
- `ctx.Raise(action, arg)` — raise this app's own game event and fire its hooks
- `ctx.Config<T>()` — read a per-app content contract from the program

## 4. The UI component kit

Every element is a `RetroWidget` MonoBehaviour with a `Create(...)` factory —
no prefabs, no scene wiring, and each one owns its own look, so fixing a bug
in `RetroPushButton` fixes every button in the OS.

`RetroPushButton` (push and toggle) · `RetroCheckBox` + `RetroRadioGroup` ·
`RetroTrackBar` · `RetroProgressBar` · `RetroTextBox` · `RetroComboBox` ·
`RetroListView` (list / details with sortable columns / icons) ·
`RetroTreeView` (with lazily loaded branches) · `RetroTabControl` ·
`RetroGroupBox` · `RetroToolStrip` · `RetroStatusStrip` · `RetroMenuStrip`
(real drop-downs with submenus) · `RetroContextMenu` · `RetroToolTip` ·
`RetroSplitter` · `RetroDialog` (alert / confirm / prompt / custom)

Reach them through `RetroUi`: `RetroUi.PushButton`, `RetroUi.ListView`,
`RetroUi.Tabs`, `RetroUi.Menu`, `RetroUi.Split`, … They work on any uGUI
canvas, with or without the OS running.

**Mouse buttons.** Everything activates on the primary button only — the right
button opens context menus and nothing else. `RetroUi.IsPrimary(e)` is the test
if you write your own handler, and `RetroUi.IsTyping()` tells you a text field
has the keyboard, which anything reading keys directly has to respect.

**Menu shortcut hints are decoration.** `RetroMenu.Command(caption, action,
"Ctrl+S")` draws `Ctrl+S` down the right of the row, the way 95 did, but does
not bind the key — binding keys globally would take them from your game.
Wire whichever ones you want in your own input handling and call the same
method the menu item calls.

## 5. Theming

One `RetroTheme` asset controls every colour, metric and font. Assign it to
`RetroOsBootstrap → Look → Theme`; leave it empty for the stock Windows 95
palette.
Assigning a theme at runtime restyles the OS live.

Icon resolution (32 / 48 / 64) and the "vivid" switch live on the theme too,
along with `gradientTitleBars` — the left-to-right wash the OS grew in 98,
drawn as a mesh effect so it costs no material, no texture and no draw call.

**Colour schemes.** On top of the theme, the Control Panel's *Appearance* page
offers fourteen of the schemes the machine actually shipped with — Brick,
Desert, Eggplant, Plum, Slate, Spruce, Teal (VGA), High Contrast Black. Picking
one repaints the windows that are already open: `RetroPalette` walks the work
area, matches each untextured graphic against the scheme being replaced, and
writes the same slot from the new one. Nothing has to be tagged as it is built,
and there is no per-graphic component to pay for.

A scheme is the *player's* choice, so it is remembered in the machine's own
settings file and never written back to the theme asset — a player picking Plum
must not edit your project.

## 6. Icons

67 built-in icons, **painted from code at runtime** — no atlas to import, no
resolution to pick at build time. Browse and export them all as PNG from
**Deepwave → RetroOS → Icon Browser**.

```csharp
RetroIcons.Get(BuiltinIcon.Floppy);        // at the theme's resolution
RetroIcons.Get(BuiltinIcon.Floppy, 64);    // at a specific size
```

## 7. Game events

Everything raises on `os.Events`:

```
os.boot / os.login(user) / os.logout(user) / os.shutdown
os.poweron                              ← the machine was switched back on
os.bsod(title)                          ← the machine crashed
app.opened(appId) / app.closed / app.focused / app.minimized / app.maximized
app.action(appId:action[:arg])          ← every interaction inside any program
file.opened(path) / file.saved(path) / file.denied(path)
node.unlocked(path) / node.revealed(path) / fs.changed(path)
explorer.navigated(path) / terminal.command(line)
mail.received(id) / mail.read(id) / mail.sent(to) / mail.deleted(id)
reminder.due(id)                        ← a reminder went off
chat.message(contactId) / chat.said(text)
web.visited(url) / web.failed(url) / web.download(url)
printer.job(path)                       ← something was printed
cctv.selected(label)
disc.inserted(label) / disc.ejected(label)
net.mounted(zone/host) / net.reached(host) / net.unreachable(host)
flag.set(name) / flag.cleared(name)
```

Programs also raise their own: `mines.won`, `solitaire.won`, `notepad.saved`,
`browser.visited`, and anything you raise with `ctx.Raise(...)`.

**Three ways to react:**

- **Rules (no code)** — `RetroOsBootstrap → Story rules`: when *event* [+ *argument*,
  `*` = prefix match] [+ *requiredFlag*, `!flag` = must be unset] → run
  actions: `SetFlag`, `ClearFlag`, `RevealNode`, `HideNode`, `UnlockNode`,
  `DeliverMail`, `OpenApp`, `ShowAlert`, `Notify`, `PlaySound`, `RaiseEvent`,
  `Bsod`, `LogOff`, `Shutdown`, `HostOnline`/`HostOffline` (take a web site
  off the air mid-story) and `ChatOnline`/`ChatOffline`.
- **Inspector** — add **Deepwave → RetroOS → RetroOS Event Listener** to any object and
  wire UnityEvents, or use the hooks on an app component.
- **Code** — `RetroOs.Current.Events.Subscribe("file.opened", path => …)`,
  `RetroOs.Current.Flags.IsSet("archive_opened")`, `RetroOs.Raise("...")`.

**The terminal answers words you invent.** The **RetroTerminalApp** component carries
its own command list: a word, what it prints, a flag it sets, an event it
raises and a UnityEvent that gets whatever was typed after it. A command with
no description stays out of `help`, so the game can hide one to be found. Set
`once` and it answers differently the second time.

**Say something without stopping the player.** `RetroOs.Current.Notify(title,
body, icon, onClick)` raises a balloon over the tray rather than a modal box —
which is what new mail, an inserted disc and a reminder going off all use.

## 8. Games

Mines, Solitaire (Klondike), FreeCell, Reversi with a real opponent, and
Snake. All built on public-domain rules with original code and art. Each
raises `won` / `lost` / `score`, so a game inside the computer can be a puzzle
in your game.

## 9. CCTV

Each channel is, in priority order:

1. **Live device** — set `deviceId` to match a `CctvCameraDevice` on a real
   `Camera` in your scene. PAN/ZOOM rotate and zoom that camera; it only
   renders while watched.
2. **Recorded tape** (`recording`) or **still** (`staticImage`) — PAN/ZOOM
   become digital pan/zoom.
3. Nothing set — NO SIGNAL static. That is how you ask for a dead camera on
   purpose. A channel that *names* a `deviceId` nothing in the scene answers
   to shows the same static, so that case warns in the console instead: on
   screen the two are the same words, and a typo in that field should not look
   like a story beat.

With scanlines, an in-fiction timestamp, a blinking REC light, a noise burst on
channel switch, and a DVR ring buffer so a live feed can be rewound in
PLAYBACK. Watching a channel raises `cctv.selected(label)`.

## 10. Windows, the desktop, and the work area

- Windows drag, resize (per-app minimum sizes), minimize, maximize, and are
  **clamped to the work area** — one can never be lost off-screen or hidden
  behind the taskbar.
- Desktop icons clamp while dragging, snap to the grid on release, and bump to
  the nearest free cell instead of stacking.
- **Right-click works where 95 says it should.** The desktop offers Arrange
  Icons, Line up Icons, Refresh and Properties; a shortcut offers Open and
  Properties; the taskbar offers Cascade, Tile Horizontally, Tile Vertically
  and Minimize All Windows; files and folders in Explorer get their own menu.
- **Work area:** the whole OS lives inside a configurable region of the
  picture, set on the machine's **Monitor** — `Work Area`, a normalized rect
  whose `0,0,1,1` is the whole picture. Which part of a screen the desktop
  fills is a fact about the screen, so a bezel that eats the edges of the glass
  is something the OS can be held clear of.
- **Every window is two rects.** The host stretches the work area and carries
  the window's depth and its transition; the frame inside it is the chrome you
  see and drag. That split is what lets a window maximize by zeroing four
  offsets, resize from a pivoted corner without its title bar wandering, and
  animate as a whole. Depth across the entire OS is sibling order and nothing
  else — no per-window canvases, no sorting orders — so windows can never
  interleave with each other's children.
- Windows open, close and minimize through `WindowTransition`, a curve rather
  than an animation clip: a window built entirely in code still animates, and
  reskinning the OS never means re-authoring anything. It idles at zero cost.
  Set `instant` for the flat, immediate 95 feel.
- Dialogs, menus, start-menu fly-outs, tool-tips and tray notices all animate
  through one component, `RetroPopup`. A closing menu stops taking clicks the
  moment it starts to fold, so the click that dismissed it lands where the
  player aimed it.
- The machine remembers: where the player dragged each desktop icon, the
  wallpaper they chose, which colour scheme they picked, whether the clock is
  24-hour, which side panes they folded away. It is one small text file beside the drives, and
  **Settings > System > Forget Settings** puts it all back.
- Side panes fold away (`RetroNavDrawer`) - the Inbox trays, the messenger
  contacts, the media playlist. They animate their width rather than switching
  off, so the content beside them slides instead of jumping, and each one
  remembers whether the player folded it.
- Every strip whose length comes from its content - tabs, menu titles, browser
  pages, taskbar buttons, mail attachments - crowds rather than pushing its
  last item outside the window.

## 11. On a 3D monitor

**The OS is drawn once, flat, and projected onto its screen.** A projector
camera that sees nothing else renders the whole interface into a
`RenderTexture`; that texture *is* the machine's picture, and the OS stops
there. Where it goes is yours: assign a `RenderTexture` asset to the monitor's
**Texture Asset**, drop the same asset into your screen material, and the
desktop is on the glass - lit like a real CRT, bent by whatever that material
does, and visible in edit mode as well as in play. The OS never touches your
materials, so any shader, any texture slot and any number of surfaces work, and
nothing - not even the on-screen cursor - needs to know where the glass is.

This is why the OS does not put a world-space canvas on the monitor: a canvas
in the world is resampled by the scene camera at whatever angle it happens to
be seen from, which is exactly how a one-pixel Windows 95 border turns to
mush.

### Interaction

**Interaction** on the monitor picks how the player points at it:

- **Fullscreen Overlay** (default) — `SetFocused(true)`, the player sitting
  down, flips the canvas to a screen-space overlay and switches the projector
  off, so while the machine is being used it is pixel-exact at the display's
  own resolution and clicks land with full precision. `SetFocused(false)` puts
  it back on the glass.
- **On Screen Cursor** — the OS never leaves its `RenderTexture`. A classic
  arrow is drawn on the top layer of the desktop and **walked** by relative
  input - the same way a real mouse works, which does not know where the
  monitor is in the room. Every hover, click, drag, scroll and keystroke is
  routed to the OS through a private pointer device, while the real devices stay
  free for the game. The cursor moves under whatever the player is using: the
  **mouse** (delta, with the hardware cursor hidden and *confined* to the window
  while seated - at the default `Cursor Speed` of 1 the two move exactly
  together, so they reach the edges at the same moment), a **gamepad** right
  stick (South / East click, left stick scrolls), or a **dragging finger** (a
  quick tap clicks). `Cursor Speed` and `Stick Speed` tune it. Nothing is asked
  of the surface - no collider, no camera, no UV that reads 0-1 - so the picture
  can be on a curved tube, a `RawImage` or a wall of six screens all the same.

  > Do not swap that `Confined` for `CursorLockMode.Locked`.
  > `InputSystemUIInputModule` forces every mouse pointer to `(-1, -1)` while
  > the cursor is locked and drops drags entirely, so the OS would draw
  > perfectly and answer nothing.

### The picture

**Texture Asset** is the whole hand-off. The OS renders into that
`RenderTexture` and nothing else; wire it into a material, a `RawImage`, six
`RawImage`s for a wall of security monitors - whatever the scene needs.
`Dynamic Resolution` is ignored when it is set, since the asset has a size of
its own. Leave it empty and the picture goes to a texture sized to the display,
which is what a game that is only the OS wants; that texture is
`RetroOsBootstrap.ScreenHost.Texture` if you want to grab it from code.

`Resolution` sets the logical size the interface lays out at (1024x768 is the
period-accurate 4:3 desktop); `Dynamic Resolution` renders at the display's own
size so the picture is as sharp as the monitor allows. Turn it off for the
chunky, honestly-low-resolution look.

While nobody is at the machine the picture is redrawn on a timer rather than
every frame - `Unfocused Fps`, 15 by default. On a desktop that is mostly
sitting still it is indistinguishable from 60 and costs a quarter as much; set
it to 0 for a machine playing video that somebody is meant to watch from across
the room.

When the player sits down the canvas becomes a screen-space overlay at
`Sorting Order` (100). Raise it to draw the OS over a HUD the game already has
up, lower it to let one draw over the OS.

The screen material wants to be **opaque and emissive** - a powered tube is not
a pane of glass - with the render texture in both **Base Map** and **Emission
Map** and an emission colour of white. That is exactly how
`crt_monitor_glass.mat` in the demo is set up. Tiling and offset are the
material's own, so a mesh that is not mapped 0-1 is fixed there; `(-1, 1)`
mirrors a monitor modelled facing the other way. The on-screen cursor lives in
the picture's own pixels, so a mirrored or oddly-mapped mesh does not affect it.

Two components drive the demo room:
- **First Person Controller** — WASD/arrows move, mouse looks.
- **Retro Workstation** — walk up + **E**: the camera glides to the screen and
  it becomes clickable; **Space/Esc** pulls back out (ignored while typing). At
  a machine that is switched off the same key is the power switch instead, and
  the prompt says so; the player watches it boot from where they stand. Sitting
  down puts the way back out on screen for a few seconds (`Exit Hint Seconds`),
  drawn over the desktop and clear of the taskbar.

## 12. Audio

`RetroOsBootstrap → Sound` sets volumes and optional clips for every
interaction. **Empty slots fall back to synthesized retro beeps** — the OS
makes sound with zero assets.

`os.Audio.PlayUi(UiSfx.Click)`, `os.Audio.Play(clip)`,
`os.Audio.PlayMusic(clip)`, `os.Audio.MasterVolume / UiVolume / MusicVolume`.

**Master volume scales this OS only.** The machine is a prop in your world, so
it does not touch `AudioListener.volume` and will not fight your own audio
settings. If your game *is* this OS, switch on `Sounds > Master Affects Game`
and the slider in the fake Control Panel drives the real listener instead.

## 13. Power, starting up, and the blue screen

**Starting up.** `RetroMachine → Start-up` prints a firmware screen before
the logon prompt: the memory count, the drives the machine actually mounted,
the address it actually has. Off by default. The `Extra Lines` are where a game
says whose computer this is or what is wrong with it — the only place it can,
before the fiction of the desktop takes over. Any key skips it.

**A machine can start switched off.** Turn `Start Powered On` off and the
bootstrap builds the whole computer — context, drives, programs — and then
leaves the monitor dark. Nothing boots until something calls
`bootstrap.PowerOn()`, and `bootstrap.PoweredOn` says which state it is in.
`os.boot` waits for the same moment, so a rule that greets the player at
start-up fires when they are standing there to read it rather than at the top
of the scene. While it is down the machine drops out of Network Neighborhood,
because a computer that is off is not a file server.

**Shut Down does not quit your game.** It takes the in-fiction machine down,
and nothing else: there is one behaviour and no setting to pick it with.

Start → **Shut Down…** asks first, tears the desktop down, holds the era's
please-wait notice for a moment and then fades the glass to a dead monitor.
The machine drops off the LAN as it goes dark, because a computer that is off
is not a file server. Start → **Log Off…** is the same shape — ask, tear down,
hold — and lands back on the logon screen instead.

Both are also `RetroOsContext` methods. `Logout()` and `Shutdown()` go straight
through, which is what a story rule wants; `RequestLogout()` and
`RequestShutdown()` ask the player first, which is what the Start menu calls.

`os.shutdown` fires either way, so your game can drive its own ending, and
`bootstrap.PowerOn()` boots the machine again (a switch on the CRT prop, a
story beat, the player at the desk) and raises `os.poweron`. A machine that has
been shut down reads as powered off until then, so a prompt in your game can
offer to turn it back on.

**Crashing it on purpose:**

```csharp
RetroOs.Current.Bsod("Windows",
    "A fatal exception 0E has occurred at 0028:C0011E36.",
    canContinue: true);   // false leaves it up until you dismiss it
```

There is a rule action for it too, so a blue screen can be a story beat with no
code: `Bsod`, alongside `LogOff` and `Shutdown`.

## 14. Login

`RetroMachine → Accounts → Login`:

- **Automatic** (default) — no users configured means **straight to the
  desktop, no logon screen**. With users, it asks.
- **Skip Always** — never ask; signs in as `autoLoginUser` or the first user.
- **Always Ask** — always show the logon dialog.

## 15. Editor tools

There are two menu entries, because everything else is a button where you are
already looking.

| Menu | What it does |
|---|---|
| **Deepwave → RetroOS → Setup** | Adds a computer to the open scene, installs the programs you tick as components under it, writes the content folders. Also has buttons for a second computer, for rewriting the sample content, and — under **Check** — for the two tests below and for repainting the component icons |
| **Deepwave → RetroOS → Icon Browser** | All 71 icons at 32/48/64, with PNG export and a one-click contact sheet |

Both checks live in the Setup window's **Check** section, and both have
command-line entry points, which is what CI actually uses:

| Check | What it does |
|---|---|
| **Run the self test** | Headless check of the HTML and CSS engines, the icon painter, folder mounts, address resolution, path-traversal, per-user privacy and the lyric reader — 188 assertions |
| **Take screenshots of the running OS** | Enters play mode, drives the logon, opens each program, repaints the interface in three colour schemes, and maximizes, minimizes and folds a pane — 25 screenshots into `SmokeShots/` |

**The nine components a scene is built from wear their own icons** in the
Inspector, the Hierarchy and the Project window: the bootstrap, the machine,
the monitor, the world, the internet and one host on it, the event listener,
the CCTV camera and the theme asset. They are drawn for Unity — laid out on a
16×16 grid, which is the size the Inspector header and the Hierarchy actually
draw, and written out at ×4 unfiltered — not borrowed from the OS's own desktop
icons, which are shaded for a teal desktop rather than for two editor skins.
The programs, the window plumbing and the interface widgets keep Unity's script
icon on purpose: an icon that is on everything distinguishes nothing.

They ship painted with the package. To change one, edit its art in
`Editor/RetroInspectorIcons.cs` — each glyph is sixteen strings of sixteen
characters, one per palette colour — and press **Repaint the component icons**
in the Setup window.

**Offline tests.** `Tests~/RetroOS.LogicTests.csproj` runs the layers that need
no Unity at all — the calculator's arithmetic, the mailbox's trays, the system
clock, the terminal's shell, the `.lrc` lyric reader, **the HTML parser and the
CSS engine**, **the story rules**, **the event bus**, **the `.rmeta` sidecars**, **path
resolution and per-user privacy**, and the file operations against a real
temporary tree. `dotnet
run` it; it prints one line per check and exits non-zero on failure. Worth
having because the editor usually holds the project open, which stops a
batch-mode play run — and because its first run caught two real calculator bugs
a compile could never see.

The parser tests are the shapes a page written by hand actually has: `<li>`
with no closing tag, single-quoted attributes, a stray `</b>`, a file that ends
mid-tag, `&nbsp;`, and a CSS rule with a colon missing. A parser that throws on
any of those takes the whole browser window with it.

## 16. Requirements

- Unity **2022.3 LTS or newer**, including Unity 6
- **uGUI** and the **Input System** package (both declared as dependencies, so
  the Package Manager pulls them in)
- Built-in, URP and HDRP — the OS is uGUI, so nothing it draws is
  pipeline-specific. The **3D demo scene** is a different matter: its walls,
  desk and monitor use URP materials, so in a Built-in or HDRP project those
  need their shaders re-assigned (Edit → Rendering → Materials → Convert, or by
  hand). The OS on the screen renders correctly either way.
- Full C# source, no DLLs, no third-party dependencies
- **Platforms: anywhere `System.IO` can write a folder** - Windows, macOS,
  Linux, iOS, Android, WebGL, and consoles with a writable user folder. The
  file system is real folders on disk, and a player's first run copies the
  project's content into a folder of its own so it stays writable: the user's
  Documents on the desktop, `persistentDataPath` everywhere else.
- **WebGL and Android are handled for you.** StreamingAssets on those two is a
  URL and a zip entry rather than a directory, so there is nothing to copy from.
  The editor bakes `StreamingAssets/RetroOS/` into a compressed
  `Resources/RetroOsContent.bytes` and keeps it current - on load, after any
  change under StreamingAssets, and at build time - and the first run unpacks it
  into the player's folder. That file is committed with the package, so an
  imported copy builds for either platform straight away. If it is ever missing
  the build stops with a one-line fix rather than shipping an OS with no drives;
  **Deepwave > RetroOS > Bake Content For Web** forces a rebuild by hand.
  (`RetroStorage.OverrideRoot` still points the whole system at a folder of your
  choosing if you would rather write the content in yourself.)

---

## Demo walkthrough (spoilers)

Walk to the desk, press **E**. `operator / 1997`. Read the mail and
`Documents/`. The terminal knows `ls -a`, and one word nobody tells you about
— try `override`. The memo says the archive code is *the year it happened*;
the incident report on `\\192.168.1.20` names the year; the site at
`northpoint.local` fills in the rest. Watch CAM 02.

M. HALE is on the messenger and would rather you left the archive alone.
Open it anyway and somebody else comes online - and sends you the photograph
that was in there all along.

There is a tape in `Recordings/`. Play it in the CD Player and read the
transcript beside it: **View - Lyrics** follows the words as it runs.
