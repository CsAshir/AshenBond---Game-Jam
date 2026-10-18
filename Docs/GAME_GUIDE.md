# Ashen Bond — Game Guide

> Carry the light. She never left.

## At a glance

| | |
|---|---|
| Genre | Dark fantasy 2D action platformer |
| Players | Single player |
| Playable package | Existing Windows standalone build |
| Engine | Unity 6, `6000.3.8f1` |
| Presentation | Pixel art, 2D lighting, parallax environments, dialogue, and cinematics |
| Journey | Burning Village → Dark Forest → Clock Tower |

This guide describes the checked source. The submission includes the developer's final Windows build; see the build provenance note in the [judge quick start](JUDGE_QUICK_START.md).

## The story — without the ending

The village is burning. The great bell has stopped. Ilyan carries a lantern toward the clock tower, following a promise he made to his older sister, Mira. A shadow rises beside him: frightening at first, then familiar, then protective.

Mira's presence turns the lantern into more than a weapon. Fire reveals enemies; her shadow finishes what it reveals. Along the way, memories and conversations uncover what happened to the village and why she returned. The full dialogue script includes spoilers and production notes, so it is best read after playing.

## The journey

### 1. The Burning Village

Fire and drifting ash fill the first environment. This area introduces movement, lantern combat, Mira's strike, and traversal through damaged terrain. Broken crossings and arena encounters encourage the player to use the environment and the two-part combat combo together.

### 2. The Dark Forest

The palette cools to moonlight and deep shadow. Fog, fireflies, black water, and enemy ambushes change the pace. Watch for interactive dead-tree crossings. The forest builds toward the Executioner's clearing while revealing more of Ilyan and Mira's relationship.

### 3. The Clock Tower

The path climbs through gears, hanging chains, pendulums, moving platforms, and collapsing sections. Timing and route reading become central. Rewind is particularly useful around moving hazards, while the tower's encounters bring the journey toward its ending.

## Movement and survival

**Walk and sprint.** Use A/D or the arrow keys. Hold Shift for faster movement when there is enough ground ahead.

**Double jump.** Jump once, then press jump again in the air. Use the second jump to correct your route rather than spending it immediately every time.

**Dash.** K or right mouse activates the dash. The current damage checks protect Ilyan while he is dashing. Treat it as a timed escape through danger.

**Checkpoints.** Hazards can send Ilyan back to the active checkpoint while removing a heart. Enemy damage normally causes knockback and a brief protection window; losing the final heart triggers the chance-and-respawn logic.

## Combat: fire, then shadow

1. **Fire the lantern with J.** Hold J to prepare a charged flare.
2. **Watch for the cyan glow.** Lantern fire marks an enemy for a short combo opportunity.
3. **Call Mira with P.** A shadow strike against a marked target deals triple damage.
4. **Manage the recharge.** Mira's strike recharges over seven seconds in the current source. The HUD indicates readiness.

Defeated enemies release resources into the lantern. Use the time between strikes to reposition, dodge attacks, and choose the next target.

## Healing and embers

Press **E** to ask Mira to restore one heart. Healing costs **two embers** and requires a missing heart. It is unavailable when the relevant ability conditions are not met; the HUD can show a hint explaining why it did not start.

Healing is a resource decision: restore health before the next dangerous section when you have the embers to spend.

## Rewinding time

Hold **Q** to play recorded action backwards; release to resume from that moment. The source provides **three rewind charges**, with up to **three seconds** of history per use when enough history has been recorded.

Rewind affects the player and recorded world objects such as enemies, projectiles, moving platforms, pendulums, and other supported hazards. Visual effects distinguish rewind from normal play.

Two rules matter:

- A defeated enemy stays defeated.
- Respawning clears the recorded history, so rewind cannot recover the timeline from before a pit respawn.

Rewind is a limited tactical resource, separate from hearts and lives.

## Hearts, lives, and difficulty

Ilyan has **three hearts** in each of the three supplied area scenes. Hearts represent current health; chances represent how many full health losses the run can withstand.

| Difficulty | Chances at the start of a run |
|---|---:|
| Easy | 10 |
| Medium | 5 |
| Hard | 3 |

When hearts reach zero, one chance is spent. If chances remain, health refills and Ilyan returns to the checkpoint. When the chance counter reaches zero, the **Game Over** panel pauses the game.

**Restart** reloads the current area with a fresh set of chances for the selected difficulty. **Give Up** returns to the main menu. Starting a new game also resets the chance counter.

## Complete control reference

| Action | Keyboard / mouse | Controller input supported in source |
|---|---|---|
| Move | A/D or Left/Right | Left stick or D-pad |
| Run | Hold either Shift | Hold either shoulder button or press left stick |
| Jump / double jump | Space, W, or Up | South face button |
| Dash | K or right mouse | Right trigger |
| Return to checkpoint manually | R | Keyboard R |
| Lantern attack / charge | J or left mouse; hold to charge, release to fire | East face button; hold to charge |
| Mira's shadow strike | P | West face button |
| Heal | E | North face button |
| Rewind | Hold Q | Hold left trigger |
| Pause | Esc | Start |
| Advance dialogue | Space, Enter, J, or left mouse | South face button |
| Skip cinematic | Esc, Space, or Enter | Start or South face button |

Controller face-button positions are used because printed button names differ between devices. Keyboard controls are the clearest reference for judging. Controller mappings listed here were inspected in source, not verified across physical controllers.

## A useful first session

1. Start on Easy and watch the opening for the story setup.
2. Practice a double jump and a dash before committing to a long crossing.
3. Try the lantern → cyan glow → Mira combo on an early enemy.
4. Watch the HUD for strike readiness, health, and resources.
5. Test a short rewind while standing somewhere safe.
6. Look for environmental solutions at broken crossings.
7. Continue through the areas from the main-menu flow to keep the intended story order.

## Presentation and production

The project combines pixel-art environments with layered parallax, animated hazards, particles, dynamic 2D lighting, and cinematic sequences. The credits identify the team, third-party art sources, the Pixelify Sans font, and AI-generated artwork and cinematic production.

The repository includes source scripts and editor tools used to construct the environments. Those tools are for development; judges can play the existing executable without running scene builders or compiling a new build.

## Technical handover

The Unity project lives at the repository root. The required source folders are `Assets`, `Packages`, and `ProjectSettings`; `.meta` files must stay with their assets. Unity regenerates `Library`, `Temp`, and other local cache folders after the source is opened.

The Windows package lives separately under `Play/Windows`. Keep that whole folder together when copying or sharing it. The original debug-only build output is not required to play.

No measured minimum hardware specification or full compatibility matrix is claimed here. The supplied package is a Windows executable; macOS and Linux standalone packages are not included.

## Feedback and bug reports

Use the repository's [Issues page](https://github.com/CsAshir/AshenBond---Game-Jam/issues). Include:

- The build or source version you used.
- Your operating system and whether you used keyboard or controller.
- The area and checkpoint where the problem occurred.
- Steps that reproduce it.
- What you expected and what happened instead.
- A screenshot, short recording, or relevant error message when available.

Please avoid story spoilers in issue titles.

## Further reading

- [Judge quick start](JUDGE_QUICK_START.md)
- [Team and third-party credits](CREDITS.md)
- [Dialogue script](Dialogue_Script.md) — spoilers and production direction

