# Changelog

All notable changes to RetroOS. Dates are the day the work landed.

## [1.1.0] - 2026-09-08

The interface is drawn a different way, the machine is really a machine, and
every program grew up. This release **replaces** the 1.0 world-space-canvas
path and the `RetroOsProfile` asset - see *Migrating from 1.0*.

### How the interface is drawn

- **One canvas, drawn once, projected onto its screen.** The whole OS renders
  flat into a `RenderTexture`; that texture *is* the machine's picture and the
  OS stops there. Assign it to a material, a `RawImage`, or six `RawImage`s for
  a wall of monitors - the OS never touches your materials and asks nothing of
  the surface. Sitting down flips to a pixel-exact screen-space overlay.
- **CRT curvature is gone.** `ScreenCurvature`, `RetroScreenWarp`,
  `RetroCurveEffect` and the Control Panel *Display* tab are removed; a 3D
  bezel and an emissive glass material carry the tube look now.
- **On-screen cursor mode.** `RetroMonitor > Interaction` picks between the
  fullscreen overlay and a drawn cursor that is *walked* across the glass by
  relative input - mouse delta, gamepad stick or a dragging finger - with
  every click, drag, scroll and keystroke routed through a private pointer
  device while the real devices stay free for the game.
- **Windows are a host and a frame**, open where they sit, resize from a
  pivoted corner and drag by tracking the pointer. Every transient surface -
  dialogs, menus, fly-outs, tool-tips, tray notices - animates through one
  component, `RetroPopup`, and stops taking clicks the moment it folds.
- **Scroll bars.** There were none; now the three-part bar of the period, on
  every scroll area, hiding itself when the content fits.
- **Whole-number interface scaling** (`pixelPerfect`, on by default) so a
  one-pixel border is one pixel along its whole length.
- **Fourteen period colour schemes** applied to interface already on screen,
  and title bars that draw the gradient the theme always promised.
- **Side panes fold away** - Inbox trays, messenger contacts, media playlist,
  Explorer's tree - animating their width, each remembering its state.

### The machine is one object

- **`RetroOsProfile` is deleted.** Accounts, logon and the BIOS screen moved to
  `RetroMachine`; the system name, theme, wallpaper, sounds and story rules to
  `RetroOsBootstrap`; the data folder, the in-fiction date and the LAN to the
  new `RetroWorld`. Every setting now sits on the thing it describes.
- **New `RetroMonitor`** holds the resolution, the picture, the work area and
  the pointer mode, and lives beside the bootstrap - the screen is part of the
  computer, not a reference into the furniture.
- **New `RetroInternet`** is the line to the outside world with one switch:
  off means every site is unreachable at once while the LAN carries on.
- **A machine you switch on.** `Start Powered On` off builds the whole computer
  and leaves the monitor dark until `PowerOn()`; `os.boot` waits for that
  moment. A machine that is off drops out of Network Neighborhood.
- **Two computers in one scene** no longer share one palette, one storage root,
  one pointer or one projector bay - each is owned by somebody now, and the
  sample scene is two real machines.

### Sharing, and shutting down

- **A share is a property of the folder.** `RetroShare` is gone; a folder
  publishes itself with `share:` in its own `.rmeta`, beside its password and
  owner, so a folder copied to another machine arrives shared. The player can
  publish and unpublish from Explorer's right-click **Sharing...**.
- **One shut-down behaviour.** `ShutdownAction` is gone. `RequestLogout()` and
  `RequestShutdown()` ask the player and run the era's tear-down and
  please-wait screens; `Logout()` and `Shutdown()` still go straight through
  for a story rule. Both refuse, and say why, on a machine already down.

### Programs

- **New: Calculator, Reminders, Messenger, Notepad, Control Panel**, and the
  five games rebuilt as real programs with a move back where it fits.
- **Messenger** runs scripted conversations - a line can pause, wait for the
  player to choose a reply, or send a file; the conversation survives closing
  the window and is dropped on log off.
- **CD Player**: transport, seek, shuffle, and timed lyrics from a `.lrc`
  beside the track. **Media Player**: playlist, speed. **Photo Gallery**:
  arrow-key paging and set-as-wallpaper. **Image Viewer**: zoom, fit, rotate,
  pan.
- **Inbox**: Inbox / Sent / Deleted trays, attachments that open in the right
  program, a composer. **Notepad**: New, Open, Save, Save As, Find over the
  machine's disk. **Explorer**: create, rename, move, copy and delete for real.
- **The browser**: pages on folder tabs each with their own history, a
  bookmarks window, bookmarks authored in the inspector, Find, View Source,
  Document Info, Save As to the disk, and a load time that makes Stop mean
  something.
- **Terminal**: history, Tab completion, typewriter output, and commands the
  game authors in the inspector.
- Programs group into Start-menu categories.

### The rest of the OS

- **It remembers** - colour scheme, wallpaper, clock format, folded panes,
  every desktop icon's position and the last user - in one text file beside the
  drives. **Control Panel > System > Forget Settings** puts it all back.
- **Tray notices** (`RetroNotification`) instead of modal interrupts; a
  start-up screen before the logon prompt; **Print** writes to the user's
  Printouts folder and raises `printer.job`.
- **New events**: `mail.sent`, `mail.deleted`, `reminder.due`, `printer.job`,
  `chat.message`, `chat.said`. **New rule actions**: `Notify`, `HostOnline`,
  `HostOffline`, `ChatOnline`, `ChatOffline`, `Bsod`, `LogOff`, `Shutdown`.
- **Five sample web sites** where there was one - a bookshop, a hand-kept
  directory, a search engine, a free-space community and an auction list, all
  cross-linked. Visited links are purple.

### Icons and the editor

- **The main components have icons.** The nine a scene is built from wear their
  own pixel-art glyph in the Inspector and the Hierarchy, drawn for Unity's two
  skins rather than borrowed from the desktop set.
- **Icons paint at the size they are drawn** - 67 grew to 71, ten that did not
  read were redrawn, twelve gained the dark outline they needed on a silver
  toolbar, and cutting a hole in one finally works.
- **Editor menus moved to `Deepwave`.** `Deepwave > RetroOS > Setup` and
  `Icon Browser` are the only entries; the self test, the smoke run and the
  contact sheet are buttons in Setup and command-line entry points for CI.
- **WebGL and Android have files.** The editor bakes `StreamingAssets/RetroOS/`
  into a compressed `Resources/RetroOsContent.bytes` and keeps it current; the
  first run unpacks it into the player's own folder, since `StreamingAssets` on
  those two platforms is a URL and a zip entry, not a directory.
- **Inspectors audited** - a section that throws no longer breaks the drawer,
  lists have one disclosure arrow, drawers measure what they draw, disk and
  scene reads are cached, and the settings that moved got their own inspectors.

### Fixed

Dozens, most found by reading the code while the editor held the project open.
Among them: clicks landing four pixels above the control they aimed at; a
submenu that shut the menu it came from; a selected row invisible until
something hovered it; High Contrast Black writing white text on white paper;
the taskbar button showing half a word; a browser added to a scene opening on
"the page cannot be displayed"; a reminder that stopped when its window closed;
Save As treating a typed path as a name; the palette book growing for as long
as the game ran.

### Tests

- **Offline logic suite** (`Tests~/RetroOS.LogicTests.csproj`, `dotnet run`) -
  the calculator, the mailbox, the clock, the disk operations and their name
  and containment rules, the `.rmeta` sidecars, path resolution and per-user
  privacy, the event bus, the terminal shell, the lyric reader, the HTML
  parser, the CSS engine and the story rules.
- **Self test** - the HTML and CSS engines, the icon painter, folder mounts,
  address resolution, path-traversal and per-user privacy, and the sidecar
  walk that turns folders into a network.
- **Smoke run** - boots the OS, drives the logon, opens every program,
  repaints in three colour schemes and maximizes, minimizes and folds a pane,
  writing PNGs to `SmokeShots/`.
- `Tests~/IconSheet` and `Tests~/SiteArt` paint the icon set and the sample
  sites' display type to contact sheets without Unity.
- `Docs/UI_STYLE_GUIDE.md` is the standard the interface is held to.

### Migrating from 1.0

| 1.0 | 1.1 |
| --- | --- |
| `RetroOsProfile` asset | gone - settings live on `RetroMachine`, `RetroOsBootstrap` and the new `RetroWorld` |
| `DisplayMode`, `targetCanvas`, `screenAnchor`, `screenWorldSize`, `worldCamera` | gone - the OS renders to a `RenderTexture` on the new `RetroMonitor`; drop that asset into your screen material |
| `RetroShare` component | `share:` (with `sharecomment` / `sharepassword` / `sharehidden`) in the folder's own `.rmeta` |
| `ShutdownAction` field + enum | one behaviour; `RequestShutdown()` / `RequestLogout()` to ask, `Shutdown()` / `Logout()` to go straight through |
| `ScreenCurvature`, `RetroScreenWarp`, `RetroCurveEffect` | gone - a 3D bezel and an emissive glass material |
| `RetroAppDefinition` asset, `PrefabAppDefinition` | `RetroAppBehaviour` component, `RetroPrefabApp` |
| `Tools > RetroOS` menu | `Deepwave > RetroOS` |
| `startHidden` | `startFocused` |
| `DragMove`, `ResizeGrip` | `WindowDragger`, `WindowResizeAnchor` |

## [1.0.0] - 2026-07-07

First release. Windows, a desktop and a taskbar; a file system mounted from
real folders on disk with `.rmeta` sidecars and per-user privacy; a LAN of
machines that have to be switched on; a browser that renders your own HTML and
CSS; CCTV wired to live scene cameras; five period games; 67 icons painted at
runtime; CRT curvature; and an event for everything the player does.
