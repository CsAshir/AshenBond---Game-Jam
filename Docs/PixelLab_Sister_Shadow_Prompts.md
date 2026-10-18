# Ashen Bond: the sister's shadow (PixelLab + Claude in Chrome)

## Why these frames
At the moment the sister has only **7 frames** (rise and attack) plus a single heal pose, so she looks stiff. She is a
**shadow spirit**, not a person: she should read as living smoke and ink with a faint violet edge, and she should never
be a flat black cut-out. Her height is about **1.6× the boy**, she stands behind him, and she faces right.

The boy's sprites are 128 × 96 at 30 px per unit. To keep the sister at the same pixel density, use a
**160 × 160** canvas, feet on the bottom edge, transparent background.

**Delivery:** separate PNG frames or a horizontal sheet, named `sister_<state>_<n>.png`, in
`Assets/Player/NewFrames/Sister/`. Then tell Claude Code "sister frames added".

| State     | Frames | Used for |
|-----------|--------|----------|
| idle      | 8 (loop) | hovering behind the boy, breathing smoke |
| rise      | 10 | P pressed: she pours up out of the boy's shadow |
| attack    | 10 | throws the shadow-fire orb (release on frame 6–7) |
| heal      | 10 | E pressed: kneels, cups her hands, a cyan glow spreads to the boy |
| sink      | 8 | melts back down into his shadow |
| (optional) guard | 6 | shields him with her arms / cloak (for the boss cutscene) |

---

## 1. PixelLab prompts

**Settings for all of them:** side view, facing right, 160 × 160 canvas, transparent background. Use no outline (or a
very thin dark-violet one), soft flat shading, and a limited palette: near-black indigo #0b0614, deep violet #2a1446,
violet rim #7a3cff, a little cyan #5ff0ff only for the eyes and the heal glow. Upload the reference image
`Assets/DarkForest/Sprites/shadow_3.png`, her current pose.

**Base character description** (paste this at the start of every prompt):
> A tall, slender shadow spirit of a young girl, made of drifting black smoke and ink, long hair and a ragged cloak
> that dissolve into wisps at the edges, faint violet rim light, two small glowing cyan eyes, no visible face
> details, no feet (her lower body fades into smoke), side view, facing right, pixel art.

**Idle (8 frames, loop)**
> [base] hovering in place, slow breathing motion, hair and cloak edges drifting upward like smoke, small wisps
> peeling off and fading, eyes blink once, seamless loop.

**Rise (10 frames)**
> [base] emerging from a flat pool of shadow on the ground: starts as an ink puddle, a column of smoke stretches up,
> the head and shoulders form, the cloak unfurls, ends in the full standing pose. Wisps scatter outward as she forms.

**Attack: shadow orb (10 frames)**
> [base] gathers violet shadow-fire in her right hand (frames 1–4, a growing purple-blue flame orb with pixel
> sparks), draws the arm back (5), throws forward with a whip of the cloak (6–7, the orb leaves her hand), smoke
> trails from the arm, recovers to idle (8–10).

**Heal (10 frames)**
> [base] kneels down gently behind the boy (frames 1–3), cups both hands; a soft cyan light grows between them
> (4–7) and spreads outward in a ring, her silhouette brightens slightly at the edges, then she rises back (8–10).
> Calm and protective, not aggressive.

**Sink (8 frames)**
> [base] dissolves downward: the body melts into falling smoke and ink, the eyes are the last thing to fade, ends as
> a flat shadow pool that shrinks to nothing.

**Optional: Guard (6 frames)**
> [base] spreads her cloak and arms wide in front of her as a shield, wisps flaring outward, braced against an
> impact, protective pose.

**Tip:** if a frame becomes too solid black, add "semi-transparent smoke edges, wisps" and regenerate only that frame.
Keep the cyan to the eyes and the heal glow only.

---

## 2. Prompt for the Claude Chrome extension (paste as is)

> I'm making the pixel-art game "Ashen Bond". Please help me make animation frames for the boy's protective
> **sister-shadow** on **pixellab.ai**, in this browser tab.
>
> 1. Open https://www.pixellab.ai. If I'm not logged in, stop and ask me to log in. Never type a password yourself.
> 2. Open the animation tool ("Animate with reference" or the closest match). Ask me to upload the reference image
>    `shadow_3.png` (from `E:\All UNITY\GAME JAM - 2026\Assets\DarkForest\Sprites\`).
> 3. Settings: side view, facing right, canvas 160 × 160 (or the closest size offered), transparent background,
>    no outline or a very thin dark-violet one, soft flat shading, palette near-black indigo, deep violet,
>    violet rim, cyan only for the eyes and the heal glow.
> 4. Every prompt starts with: "A tall, slender shadow spirit of a young girl, made of drifting black smoke and ink,
>    long hair and a ragged cloak that dissolve into wisps, faint violet rim light, two small glowing cyan eyes, no
>    face details, lower body fades into smoke, side view facing right, pixel art."
> 5. Generate these one at a time. After each, show me the result and wait for "ok" or "redo":
>    - idle, 8 frames: "hovering, slow breathing, hair and cloak drifting like smoke, seamless loop"
>    - rise, 10 frames: "emerging from an ink puddle on the ground into the full standing pose"
>    - attack, 10 frames: "gathers a violet shadow-fire orb in her right hand, throws it forward, recovers"
>    - heal, 10 frames: "kneels, cups her hands, soft cyan light spreads outward in a ring, rises again"
>    - sink, 8 frames: "melts down into smoke and ink, eyes fade last, ends as a shrinking shadow pool"
> 6. When I approve one, export it as PNG frames or a horizontal sprite sheet with a transparent background. Ask me
>    before clicking any download, purchase, credits or upgrade button.
> 7. Name the files `sister_idle_0..7`, `sister_rise_0..9`, `sister_attack_0..9`, `sister_heal_0..9`,
>    `sister_sink_0..7`.
>
> Keep her consistent between animations (same height, same eye position, same violet rim). If she drifts off-model,
> tell me and use the last good frame as the new reference.
