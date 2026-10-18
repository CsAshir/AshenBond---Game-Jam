# Ashen Bond: Boy animation frames (PixelLab + Claude in Chrome)

## What we have now (Assets/DarkForest/Sprites)
| State  | Frames | Size per frame |
|--------|--------|----------------|
| idle   | 12     | 128 x 96       |
| run    | 17     | 128 x 96       |
| rise   | 2      | 128 x 96       |
| fall   | 2      | 128 x 96       |
| land   | 1      | 128 x 96       |
| charge | 7      | 128 x 96       |

The jump set is the weak part (2 + 2 + 1 frames), and there is no walk cycle. The boy faces **right**, stands on the
**bottom edge** of the canvas, and the canvas is transparent. Use `boy_idle_0.png` and `boy_run_0.png` as the
reference images.

**Delivery format:** PNG frames on a transparent background, same 128 x 96 canvas, feet on the same baseline as the
references, facing right, **no lantern in the hand** (the game adds the lantern). Name them like
`boy_walk_0.png ... boy_walk_7.png`, `boy_jump_0.png ...`, and drop them in `Assets/Player/NewFrames/`.

---

## 1. PixelLab prompts (pixellab.ai, "Animate with reference" / "Animate with text")

Settings for every animation:
- Reference image: `boy_idle_0.png` (style + character lock), plus `boy_run_0.png` for the run-related ones
- Canvas: 128 x 96 (or 64 x 48 and upscale x2 nearest-neighbour if the tool limits size)
- View: side view, facing right. Outline: single-colour dark outline. Shading: medium, flat. Detail: medium.
- Palette: keep the reference colours (navy tunic, red scarf, brown boots, dark hair)
- Transparent background, no ground, no shadow, no lantern

**Walk (8 frames)**
> Side-view pixel art walk cycle, 8 frames, of the same boy as the reference image: a young boy with messy dark hair,
> red scarf, navy tunic, brown belt and boots, small cape. Calm, careful walk through a dark burning village. Slight
> head bob, arms swing gently, the right hand stays half-closed near the hip as if holding something. Loopable,
> consistent proportions and colours, crisp pixel art, no anti-aliasing, transparent background, facing right.

**Jump: anticipation + take-off (3 frames)**
> Same boy, side view facing right. A 3-frame jump take-off: 1) knees bend and crouch, scarf settles; 2) pushes off
> the ground, body stretching upward, one knee up; 3) leaves the ground, arms up for balance, scarf and cape trailing
> down. Crisp pixel art, same palette and outline as the reference, transparent background.

**Jump: rise to apex (3 frames)**
> Same boy rising through the air, side view facing right, 3 frames: body tucks slightly, knees up, cape and scarf
> lifting, then a short floating apex pose with arms out. Same style, transparent background.

**Fall (3 frames, loopable)**
> Same boy falling, side view facing right, 3 looping frames: legs reach down for the landing, arms raised, cape and
> scarf flutter upward from the falling speed. Same style, transparent background.

**Land (3 frames)**
> Same boy landing, side view facing right, 3 frames: 1) feet touch down; 2) deep knee bend absorbing the impact,
> scarf swings forward; 3) recovers to the standing pose. Same style, transparent background.

**Optional: slide / dodge (5 frames)**
> Same boy doing a low ground slide to the right, side view, 5 frames: drops low, one leg forward, one hand on the
> ground with dust, cape flattened behind, then gets back up. Same style, transparent background.

**Optional: hurt (3 frames)**
> Same boy getting hit and knocked back to the left, side view facing right, 3 frames: flinch, recoil with eyes shut,
> recover. Same style, transparent background.

Tip: if a frame drifts off-model (hair or colours change), regenerate just that frame with "Inpaint" or "Edit frame",
using the previous good frame as the reference.

---

## 2. Prompt for Claude in Chrome (paste this into the Claude browser extension)

> I'm making the pixel-art game "Ashen Bond". Please help me make animation frames for my main character on
> **pixellab.ai**, in this browser tab.
>
> 1. Open https://www.pixellab.ai and stop and ask me to log in if I'm not already logged in. Don't enter any
>    password yourself.
> 2. Open the animation tool ("Animate with reference" or the closest match). Ask me to upload my reference image
>    `boy_idle_0.png` (I'll pick it from my computer: `E:\All UNITY\GAME JAM - 2026\Assets\DarkForest\Sprites\`).
> 3. Use these settings: side view, facing right, canvas 128 x 96 (or 64 x 48 if 128 isn't offered), transparent
>    background, single-colour dark outline, medium flat shading, keep the reference palette, no lantern in the hand.
> 4. Generate these animations one at a time with the prompts below. After each one, show me the result and wait
>    for me to say "ok" or "redo" before moving on:
>    - Walk, 8 frames: "calm careful walk cycle, slight head bob, arms swing gently, loopable"
>    - Jump take-off, 3 frames: "crouch, push off, leave the ground with arms up, scarf and cape trailing"
>    - Jump rise to apex, 3 frames: "rising, knees up, cape lifting, short floating apex pose"
>    - Fall, 3 frames: "falling loop, legs reaching down, arms up, cape and scarf fluttering upward"
>    - Land, 3 frames: "feet touch down, deep knee bend, recover to standing"
> 5. When I approve an animation, export it as separate PNG frames (or a horizontal sprite sheet) with transparent
>    background, and ask me before you click any download, purchase, credits or "upgrade" button.
> 6. Name the files `boy_walk_0..7`, `boy_jump_0..2`, `boy_rise_0..2`, `boy_fall_0..2`, `boy_land_0..2`.
>
> Keep the character exactly on-model: messy dark hair, red scarf, navy tunic, brown belt and boots, small cape.
> If generations start drifting, tell me and use the last good frame as the new reference.

After downloading, put the files in `Assets/Player/NewFrames/` and tell Claude Code: "new boy frames added". It will
slice them, match them to the 128 x 96 baseline and set the timing (walk ~9 fps, take-off 14 fps, fall loop 10 fps,
land 14 fps).

---

## 3. Free alternatives
- **Aseprite** (paid, or free if you compile it) / **LibreSprite** (free): draw in-betweens by hand with onion skin.
- **PixelLab inside Aseprite**: the same AI tool as a plugin, so you can edit frames right after generating them.
