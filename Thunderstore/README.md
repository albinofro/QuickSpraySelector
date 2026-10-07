# QuickSpraySelector

Created by **Stooo**.

Replaces the Graffiti Minigame with a weapon wheel style selector. Press cancel to spray last used piece.

The screen may hang for a second on the first spray after launching the game.

## Features

- A wheel of collected graffiti, filtered to the current spot's size, with a large artwork preview in the middle.
- Aiming with either analog stick or the mouse, D-pad and keyboard selection, and twelve artworks per page.
- Button helpers follow the game's bindings and current input device.
- Remembers the last successfully sprayed design separately for medium, large and extra-large graffiti.
- Randomly keeps the original character pose or holds one of eight directional poses while the selector is open.
- Keeps the native yellow dots and opening hit-pause, while the larger paint cloud moves slowly and clears away instead of freezing over the wall.
- The native artwork reveal, camera sequence and rewards remain intact.
- Flowing green-and-cream paint, responsive pointing and artwork magnification.
- A smaller field of native yellow dots drifts briefly, then stays suspended over the selector and sharp while the background blurs.
- Random spray-can lid sounds have a maximum of three simultaneous voices.
- Subtle, irregular shoulder-camera sway adds a natural handheld feel to the orbit.
- Gentle circular camera drift with a 50/50 clockwise or counterclockwise start, a subtle shake and a smooth medium-strength orbital pull toward highlighted artwork: left pulls clockwise and right pulls counterclockwise. The wheel stays steady.
- Native background blur waits ten seconds, then fades in over five seconds: linear for the first four seconds and easing out in the final second. Artwork and button hints stay sharp.
- Random spray-can lid sounds when the highlighted artwork changes.

## Controls

Aim either stick or move the mouse around the wheel to choose an artwork. D-pad left/right or the game's keyboard horizontal-menu bindings step through designs. Use the displayed page buttons to browse pages, then confirm to spray. Mouse controls also support left-click to spray, right-click to use the last piece (or safely exit if none is available), and scrolling to change pages. The system mouse cursor stays hidden; the wheel arrow shows your aim.

Cancel sprays the last-used piece for the current size. If no saved piece is available, cancel safely exits without painting. Simply highlighting a piece does not change the remembered design. Small character tags keep their usual behavior.

## Installation

Install through Thunderstore Mod Manager or r2modman with BepInEx enabled, then launch the game using the manager's modded launch option.

For manual installation, install BepInEx for Bomb Rush Cyberfunk and copy the package's `plugins/QuickSpraySelector` folder into `BepInEx/plugins`.

Disable other mods that replace the graffiti minigame before enabling this one; overlapping patches can conflict.

## Settings

The existing configuration identifier is retained for compatibility: `BepInEx/config/codex.quickpickgraffiti.cfg`. The `LastGraffiti` section stores remembered artwork for each size.

Under `Graphics`, `NativeBlurQuality` defaults to `Medium`; use `Small` for less GPU work. Set `NativeBlurEnabled = false` to remove the blur cost entirely. The liquid uses a capped render target and lighter geometry without reducing its animation update rate.

## Artwork and license

The icon is a close-up of Graffo Le Fou by Greunouylle. The arrow comes from the game's dance selector. Game artwork belongs to its respective creators and is separate from this project's code license.

The mod's source code is licensed under MIT. See `LICENSE`.
