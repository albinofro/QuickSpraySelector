# QuickSpraySelector

Replaces the Graffiti Minigame with a weapon wheel style selector. Press cancel to spray last used piece.

- Supports controller and Keyboard+mouse
- Uses the original minigame animations
- Blur quality is configurable

The screen may hang for a second on the first spray after launching the game.

![Graffiti selection wheel](https://raw.githubusercontent.com/albinofro/QuickSpraySelector/main/assets/bomb-rush-0741-to-9s.gif)

![Spraying graffiti in gameplay](https://raw.githubusercontent.com/albinofro/QuickSpraySelector/main/assets/spray-line-github-optimized.gif)

## Installation

Install through Thunderstore Mod Manager or r2modman with BepInEx, then launch modded. Disable other mods that replace the graffiti minigame.

## Blur settings

In `BepInEx/config/codex.quickpickgraffiti.cfg`, under `Graphics`, use `NativeBlurQuality = Small` for less GPU work, or `NativeBlurEnabled = false` to turn blur off.

Created by **Stooo**. Icon: Graffo Le Fou by Greunouylle. The pointer comes from the game’s dance selector. Game artwork belongs to its creators; mod code is [MIT licensed](https://github.com/albinofro/QuickSpraySelector/blob/main/LICENSE).
