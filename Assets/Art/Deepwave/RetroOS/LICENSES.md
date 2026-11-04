# Licences and attribution

## This package

All source code, artwork and content in RetroOS is original work by Deepwave,
licensed to you under the Unity Asset Store EULA (Extension Asset).

There are **no third-party dependencies**, no bundled DLLs, and no
redistributed fonts, textures or audio. Every icon is painted at runtime from
code in `Runtime/UI/Icons`; every interaction sound is synthesized at runtime
in `Runtime/Audio/RetroSfxSynth.cs`.

## The games

The five games implement rulesets that are in the public domain. The code,
artwork, layout and naming here are original.

| Program | Ruleset | Status |
|---|---|---|
| Mines | Mine-clearing grid puzzle | Game mechanic, not a protected work. Implemented from scratch |
| Solitaire | Klondike | Traditional card game, public domain |
| FreeCell | FreeCell | Traditional card game, public domain |
| Reversi | Reversi (1883) | Public domain. Deliberately **not** called Othello, which is a trademark |
| Snake | Snake (1976 arcade mechanic) | Game mechanic, not a protected work |

Card faces are drawn from code, not scanned from a deck.

## Trademarks

RetroOS is an original work inspired by the visual conventions of 1990s
desktop operating systems. It is not affiliated with, endorsed by, or derived
from any product of Microsoft Corporation or any other company.

- No Microsoft trademark, logo, wordmark, sound or icon is included.
- "Windows 95-style" appears in the documentation only as a descriptive
  comparison of visual era, which is nominative fair use.
- Do not ship a game that reproduces a real operating system's logo,
  boot sound, wordmark or icon set using this package. Use the built-in icons
  and the theme system, or make your own.

## Sample content

The names, places, dates and documents in the sample content
(`StreamingAssets/RetroOS`) and the demo scene are fiction. Any resemblance to
a real person, company, facility, IP address or domain is unintentional.

The addresses used are inside reserved ranges on purpose:
`192.168.1.20` is RFC 1918 private space, and `.local` is reserved by RFC 6762.

## Fonts

The interface uses Unity's built-in legacy runtime font and, for monospace,
whichever of Consolas / Courier New / Lucida Console the player's operating
system provides. Nothing is redistributed. Assign your own fonts on a
`RetroTheme` asset.
