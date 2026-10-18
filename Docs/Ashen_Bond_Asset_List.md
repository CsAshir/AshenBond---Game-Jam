# Ashen Bond: Effects, Sounds and Art Prompts (Areas A → C, up to the Clock Tower)

The lantern work (moving the animated lamp from the boy's hand to its own object) is the next task. Nothing in Unity is changed by this list.

---

## 1. Free tools

| Need | Free tools |
|---|---|
| Sprite / character generation | Google Gemini or ImageFX, Bing Image Creator (DALL·E 3), Leonardo.ai (free daily credits), Ideogram (free tier), PixelLab (free trial, made for pixel art) |
| Pixel clean-up and animation | Pixelorama, LibreSprite, Piskel (browser) |
| Background removal / edits | Photopea (browser Photoshop), remove.bg |
| Particle / VFX textures | Unity Particle System and Shader Graph (already in the project); itch.io free pixel VFX packs (check each licence) |
| Music | Suno or Udio (free credits), BeepBox (chiptune, browser), LMMS (desktop DAW), Pixabay Music, Incompetech (Kevin MacLeod, CC-BY: credit required) |
| Sound effects | ElevenLabs Sound Effects (free tier, text-to-SFX), jsfxr / Bfxr / ChipTone (retro SFX), Freesound.org, Pixabay Sound Effects, Kenney audio packs (CC0) |
| Audio editing | Audacity |

Read every licence before submitting to the jam. Prefer CC0; credit CC-BY assets on the end card.

---

## 2. Visual effects list

### Player and sister (all areas)
| # | Effect | Status |
|---|---|---|
| 1 | Lantern glow + flicker (light + halo, intensity follows Flame) | In game; moves to the new animated lamp |
| 2 | Ember Shot projectile (small fire bolt + trail) | In game |
| 3 | **Charged Flare fireball**: full projectile loop + impact | **Needs new art**: the current sprite is cut in half (prompt 4.6) |
| 4 | Charge-up ring around the hand (white-hot) | In the attack sheet; regenerate without the lamp |
| 5 | Fire hit spark on walls / enemies | In game (particles) |
| 6 | **Player hit / hurt**: red-white flash, knock-back sparks, short screen shake | Shake + flash in game; **hurt animation needed** (prompt 4.5) |
| 7 | **Jump dust** (take-off puff) + **landing dust** (2-side puff) | **Needed** (prompt 4.8) |
| 8 | **Slide dust trail** + sparks under the feet | **Needed** (prompts 4.4 + 4.8) |
| 9 | Fire Dash: orange afterimages + blue shadow echo | Needed (GDD) |
| 10 | Neon rim glow on the boy (amber / cyan / orange) | In game |
| 11 | Sister Shadow Rise from the boy's feet | In game |
| 12 | Shadow orb + purple-blue fire impact | In game |
| 13 | Shadow Strike blue crescent + hit-stop | Needed (GDD) |
| 14 | Cyan "exposed" marker above an enemy (1.2 s) | Needed (GDD) |
| 15 | Heal: ground magic circle, rising cyan motes, cyan flash | In game |
| 16 | Ember pickup sparkle | In game |
| 17 | Checkpoint altar ignite (blue → amber) | In game |
| 18 | Respawn: dissolve out at hazard, embers in at checkpoint | Needed |
| 19 | Memory flash: desaturate + burned-paper dissolve (0.35 s) | Needed (Area B) |

### Area A: Burning Village
Falling ash · rising embers · roof fires · fire pits · fire jets (warning sputter → column) · falling burning beam + impact dust · smoke puffs in the background · heat shimmer over fires · burning foreground pines · thorn hit sparks · cyan spirit-platform glow · collapsed-gate fire + blue forest glow.

### Area B: Dark Forest
Low fog strips · blue fireflies · drifting ash and leaves · fire jet across the lane · falling branch (jump cue) + impact · shallow-water ripples and reflections · Ember Hound flame tail, telegraph glow, rush streak, exposed white-hot cracks, dissolve death · Ember Bloom pulse + orange motes into the lantern (heal) · glowing roots · distant warm lantern points · Memory flash 1 and 2.

### Area C: Clock Tower approach
Gear grinding sparks · dust falling from the ceiling · steam vents · swinging chains (spark on impact) · moving gear platform glow edges · collapsing bridge (cracks, falling stones, dust burst) · bell-strike shockwave ring + camera shake · wind streaks · lantern weakening (dimmer light, sputter, colour fades orange → red) · Flame-drain warning pulse on the HUD · frozen clock face glow · arena-door checkpoint ignite.

---

## 3. Sound list

### Music (loops)
| Track | Mood / notes |
|---|---|
| Music_Menu | 8-note music-box version of the sister theme, soft wind |
| Music_Village (Area A) | Muted pulse ~80 BPM, two-note piano motif, low cello, tense |
| Music_Memory | Music box alone with reverb (used for both memories) |
| Music_Forest (Area B) | Adds frame drum + low strings, mysterious, cold |
| Music_Tower (Area C) | Rises to ~105 BPM, ticking percussion, urgent |

### Ambience (loops)
Amb_FireLoop (crackle, distant roofs collapsing) · Amb_ForestNight (wind in pines, no insects near enemies) · Amb_TowerGears (gears, chains, rising wind) · Amb_Wind.

### SFX
| Group | Sounds |
|---|---|
| Player | Footstep wood / stone / dirt · Jump · Land · **Slide** (scrape loop + stop) · **Hurt** (gasp + impact) · Death / respawn whoosh · Dash burst |
| Lantern / fire | Ember Shot · Charge loop (rising hum) · Charged Flare release (whoomph) · Fireball travel loop · Fire hit / impact · Lantern flicker / low-energy sputter |
| Sister | Shadow Rise (deep bloom) · Shadow orb throw · Shadow Strike slash + bass impact · Heal hum + chime · Shield |
| Pickups / UI | Ember pickup chime · Checkpoint ignite · Heart restore · UI move / confirm / back |
| Area A | Beam crack + fall + thud · Fire-jet warning sputter + roar · Thorn hit · Spirit-platform hum · Gate collapse |
| Area B | Hound growl / telegraph / rush / hurt / dissolve · Falling branch crack · Water splash · Ember Bloom chime |
| Area C | Gear clank · Steam hiss · Chain rattle · Bridge creak + collapse · Single bell strike · Clock tick |

### Example prompts
**Suno / Udio (Music_Village):** *"Dark cinematic game soundtrack, 80 BPM, melancholic two-note piano motif over low cello drone, soft muted pulse, distant fire crackle feel, no vocals, seamless loop, medieval village burning at night, emotional but tense."*

**ElevenLabs SFX (Charged Flare release):** *"Short magical fire blast release, deep whoomph with crackling flames, 1 second, game sound effect, no music."*
Other SFX: write the action + material + length, e.g. *"child sliding on dirt and gravel, 0.6 seconds"*, *"falling burning wooden beam cracks and hits stone street, 1.5 seconds"*.

---

## 4. Character art prompts (keep the current boy consistent)

**Use the current boy sprite (`Assets/Player/...9.03.33 PM (1).jpeg`) as the reference image in every generation.** Paste this block at the start of each prompt:

> **Character lock:** Same character as the reference image: a frightened but determined 10-year-old boy, messy dark-brown spiky hair, brown eyes, muted rust-red scarf with long flowing tail, charcoal-grey short-sleeve tunic, brown leather cross-body strap with a small brass buckle, brown belt with a leather pouch, leather wrist wraps, dark trousers, brown knee boots. **Hands are empty — no lantern, no lamp, no weapon.** Modern clean 32-bit pixel art (SNES-plus detail), dark navy outline (not pure black), 20–24 colours, crisp pixel clusters, no blur, no anti-aliasing on the outline. Side view facing right. Each frame on a 128×128 canvas, feet on the same baseline, identical proportions and scale across all frames. **Real transparent background (PNG alpha) — no checkerboard, no ground, no shadow, no text, no watermark.** Single horizontal sprite-sheet row, frames evenly spaced.

### 4.1 Idle (re-make, no lamp): 8 frames
> [Character lock] Idle animation, 8 frames: relaxed breathing (chest rises 1px), scarf tail sways gently, one blink on frame 6, hands loose at the sides, left hand slightly open as if it holds an invisible lamp handle.

### 4.2 Run (re-make, no lamp): 10 frames
> [Character lock] Run cycle, 10 frames, fast childlike run leaning forward, arms pumping, scarf streaming behind, hair bouncing, clear contact / down / passing / up poses, loopable.

### 4.3 Jump + Fall: 8 frames
> [Character lock] Jump sequence, 8 frames in order: 1 anticipation crouch, 2 take-off push, 3 rising with knees tucked and arms up, 4 apex (body stretched, scarf lifting), 5 start of fall (arms out for balance), 6–7 falling loop (scarf and hair blown upward, legs slightly apart), 8 landing squash (knees bent). Keep the head the same size in every frame.

### 4.4 Slide: 6 frames
> [Character lock] Low ground slide, 6 frames: 1 lean-in, 2 drop to the ground on one hip with the leading leg extended forward, 3–4 sliding loop (body low, one hand trailing near the ground, scarf flat behind), 5 push up, 6 back to a run pose. Height in the slide pose about half the standing height.

### 4.5 Hit / Hurt: 4 frames + death 6 frames
> [Character lock] Hurt reaction, 4 frames: 1 impact (head snaps back, eyes shut, body recoils left), 2 knock-back (feet off the ground, arms flung out), 3 stumble, 4 recover to idle stance. Then a separate 6-frame defeat: kneel, collapse forward, then the body fades into small orange embers and ash pixels from the feet up.

### 4.6 Fire-cast (charged attack without the lamp): 7 frames
> [Character lock] Fire-casting attack, 7 frames: 1 ready stance, 2 right hand pulls back with small orange sparks, 3 a ring of fire forms around the open right palm, 4 the ring grows white-hot with an orange edge, 5 thrust the palm forward — release pose (**do not draw the projectile**, only a small muzzle flash at the palm), 6 recoil step back, 7 recover. The ring stays inside the 128×128 frame.

### 4.7 Charged Flare fireball (projectile): replaces the cut-in-half sprite
> Pixel-art fire projectile for a side-scrolling game, flying to the right, full shape inside each frame (nothing cut off at the edges). 6-frame looping animation on 96×64 canvases: a round white-hot core, layered orange and yellow flame tongues trailing to the left, small ember pixels peeling off, flicker between frames. Then a separate 8-frame impact explosion on 128×128: bright white flash, an expanding orange flame burst, a ring of square ember pixels, fading grey smoke on the last frames. Same palette as the boy's fire ring: white #FFF6D8, yellow #FFD24A, orange #FF8A2A, deep red #C2361F. Real transparent background, crisp pixels, no blur, no text.

### 4.8 Dust and impact effects (jump / land / slide / hit)
> Pixel-art game VFX sprite sheets, real transparent background, crisp pixels, warm ash-grey dust with a faint orange rim (burning-village lighting). (a) Jump take-off puff: 5 frames, 48×32, small dust clouds bursting left and right from the feet. (b) Landing puff: 6 frames, 64×32, a wider dust ring spreading on both sides. (c) Slide trail: 6-frame loop, 64×24, dust and pebbles kicked up behind, with tiny orange sparks. (d) Hit spark: 5 frames, 48×48, a white-orange star burst with 4–6 square pixel shards flying outward. No text, no watermark.

**Tips:** generate one animation per request; if the tool can't output alpha, ask for a flat pure-magenta (#FF00FF) background and remove it in Photopea. Keep frame counts and canvas sizes exactly as written so the frames slot into the project's existing pipeline.
