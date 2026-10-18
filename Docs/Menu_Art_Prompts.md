# Ashen Bond: art prompts for the menus, loading cards and missing sprites

**Palette (use it in every prompt):**
- Ember orange #FF9A3C and hot core #FFE3A0
- Shadow navy #0B0614 and deep violet #2A1446
- Mira cyan #5FF0FF (her eyes and glow only)
- Dark navy outlines, never pure black

**Story to keep in mind:** Ilyan, a small boy with a brass lantern, flees his burning mountain village. The shadow
of his older sister Mira protects him. The clock tower's clock is frozen at 11:59.

**Where to put the files once they're ready:** `Assets/Art/Menu/`. Then tell Claude Code "menu art added". The
`MainMenu` component already has `panelArt` and `logo` slots, and the art will be wired into them.

---

## 1. Side panel art (left third of the title screen), 640 × 1080, vertical
> Vertical pixel-art panel, dark and painterly, for a sad cinematic platformer. Tall, slim composition. At the
> bottom, a small boy in a dark cloak and red scarf holds up a glowing brass lantern. His warm orange light
> spills upward. Behind and above him rises the huge, soft silhouette of a girl made of smoke and ink, long
> hair drifting upward, faint violet rim light, two small cyan eyes, one protective hand curved over the boy.
> Embers float through the whole panel. The top 40% is near-black navy smoke (space for the title). The
> right edge fades to transparent. No text, no logo, no border. 32-bit pixel art, limited palette: ember
> orange, navy, violet, a touch of cyan.

## 2. Key art / title background (full screen), 1920 × 1080
> Wide pixel-art key art. A burning mountain village at night under a huge pale moon. Orange fires eat the
> rooftops, smoke drifts into a navy sky. In the far distance, on a hill, stands a dark gothic clock tower with
> a pale clock face stopped at 11:59. Right third: the boy Ilyan from behind, small, lantern raised. A giant
> shadow-girl spirit rises from his shadow on the ground and curls around him like a cloak. Left half: calm,
> dark, low-detail smoke (the menu goes there). Cinematic, melancholic, Hollow Knight / Limbo mood.
> No text.

## 3. Logo "ASHEN BOND", transparent PNG, about 1200 × 400
> Pixel-art game logo reading "ASHEN BOND". Heavy, slightly worn serif letters made of charred dark metal with
> glowing ember cracks inside them (orange-gold). A thin cyan wisp of smoke curls from the "O" like a
> spirit's hair. Small embers drift off the top edge. Transparent background, crisp pixel edges, no other
> text.

## 4. Loading cards (one per area), 1920 × 1080, mostly dark
- **Area A:** a close-up of a brass lantern on scorched floorboards, flames reflected in its glass, a child's
  hand reaching in from the left, fire glow from off-screen. Dark edges, centre-weighted.
- **Area B:** a misty night forest with moonbeams through the canopy and green fireflies. A small figure with a
  lantern walks a path, and a tall shadow follows on the trees beside him.
- **Area C:** inside a clock tower, giant brass gears frozen, dust hanging still in a beam of moonlight, a huge
  clock face reading 11:59 seen from behind the glass.

> Common suffix: "pixel art, cinematic, limited palette (ember orange, navy, violet, cyan accent), dark
> vignette, empty lower third for text, no text."

## 5. Button frame / panel trim (optional, 9-slice), 96 × 32 and 64 × 64
> Pixel-art UI frame, thin charred-iron border with a tiny ember glow at the corners, transparent centre,
> dark navy outline, 9-slice friendly (plain straight edges), no text.

## 6. Ilyan: slide and dash frames (he has none yet)
Use the same 128 × 96 canvas, side view facing right, and the same character as the existing PixelLab
walk/jump frames. Upload `boy_ref_128x96.png` as the reference.
- **Slide (8 frames):** "the boy drops low and slides forward on one knee, cloak flying back, lantern held
  close to his chest, dust kicking up, then rises back to run."
- **Dash (6 frames):** "a fast forward dash: body leaning hard, cloak and scarf streaming straight back,
  motion-blur smear frames 2–4, lantern trailing a short flame streak, lands into a run."

Name them `boy_slide_0..7` and `boy_dash_0..5`, and put them in `Assets/Player/NewFrames/`. Until they exist,
the dash uses his run frame plus violet after-images and the dash streak effect.

## 7. Rewind (TIME) HUD element: a pocket watch, transparent PNG, 256 × 256
Right now the HUD draws a simple pixel watch for the rewind. Generate this to replace it:
> Pixel-art game UI icon of an antique brass pocket watch seen from the front, slightly worn, ornate rim with
> small engraved gears, a crown and ring at the top. The glass face is deep navy with faint constellation dots
> and 12 thin gold hour marks; NO clock hands (the game draws a moving hand on top). Around the rim, a soft
> amber-gold glow and a few tiny embers, like time burning. Three small empty sockets under the watch for
> hourglass gems. Transparent background, crisp 1-pixel dark navy outline, 32-bit pixel art, centred, no
> text.

Also make a matching **charge gem** (64 × 64, transparent): "a tiny glowing hourglass gem, amber sand inside,
pixel art, dark navy outline". Make two versions: full (bright gold) and empty (dark, glass only).

Save them as `ui_rewind_watch.png` and `ui_rewind_gem_full.png` / `ui_rewind_gem_empty.png` in `Assets/Art/UI/`.
The HUD's `rewindDialArt` slot takes the watch.
