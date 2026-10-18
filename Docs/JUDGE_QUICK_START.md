# Ashen Bond — Judge Quick Start

**Fastest route:** download the repository ZIP, extract it, open `Play/Windows`, and run `GAME JAM - 2026.exe`. Unity is not required for the Windows executable.

## Download and play

1. Visit [Ashen Bond on GitHub](https://github.com/CsAshir/AshenBond---Game-Jam).
2. Select **Code → Download ZIP**.
3. Extract the ZIP completely.
4. Open `Play/Windows` and launch `GAME JAM - 2026.exe`.
5. Select **Start Game → Easy** for a first playthrough.
6. Use **How to Play** from the menu whenever you need the control reference.

The repository contains both the Unity source and the existing Windows build. Keep every file in the Windows folder together. The executable depends on its adjacent data and runtime files.

## Clone and play

With Git installed:

```sh
git clone https://github.com/CsAshir/AshenBond---Game-Jam.git
cd AshenBond---Game-Jam
```

Then open `Play/Windows` in your file browser and run `GAME JAM - 2026.exe`. No build command is necessary.

To update an existing clone, open a terminal at the repository root and run:

```sh
git pull
```

If you edited files locally, preserve those edits before updating. `git pull` updates an existing clone; the first download uses `git clone`.

## First-play checklist

- Move with **A/D**; hold **Shift** to run.
- Press **Space** twice to double jump.
- Fire with **J**, then use **P** while an enemy glows cyan.
- Dash with **K** to evade danger.
- Heal with **E** when you have at least two embers and a missing heart.
- Hold **Q** to rewind while charges remain.
- Use **Esc** to pause; use the on-screen dialogue prompts to continue the story.

## Review the Unity source

Use **Unity 6000.3.8f1**. Add the repository root through Unity Hub, wait for the initial import, and open:

```text
Assets/DarkForest/Scenes/MainMenu.unity
```

Press Play in the editor. The source includes the opening cinematic, three playable areas, and the ending cinematic in the configured scene list.

| Source area | Location |
|---|---|
| Main menu and entry point | `Assets/DarkForest/Scenes/MainMenu.unity` |
| Three environments | `Assets/DarkForest/Scenes/AreaA_BurningVillage.unity`, `AreaB_DarkForest.unity`, `AreaC_ClockTower.unity` |
| Gameplay scripts | `Assets/DarkForest/Scripts` |
| Scene generation tools | `Assets/DarkForest/Editor` |
| Package definitions | `Packages` |
| Unity project configuration | `ProjectSettings` |

## Build provenance and verification

The Windows executable is copied from the developer-created `final build` output, dated **18 October 2026, 06:38 PKT**. No new build was produced for this submission preparation. Runtime files were packaged together and debug-only `DoNotShip` output was excluded. Packaging checks are not a substitute for an end-to-end playtest.

## Trouble getting started?

| Problem | What to check |
|---|---|
| Missing `UnityPlayer.dll` or missing game data | Extract/download the entire Windows folder, not just the `.exe`. |
| Game launched directly from a ZIP | Extract first, then launch from the extracted folder. |
| Source opened in the wrong Unity version | Use `6000.3.8f1` for the supplied source. |
| Unity is importing for a long time | Wait for the first import and package restoration to finish. |
| Editor starts in the middle of a level | Open `MainMenu.unity`, then press Play. |
| Need to report a bug | Open a [GitHub issue](https://github.com/CsAshir/AshenBond---Game-Jam/issues) with the area, steps, expected result, actual result, and a screenshot if available. |

For mechanics and deeper detail, read the [game guide](GAME_GUIDE.md).
