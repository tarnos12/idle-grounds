# Idle Grounds — Art Request / Art Bible (v2)

Self-contained brief for the artist (human or AI image tool). Everything needed to produce, name, size and deliver the art is in this file. No other document is required.

- Game: **Idle Grounds** — a cultivation (xianxia) sandbox idle game, ported to Unity 6 (URP 2D). The player gathers physical items by hand, builds and feeds buildings, automates hauling with spirit wisps, wakes the Sleeping Dragon and ascends.
- Current state: the game runs with **placeholder emoji** for everything. 183 sprite keys are placeholders (list in section 4 / 5); each needs a real asset. Section 3 lists brand-new art (floating islands, sky, Spirit Bridge) that has no placeholder yet.
- Delivery: Google Drive folder `AI files/Idle Grounds Art/incoming/<category>/`. Anything in `incoming` is imported into Unity and the **Status** column of this document is updated.

---

## 0. Glossary (use these words)

| Term | Meaning |
|---|---|
| **Island** | One of 7 floating landmasses (93×93 cells each = 2976×2976 px): Center, Farm, Mine, Fishing, Volcano, Spirit Grove, Celestial Peak. Separated by open sky. |
| **Zone** | A named rectangle inside an Island (corner, field, ...). |
| **Center** | The starting Island. Never the Altar building. |
| **Altar** | The upgrade-shrine building in the Center (file key `bld_center`). |
| **Converter** | Building that turns input items into output items (Workbench, Loom, ...). |
| **Burner** | A Converter that also burns fuel: Forge, Kiln, Pill Furnace, Star Anvil. 3×5 cells, with a fuel rack drawn to its left. |
| **Generator building** | Makes an item from nothing on a timer: Algae Farm, Herb Garden. |
| **Spirit Bridge** | Building paired one-to-one with a Spirit Bridge on another Island; wisps carry items across the sky one way. |
| **Wisp** | Small glowing spirit that carries one item along a lantern link. |
| **Tribute / Offering / Cost** | Items fed to the Dragon / the Ascension Gate / a construction ghost, Island unlock or Altar upgrade. |

---

## 1. Overview, art direction, palette

### 1.1 Fixed art direction

| Decision | Value |
|---|---|
| Theme | **Xianxia / cultivation fantasy.** An immortal sect's grounds on floating islands above a sea of clouds. Jade, cinnabar red, gold, ink-black, misty whites. Spirit-qi glows in cyan / green / gold. |
| Style | **Pixel art**, **top-down ¾ view** (like Stardew Valley / Eastward): we see the top of the ground and the front face + roof of objects. Crisp. **No anti-aliasing against transparency**, no blur, no gradients except dithered ones. |
| Light | Soft light from the **upper left**. Shadows fall down-right, 1 tone darker than the ground (not black). One-pixel dark outline (palette ink) around items, creatures and icons; buildings and terrain use a darker shade of their own colour as outline ("selective outline"). |
| Palette | 32 colours (below). Do not add colours; use only the palette (FX glows may use the 4-step alpha rule in section 2). |
| Camera | The player sees 17.5 tiles across at the closest zoom and up to 52 tiles across at the furthest. Silhouettes and colour blocking must read small; avoid 1-px noise. |
| Tone | Calm, serene, a little humorous. Buildings look like handmade sect workshops (bamboo scaffolds, tiled eaves, paper lanterns), not heavy industry. Nothing grim, no blood. |

### 1.2 Mood references (words only)

Chinese ink-and-wash *shan shui* landscapes with peaks rising out of mist; the stone pillar mountains of Zhangjiajie; Tang-dynasty temple architecture (sweeping tiled eaves, red lacquer columns, gold finials); Dunhuang mural pigments (mineral green, vermilion, ochre); carved jade and celadon glaze; paper talismans with red cinnabar script; wuxia / xianxia novel cover art (immortals on swords, qi swirls); koi ponds and lotus; rice terraces at dawn; Eastward / Stardew Valley for pixel density and cosy readability.

### 1.3 Colour palette (32 colours, use exactly these hex values)

| # | Name | Hex | Typical use |
|---|---|---|---|
| P01 | Ink black | `#0D0F14` | outlines, deepest shadow, obsidian base |
| P02 | Night ink | `#1B1F2B` | dark shadow, UI panel dark |
| P03 | Slate | `#2E3345` | rock shadow, roof shadow |
| P04 | Dusk grey | `#4A5068` | stone mid-dark |
| P05 | Mist grey | `#7A809A` | stone mid, paths |
| P06 | Pale cloud | `#B7BCCD` | stone light, cloud shadow |
| P07 | Mist white | `#E8ECF3` | clouds, highlights, paper |
| P08 | Deep jade | `#0F3B34` | dark foliage, deep water edge |
| P09 | Jade dark | `#1F6B57` | foliage shadow, jade shadow |
| P10 | Jade | `#2F9A7A` | jade, leaves mid |
| P11 | Bright jade | `#5FCF9C` | jade light, herb glow |
| P12 | Pale jade | `#A8ECC0` | jade highlight |
| P13 | Grass | `#4F7A2F` | grass shadow |
| P14 | Leaf | `#86B34A` | grass mid, leaves |
| P15 | Young bamboo | `#C3D67A` | grass light, bamboo, rice |
| P16 | Lacquer dark | `#4A1417` | dark red shadow |
| P17 | Cinnabar dark | `#8C1D22` | red roof shadow, red mid-dark |
| P18 | Cinnabar | `#C8322B` | red lacquer, pillars, talismans |
| P19 | Vermilion light | `#EE6A4A` | red highlight, flame edge |
| P20 | Dark wood | `#3D2616` | wood shadow |
| P21 | Wood | `#6E4524` | wood mid |
| P22 | Tan wood | `#A8703A` | wood light, clay |
| P23 | Sand | `#D9A45C` | sand, light wood, straw |
| P24 | Gold | `#F2C94C` | gold trim, rewards, qi (gold) |
| P25 | Gold glow | `#FFF0A0` | gold highlight, sparkle core |
| P26 | Deep sea | `#14405A` | deep water, night sky |
| P27 | Water | `#2B86B3` | water mid, sky mid |
| P28 | Qi cyan | `#5ED4E8` | spirit-qi glow, ice, sky light |
| P29 | Qi highlight | `#C4F6FF` | glow core, glass |
| P30 | Lava orange | `#FF8A1F` | lava, fire, embers |
| P31 | Celestial violet dark | `#3A2A63` | night, star rock shadow, obsidian sheen |
| P32 | Celestial violet | `#7D5FC4` | star rock, celestial peak accent |

Colour roles (to keep the world readable): **green/jade = nature and spirit herbs; cinnabar red = sect buildings and talismans; gold = rewards, Altar, Dragon, fuel/value; cyan = wisps, qi, water, sky; orange = fire and lava; violet = celestial / star items.**

Island ground palettes: Center = P13 P14 P15 + path P05 P06; Farm = P22 P23 P15 + paddy water P27 P28; Mine = P03 P04 P05 P06; Fishing = P26 P27 P28 + shore P23 P14; Volcano = P01 P02 P03 + lava P30 P19; Spirit Grove = P08 P09 P13 P14; Celestial Peak = P06 P07 P29 P31 P32.

---

## 2. Technical specification

### 2.1 Global rules

| Topic | Rule |
|---|---|
| Grid | **1 cell = 32×32 px.** |
| Unity import | **PPU 32**, Filter **Point (no filter)**, Compression **None**, no mipmaps, Wrap Clamp (tile/parallax art: Repeat). sRGB. Sprite mode Single (static) or Multiple/grid-sliced (strips). |
| Pivot | **Bottom-centre** for buildings, nodes, fixtures, enemies, dragon (anchors on the ground). **Centre** for items, FX, UI icons, wisps, tiles. |
| Format | PNG RGBA 8-bit (or `.aseprite` — Unity's Aseprite importer is installed; keep the layer/tag names tidy). Transparent background. **No padding, margins, borders or drop-shadows baked into the canvas** (except where a table says "shadow included"). |
| Sprite sheets | **Horizontal strips**, equal frame size, no gaps, frame count and frame size in the file name: `fox_idle_32x32_4f.png`. Static art has no suffix: `bld_kiln.png`. 9-slice art states its border insets in the table (L,R,T,B px). |
| Alpha | Binary alpha (0 or 255) for all world art. **Exception:** FX glows, mist/veil overlays and cloud layers may use up to 4 alpha steps (25/50/75/100%) arranged as dither or hard bands — never smooth gradients. |
| Pixel integrity | No rotation or scaling of pixels at a non-integer ratio inside one image (no mixels). Sub-pixel rotation is done by Unity, never baked. |
| Anim playback | Idle loops 4f @ 6 fps; move 4f @ 10 fps; hit 2f @ 12 fps (played once); working/FX loops 4–6f @ 8–12 fps. Frame 1 of every loop must connect cleanly to the last frame. |
| Facing | Creatures are drawn **facing right**; Unity flips for left. |
| Orientation | Light from upper-left, base of every object sits flat on the bottom edge of its footprint. |
| Naming | lowercase, underscores, existing keys unchanged. |

### 2.2 Canvas size rules per category

Footprint = what the object occupies in cells. **Canvas** = PNG size. Art may extend **up to 32 px above the footprint** (roof tips, canopies) but must not extend left/right/below the footprint (exceptions stated). The base of the object touches the bottom edge of the canvas.

| Category | Footprint (cells) | Canvas (px) | Notes |
|---|---|---|---|
| Items (world/HUD/UI) | 1×1 | **32×32** | Shown at 1× on the ground (about 20 px), at **2×** (64 px) in the hand chip and tooltips, and ~18 px inline in text. Read at small size: chunky silhouette, 1 px ink outline. Centred, fills 24–28 px. |
| 1×1 logistics buildings (Gathering Stone, Wisp Lantern, Warding Seal, Furnace Spirit) | 1×1 | **32×48** | Up to 16 px above the cell. |
| Standard buildings (Workbench, Paper Mill, Infusion Array, Loom, Mill, Brewery, Cauldron, Jade Carver, Talisman Atelier, Dragon Shrine, Charcoal Pit, Meditation Pavilion, Storehouse, Algae Farm, Herb Garden) | 3×3 | **96×128** | 32 px roof overhang above the 96×96 footprint. |
| **Burners** (Forge, Kiln, Pill Furnace, Star Anvil) | **3×5** | **96×192** | Tall furnace/kiln; chimney or roof may use the top 32 px. The fuel rack is a separate UI sprite drawn to the left (section 5). |
| Altar, Sleeping/Awake Dragon, Ascension Gate | 5×5 | **160×192** | Big landmark pieces. |
| Spirit Bridge (new) | **2×1** | **64×64** | Footprint 64×32, up to 32 px above. |
| Fixture: Spirit Tree | 4×4 | **160×192** | Trunk base inside the 128×128 footprint; **canopy overhangs 16 px left/right and 64 px above** (this is the only left/right exception). |
| Fixture: Quarry Rock, Spring | 2×2 | **64×80** | Up to 16 px above. |
| Nodes (resource objects) | 1×1 / 2×2 / 3×3 | **32×48 / 64×80 / 96×112** | Up to 16 px above the footprint. Variable-size nodes (ore, obsidian, bamboostalk, starrock) are delivered in the sizes listed. |
| Creatures | n/a | **32×32** (boar **48×32**) | Feet at the bottom edge. |
| Wisp | n/a | **12×12** (glow included) | 4f glow pulse; red "returning" variant. |
| Terrain tiles | 1×1 | **32×32** | Seamless where stated. |
| UI icons (ui_*, perk_*, upg_*, quest icons) | n/a | **32×32** | Centre pivot, drawn on transparent; Unity adds frames. |

### 2.3 Folder / category map

`incoming/items`, `buildings`, `nodes`, `fixtures`, `creatures`, `islands`, `sky`, `ui`, `fx`. Category in every table is given in the heading. Dragon art goes in `creatures`; Spirit Bridge and unlock stele in `buildings`/`islands` as noted.

---

## 3. NEW: floating islands, sky and Spirit Bridges

The world is now seven separate floating islands over a sea of clouds (see section 0). Islands are authored in a scene; each is painted on a Unity Tilemap with **Rule Tiles**. The artist supplies tile strips that map 1:1 to a Rule Tile.

### 3.0 Islands are IRREGULAR — paint any shape (v2)

Islands must **never look like squares**. Gameplay still uses a square 93×93-cell playable area per island, but the **painted landmass is larger**: the playable square sits inside it, and the irregular coast extends 8–30 cells (varying, with big lobes, peninsulas, deep bays and detached islets) beyond the square, so the square never reads. The organic outline is purely visual. Each island is painted by hand in Unity's Tilemap with these tiles, so every island gets a unique organic silhouette: bays, peninsulas, narrow necks, diagonal coastlines, small satellite islets and even holes/ponds open to the sky. The tileset therefore has to support **any** shape — which is why every biome uses the **47-tile "blob" layout** (all combinations of the 8 neighbours, the standard autotile set used by Unity Rule Tiles / Tiled "blob" terrain), not a simple 4-edge set.

- Deliver the ground tileset as a **47-tile blob sheet** per biome: `island_<biome>_ground_blob_32x32_47f.png` — a 47-frame horizontal strip (1504×32) **or** the common 7×7 blob template grid (224×224, unused cells transparent); the frame order is **canonical and defined exactly** as follows (it is what `blob_mask_reference.json` encodes — deliver that file alongside the sheet):
  - Neighbour bits (1 = that neighbour is ground): **N=1, E=2, S=4, W=8, NE=16, SE=32, SW=64, NW=128**.
  - A diagonal bit is set **only when both adjacent cardinal bits are set** (otherwise it is 0), which leaves exactly 47 valid masks.
  - Frames are the 47 valid masks in **ascending numeric order**: `0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,19,23,27,31,38,39,46,47,55,63,76,77,78,79,95,110,111,127,137,139,141,143,155,159,175,191,205,207,223,239,255` (frame 0 = isolated cell, frame 46 = mask 255 = fully surrounded). No other ordering (cr31/Wang, 7×7 template, etc.) is accepted.
  - **Expected companion file:** `blob_mask_reference.json` (`{order, bits, frame_masks}`) delivered next to the sheet; the game's Rule Tile is generated from the same table, and the intake tool brings `*mask*.json` / `*reference*.json` files in with the art.
- Plus **3 centre-fill variants** and **4 scatter overlays** (same as below).
- Coastlines should look natural at every step: rounded outer corners, soft inner corners, 1-cell diagonal staircases must read as a smooth diagonal edge, not jaggies.
- The **cliff rim (3.2)** must likewise cover every exposed edge seen from the front: under straight south edges, under outer SW/SE corners, under diagonal steps, and short "end caps" where a south edge turns north. Side (E/W) edges get a thin rock lip only (top-down ¾ view shows the underside mainly on the south side).
- Large underside decorations (3.2) are placed by hand under each island, so their shapes should combine well (overlapping pieces of different widths), and work under narrow peninsulas as well as wide coasts.
- The **19-tile strip below remains acceptable as a minimum** (Unity can derive many cases from it), but the 47-blob set is the preferred deliverable.

### 3.1 Per-island ground tileset (32×32 tiles, Rule Tile layout)

Minimum layout (if no 47-blob set is delivered): **one strip of 19 tiles**: `island_<biome>_ground_32x32_19f.png` (608×32). Frame order is fixed:

| Frame | Role | Rule (neighbours "empty" = sky/no ground) |
|---|---|---|
| 0, 1, 2 | **Centre fill** variants A, B, C (random-picked; seamless with each other and with themselves) | all 8 neighbours filled |
| 3 | Edge **N** | north neighbour empty |
| 4 | Edge **E** | east empty |
| 5 | Edge **S** | south empty (a cliff tile from 3.2 hangs below it) |
| 6 | Edge **W** | west empty |
| 7 | Outer corner **NW** | N and W empty |
| 8 | Outer corner **NE** | N and E empty |
| 9 | Outer corner **SE** | S and E empty |
| 10 | Outer corner **SW** | S and W empty |
| 11 | Inner corner **NW** | filled on all 4 sides, only the NW diagonal empty |
| 12 | Inner corner **NE** | only NE diagonal empty |
| 13 | Inner corner **SE** | only SE diagonal empty |
| 14 | Inner corner **SW** | only SW diagonal empty |
| 15–18 | Scatter decoration overlays (transparent background, drawn on top of fill): biome flowers/pebbles/etc. | randomly sprinkled |

Edge tiles show a small lip/rim of the ground type fading to the cliff colour (no sky inside the tile; the sky is behind the tilemap). Centre variants must tile with no visible repetition grid.

| Key / file | Biome (Island) | Look | Size | Frames | Pri | Status |
|---|---|---|---|---|---|---|
| `island_center_ground_32x32_19f.png` | **Center** — sect courtyard | Short jade-green grass with light flecks and tiny white blossoms; mown, tidy. Scatter: clover, pebbles, fallen petals, a dandelion. | 32×32 | 19 | P1 | superseded by blob v3 |
| `island_center_path_32x32_15f.png` | Center | Pale **stone path** Rule Tile (frames 0–2 fill, 3–6 edges, 7–10 outer, 11–14 inner; path edges are ragged flagstones on grass). Paths join buildings. | 32×32 | 15 | P1 | integrated |
| `island_center_plaza_32x32_3f.png` | Center | Altar plaza: carved flagstone with a faint gold taiji / bagua ring pattern (3 variants, centre ones seamless). | 32×32 | 3 | P1 | integrated |
| `island_farm_ground_32x32_19f.png` | **Farm** — rice paddies | Tilled dark-tan soil with flooded **rice-paddy terrace** look (shallow water P27/P28 reflections between earth bunds, young rice shoots). Sandy band variant in frame 2. Scatter: reeds, frog, straw. | 32×32 | 19 | P2 | requested |
| `island_mine_ground_32x32_19f.png` | **Mine** — grey rock | Grey layered rock and gravel with faint ore flecks; hard, angular. Scatter: pebbles, cracks, a lantern-lit lichen. | 32×32 | 19 | P2 | requested |
| `island_fishing_ground_32x32_19f.png` | **Fishing** — lotus pond | Wet pond bank: mossy shore, wet sand, reeds at the corners. (The pond itself uses the water tile below.) Scatter: lotus pads, reeds, stepping stones. | 32×32 | 19 | P2 | requested |
| `island_fishing_water_32x32_4f.png` | Fishing | Animated pond water, seamless, 4-frame ripple loop with soft lotus pads and koi shadows. Used inside the centre zone. | 32×32 | 4 | P2 | requested |
| `island_volcano_ground_32x32_19f.png` | **Volcano** — obsidian | Black-violet obsidian plates (P01–P03, sheen P31) with **glowing lava cracks** (P30/P19) between them. Scatter: embers, ash piles, small crystals. | 32×32 | 19 | P2 | requested |
| `island_volcano_lavacrack_32x32_4f.png` | Volcano | Transparent overlay: glowing crack glimmer loop placed over centre fill. | 32×32 | 4 | P3 | requested |
| `island_grove_ground_32x32_19f.png` | **Spirit Grove** — bamboo moss | Deep teal moss, bamboo-leaf litter, dappled light patches, faint jade glow spots. Scatter: mushrooms, ferns, fallen bamboo leaves, glowing spores. | 32×32 | 19 | P2 | requested |
| `island_celestial_ground_32x32_19f.png` | **Celestial Peak** — cloud-marble | Cloud-marble: white-lilac veined stone with starlit specks and pale cyan inlay. Scatter: star dust, tiny crystals, cloud wisps. | 32×32 | 19 | P2 | requested |
| `island_celestial_twinkle_32x32_4f.png` | Celestial Peak | Transparent overlay of twinkling star specks, loop. | 32×32 | 4 | P3 | requested |
| `zone_clay_patch_32x32_3f.png` | Zone patch (Center clay field) | Reddish-brown clay ground variants, ragged-edged blotch fits on grass (3 variants). | 32×32 | 3 | P2 | requested |
| `zone_stone_patch_32x32_3f.png` | Zone patch (quarry field) | Grey gravel and chip variants. | 32×32 | 3 | P2 | requested |
| `zone_sand_patch_32x32_3f.png` | Zone patch (Farm sand band) | Pale sand ripples. | 32×32 | 3 | P2 | requested |
| `zone_water_patch_32x32_3f.png` | Zone patch (Fishing spring field) | Wet dark ground with puddles and droplets. | 32×32 | 3 | P3 | requested |
| `zone_wood_patch_32x32_3f.png` | Zone patch (Center wood field) | Grass strewn with bark chips and twigs. | 32×32 | 3 | P3 | requested |

### 3.2 Island underside / cliff rim and large decorations

The island's edge: below each S-edge/corner tile hangs a **cliff rim row** (32 px tall), then Unity places large underside decorations so each island looks like a chunk of mountain floating in the sky. Mist hangs at the bottom.

| Key / file | Depicts | Size | Frames | Pri | Status |
|---|---|---|---|---|---|
| `island_center_cliff_32x32_8f.png` | **Cliff rim strip, Center.** Frames: 0–2 mid A/B/C (earth lip over brown-grey rock, tiny roots), 3 left end (under SW corner), 4 right end (SE), 5 inner left, 6 inner right, 7 mid with **waterfall source** (small spout). Seamless horizontally across 0–2. | 32×32 | 8 | P1 | integrated |
| `island_farm_cliff_32x32_8f.png` | Cliff rim, Farm (same frame order; terraced paddy walls, warm tan rock) | 32×32 | 8 | P2 | integrated |
| `island_mine_cliff_32x32_8f.png` | Cliff rim, Mine (grey striated rock, ore glints) | 32×32 | 8 | P2 | integrated |
| `island_fishing_cliff_32x32_8f.png` | Cliff rim, Fishing (mossy blue-grey rock, dripping water) | 32×32 | 8 | P2 | integrated |
| `island_volcano_cliff_32x32_8f.png` | Cliff rim, Volcano (black rock with orange glow in cracks) | 32×32 | 8 | P2 | integrated |
| `island_grove_cliff_32x32_8f.png` | Cliff rim, Spirit Grove (dark rock draped with moss and hanging bamboo roots) | 32×32 | 8 | P2 | integrated |
| `island_celestial_cliff_32x32_8f.png` | Cliff rim, Celestial Peak (white marble with cyan inlay, drifting wisps) | 32×32 | 8 | P2 | requested |
| `island_underside_rock_256x192.png` | **Large underside decoration 1:** tapering mass of hanging rock (stalactite-like), earth tones, glowing mineral veins, bottom edge dithered into mist. Neutral colours, recoloured by Unity tint per island. Pivot top-centre. | 256×192 | 1 | P1 | integrated |
| `island_underside_roots_192x160.png` | **Large decoration 2:** twisted giant roots and vines clutching hanging rocks, a few jade leaves. Pivot top-centre. | 192×160 | 1 | P1 | integrated |
| `island_underside_vines_128x128.png` | **Large decoration 3:** curtain of hanging vines with tiny lanterns / paper charms swaying. Pivot top-centre. | 128×128 | 1 | P2 | requested |
| `island_underside_stalactite_96x160.png` | Small tapering rock spike (accent, may be repeated). Pivot top-centre. | 96×160 | 1 | P2 | requested |
| `island_underside_lavadrip_128x160_4f.png` | Volcano variant: hanging rock with slow **lava drips** (4f loop). Pivot top-centre. | 128×160 | 4 | P3 | requested |
| `island_waterfall_32x96_4f.png` | **Small waterfall** pouring off the cliff into the sky: seamless vertical loop, white-cyan streaks. Pivot top-centre. | 32×96 | 4 | P2 | requested |
| `island_waterfall_mist_64x32_4f.png` | Mist burst at the bottom of a waterfall (transparent, 4-alpha rule). Pivot top-centre. | 64×32 | 4 | P3 | requested |

### 3.3 Sky background and parallax

| Key / file | Depicts | Size | Frames | Pri | Status |
|---|---|---|---|---|---|
| `sky_gradient_512x1024.png` | Vertical sky gradient built from **dithered palette bands**: pale mist white and qi cyan at the bottom/horizon (sea of clouds), through water blue, to deep-sea / night ink at the top with a few faint stars and a soft pale sun-haze upper-left. Stretched to fill the screen. | 512×1024 | 1 | P1 | integrated |
| `sky_clouds_far_1024x256.png` | **Parallax layer far:** low, pale, flat banks of cloud sea, small shapes, lowest contrast. **Tileable horizontally** (left edge = right edge). | 1024×256 | 1 | P1 | integrated |
| `sky_clouds_mid_1024x256.png` | **Parallax layer mid:** billowing cumulus with lilac-grey shadow undersides, medium contrast, tileable horizontally. | 1024×256 | 1 | P1 | integrated |
| `sky_clouds_near_1024x256.png` | **Parallax layer near:** large, bright, sparse cloud wisps with ragged ends (drift fast, passes in front of island undersides), tileable horizontally. | 1024×256 | 1 | P1 | integrated |
| `sky_peaks_1024x256.png` | **Distant mountain-peak silhouettes:** shan-shui spires and floating pillars in 2–3 tones of blue-grey fading into mist at the base. Tileable horizontally. Sits behind the far clouds. | 1024×256 | 1 | P2 | integrated |
| `sky_cloudpuff_128x64_3f.png` | Three individual drifting cloud puffs (frames = three different shapes, 128×64 each), for random spawns. | 128×64 | 3 | P2 | requested |
| `sky_sunbeam_128x256.png` | Soft diagonal light shaft (4-alpha rule), overlay behind islands. | 128×256 | 1 | P3 | requested |

### 3.4 Spirit Bridge and unlock sign

**Spirit Bridge** is a **2×1-cell building** (footprint 64×32; canvas **64×64**): two carved jade-and-cinnabar gate posts on a small stone platform with a shimmering qi pool/mirror between them. Wisps enter/exit the pool. Three variants. Cost wood 10 + stone 10; it appears once a second Island is unlocked.

| Key / file | Depicts | Size | Frames | Pri | Status |
|---|---|---|---|---|---|
| `bld_spirit_bridge_unpaired.png` | Dormant: posts dark, pool empty and grey, small cinnabar "unlinked" talisman. | 64×64 | 1 | P2 | requested |
| `bld_spirit_bridge_sending_64x64_4f.png` | **Sending**: pool glows cyan, qi streams rise **outward/upward**, arrow-like swirl pointing out to the sky. | 64×64 | 4 | P2 | requested |
| `bld_spirit_bridge_receiving_64x64_4f.png` | **Receiving**: pool glows gold-green, qi streams drift **inward/downward** into the pool. | 64×64 | 4 | P2 | requested |
| `fx_qi_trail_64x16_4f.png` | **Sky "qi trail"**: a segment of glowing cyan thread/ribbon with moving sparkles that Unity tiles along the flight line between two bridges (wisps ride along it). Seamless **horizontally** (tiles end-to-end), drawn left→right, rotated by Unity. | 64×16 | 4 | P2 | requested |
| `fx_qi_trail_end_32x32_4f.png` | Burst/ring where the trail enters or leaves a bridge. | 32×32 | 4 | P3 | requested |

### 3.5 Locked-island veil and unlock stele

A locked island is covered by a veil of mist and shows an **unlock sign** (stone stele) at its edge. Cost is shown by UI text next to it.

| Key / file | Depicts | Size | Frames | Pri | Status |
|---|---|---|---|---|---|
| `island_veil_mist_256x256_4f.png` | Seamless-tiling **veil/mist overlay**: dense swirling pale-lilac-grey fog, mostly 50–75% alpha with a few holes, slow 4-frame drift loop. Tiles in both directions. | 256×256 | 4 | P2 | requested |
| `island_veil_edge_64x64.png` | Veil fringe tile, edge variant (fog thins out into wisps) — placed around the island silhouette. Seamless along its long axis. | 64×64 | 1 | P3 | requested |
| `island_unlock_stele_locked.png` | **Unlock sign, locked state**: 2×2-cell carved stone stele with a red-sealed talisman strip and a simple chain/lock glyph. Canvas **64×96**, pivot bottom-centre. | 64×96 | 1 | P2 | requested |
| `island_unlock_stele_ready_64x96_4f.png` | Same stele, **affordable** state: seal broken, gold glow runs through carved grooves (pulsing 4f). | 64×96 | 4 | P2 | requested |
| `island_unlock_stele_partial.png` | Same stele, **partly paid** state: half the grooves glow gold. | 64×96 | 1 | P3 | requested |

---

## 4. Asset lists

Priority: **P1** = core loop, needed first. **P2** = the rest of gameplay art. **P3** = polish, UI chrome, icons, FX. Status for all: `requested`.
Frame suffixes in the "Key / file" column show the delivered filename; static art has none.

### 4.1 Items — category `items` (46 keys, 32×32, 1 frame, centre pivot)

"Existing" = a 16×16 icon already exists in `old-game/assets/icons/<key>.png` (23 of them). They may be **redrawn at 32×32** in the new style (preferred) — or, as a fallback, kept and 2× nearest-neighbour upscaled. Items must stay recognisable at 16 px: strong silhouette, 1 px ink outline, 3–4 colours each. Colour-code families: pills (4 colours, 4 different shapes), jade (greens), metals (grey-steel).

| Key / file | What it depicts (cultivation flavour) | Existing 16×16 | Pri | Status |
|---|---|---|---|---|
| `item_wood.png` | Short stack of two cut logs from the Spirit Tree, pale cut ends with ring, hint of jade moss | Y | P1 | integrated |
| `item_leaves.png` | Fan of three broad green leaves, one with a golden dew spot | Y | P1 | integrated |
| `item_stone.png` | Rough grey chunk with a cleaved lighter face | Y | P1 | integrated |
| `item_clay.png` | Wet brick-red clay lump, finger marks | Y | P1 | integrated |
| `item_plank.png` | Single sawn board with grain, two nail/peg dots | Y | P1 | integrated |
| `item_brick.png` | Fired grey-blue roof brick with a cinnabar stamp | Y | P1 | integrated |
| `item_wheat.png` | (Rice) Sheaf of golden rice stalks tied with red thread | Y | P2 | requested |
| `item_cotton.png` | White cotton boll, small leaf collar | Y | P2 | requested |
| `item_sand.png` | Small pile of fine golden sand with a glint | Y | P2 | requested |
| `item_iron_ore.png` | Dark rusty rock nugget with metallic silver veins | Y | P2 | requested |
| `item_iron_bar.png` | Dull steel ingot bar, bevelled, one bright edge | Y | P2 | requested |
| `item_fish.png` | (Koi) Orange-and-white koi, side view, curved body | Y | P2 | requested |
| `item_algae.png` | Dripping green-teal algae tuft | Y | P2 | requested |
| `item_water.png` | (Spring Water) Round blue droplet with a cyan qi sparkle | Y | P2 | requested |
| `item_spirit_essence.png` | Floating cyan-white wisp-orb with a small trailing tail and sparkles | Y | P2 | requested |
| `item_spirit_herb.png` | Glowing green herb sprig (lingzhi-style leaf, pale-jade glow) | Y | P2 | requested |
| `item_bamboo.png` | Single jade-green bamboo segment with two nodes | Y | P2 | requested |
| `item_jade_shard.png` | Raw faceted jade fragment, bright jade with white inclusion | Y | P2 | requested |
| `item_paper.png` | Rolled rice-paper scroll tied with a red cord | Y | P2 | requested |
| `item_spirit_stone.png` | Faceted translucent blue-cyan crystal with inner glow | Y | P2 | requested |
| `item_tools.png` | Crossed hammer and small pickaxe, wood handles | Y | P2 | requested |
| `item_glass.png` | Clear pale-cyan glass pane/prism with white highlight | Y | P2 | requested |
| `item_spirit_jade.png` | Polished jade disc (bi) with glowing core, gold ring edge | Y | P2 | requested |
| `item_cloth.png` | Folded bolt of off-white woven cloth | N | P2 | requested |
| `item_rope.png` | Coiled hemp rope, tan | N | P2 | requested |
| `item_robe.png` | Folded cultivator robe, white with a red sash and gold trim | N | P2 | requested |
| `item_flour.png` | (Rice Flour) Small sack of rice flour, tied top, white dusted | N | P2 | requested |
| `item_spirit_buns.png` | Two steamed white buns with a red dot, steam wisp, faint glow | N | P2 | requested |
| `item_spirit_wine.png` | Round-bellied gourd wine jug with red stopper, cyan swirl | N | P2 | requested |
| `item_qi_elixir.png` | Small round flask of swirling cyan-green liquid, cork top | N | P2 | requested |
| `item_vitality_pill.png` | Single pink-red round pill with a gold heart-like swirl | N | P2 | requested |
| `item_beast_bait.png` | Pinkish wriggling bait lump / worm cluster in a leaf wrap | N | P2 | requested |
| `item_charcoal.png` | Black charcoal chunk with a glowing orange crack | N | P2 | requested |
| `item_jade.png` | Polished, carved jade bead / pendant (finished), smooth emerald | N | P2 | requested |
| `item_firestone.png` | Dark rock core glowing orange-red from inside, tiny ember sparks | N | P2 | requested |
| `item_beast_bone.png` | Chunky bone with a spiral glyph, ivory | N | P2 | requested |
| `item_star_steel.png` | Violet-silver ingot with star-glints and a faint trail | N | P2 | requested |
| `item_ember_pill.png` | **Red** pill, teardrop/flame shaped | N | P2 | requested |
| `item_verdant_pill.png` | **Green** pill, leaf-shaped | N | P2 | requested |
| `item_swiftwind_pill.png` | **Yellow** pill, with wind-swirl line (round) | N | P2 | requested |
| `item_stoneheart_pill.png` | **Purple** pill, hexagonal / stone-faceted | N | P2 | requested |
| `item_talisman.png` | Yellow paper strip with red cinnabar script and a seal stamp, slightly floating | N | P2 | requested |
| `item_dragon_scale.png` | Large gold-orange dragon scale, rounded diamond, bright highlight | N | P2 | requested |
| `item_obsidian.png` | Black volcanic glass shard with violet sheen | N | P2 | requested |
| `item_star_fragment.png` | Violet-white meteor shard with a four-point sparkle | N | P2 | requested |
| `item_moonpetal.png` | White-pink five-petal night flower with soft glow | N | P2 | requested |

(46 item keys. 6 are P1.)

### 4.2 Buildings — category `buildings` (26 keys + Spirit Bridge in 3.4)

Static art is the idle state. Facing: front face toward the viewer (south), roofs visible. **All buildings are readable at a glance** — each has a unique silhouette and a distinct dominant colour. Pivot bottom-centre. Burners show their furnace mouth glowing in the working variant (optional rows in 4.2.1).

| Key / file | Footprint → canvas (px) | What it depicts | Pri | Status |
|---|---|---|---|---|
| `bld_center.png` | 5×5 → **160×192** | **Altar** — circular raised stone dais with a pagoda-like shrine: red lacquer pillars, gold-trimmed double eaves, a central incense brazier, bagua ring carved into the floor; the centre of the Center island. Warm gold accents. | P1 | integrated |
| `bld_dragon.png` | 5×5 → **160×192** | **Sleeping Dragon building** — same art as `dragon_sleeping` frame 1 (see 4.4); a coiled stone-grey dragon resting on a rocky nest. Placeholder key kept; deliver a copy of `dragon_sleeping` frame 1. | P1 | integrated |
| `bld_workbench.png` | 3×3 → 96×128 | **Workbench** — open carpenter's shed: thick wooden table with saw, plane and plank stack, bamboo scaffold beams, small red cloth awning. | P1 | integrated |
| `bld_kiln.png` | **3×5** → 96×192 | **Kiln** (Burner) — stepped dragon-kiln (long, rising brick chambers) with a chimney, glowing mouth at the base, stacked bricks/clay jars at the side. Terracotta/dark grey. | P1 | integrated |
| `bld_gathering_stone.png` | 1×1 → 32×48 | **Gathering Stone** — a standing carved spirit stone with a glowing green swirl rune; floats slightly above a base ring. Green qi (matches its green reach circle). | P1 | integrated |
| `bld_wisp_lantern.png` | 1×1 → 32×48 | **Wisp Lantern** — red-and-gold hanging paper lantern on a short carved post with a tiny glowing wisp inside. Gold circle-glow. | P1 | integrated |
| `bld_warding_seal.png` | 1×1 → 32×48 | **Warding Seal** — a small stone tablet with a red cinnabar seal circle and talisman paper stuck on it, standing on a stake. | P1 | integrated |
| `bld_storehouse.png` | 3×3 → 96×128 | **Storehouse** — wooden granary on stilts with a sloped tiled roof, round carved door, rope-bound crates. | P1 | integrated |
| `bld_paper_mill.png` | 3×3 → 96×128 | **Paper Mill** — bamboo-slatted hut with a water-wheel/press, drying racks of white paper sheets hanging out. | P2 | requested |
| `bld_infusion_array.png` | 3×3 → 96×128 | **Infusion Array** — circular flagstone formation with glowing cyan runes and four small pillars, floating crystal in the middle. | P2 | requested |
| `bld_loom.png` | 3×3 → 96×128 | **Loom** — big wooden weaving loom under a thatched roof, taut threads, bobbins, a fabric bolt rolling out. | P2 | requested |
| `bld_mill.png` | 3×3 → 96×128 | **Mill** — round stone grinding mill with a wooden turning beam / water-wheel, rice sacks nearby. | P2 | requested |
| `bld_brewery.png` | 3×3 → 96×128 | **Brewery** — rustic tavern-like shed with a row of large clay wine jars with red cloth stoppers and a flag sign. | P2 | requested |
| `bld_cauldron.png` | 3×3 → 96×128 | **Cauldron** — large three-legged bronze ding cauldron on a stone base, green-cyan vapour rising, ladle and herb bundles. | P2 | requested |
| `bld_jade_carver.png` | 3×3 → 96×128 | **Jade Carver** — craftsman's stall with a stone carving wheel, a half-carved jade block, chips and tools on a mat. | P2 | requested |
| `bld_pill_furnace.png` | **3×5** → 96×192 | **Pill Furnace** (Burner) — tall alchemy furnace: bronze body with dragon-head handles, rising pagoda lid, glowing mouth, steam. | P2 | requested |
| `bld_star_anvil.png` | **3×5** → 96×192 | **Star Anvil** (Burner) — massive dark anvil with a violet star-metal glow, a hammer, sparks rising, a meteor-iron slab; forge shed behind. | P2 | requested |
| `bld_talisman_atelier.png` | 3×3 → 96×128 | **Talisman Atelier** — scholar's pavilion: low desk, brush stand, ink stone, yellow talisman strips drying on a line, red lacquer posts. | P2 | requested |
| `bld_dragon_shrine.png` | 3×3 → 96×128 | **Dragon Shrine** — small shrine with a carved gold-and-cinnabar dragon coiled round a pillar, offering bowl, incense. | P2 | requested |
| `bld_ascension_gate.png` | 5×5 → **160×192** | **Ascension Gate** — a monumental gold-and-cinnabar torii/pailou gate on a stair platform, cloud-patterned beams, a swirling taiji portal between the posts. Final monument. (Optional glow-pulse variant in 4.2.1.) | P2 | requested |
| `bld_charcoal_pit.png` | 3×3 → 96×128 | **Charcoal Pit** — earthen mound kiln with a smoking vent, stacked logs, black soot. | P2 | requested |
| `bld_furnace_spirit.png` | 1×1 → 32×48 | **Furnace Spirit** — a tiny stone lantern-brazier with a cute flame spirit face; orange (matches its orange stoker radius). | P2 | requested |
| `bld_meditation_pavilion.png` | 3×3 → 96×128 | **Meditation Pavilion** — open-sided hexagonal pavilion, blue-grey tiled roof, red pillars, cushion mats, hanging bells. | P2 | requested |
| `bld_forge.png` | **3×5** → 96×192 | **Forge** (Burner) — smithy with a stone furnace and tall chimney, bellows, anvil outside, glowing orange mouth. | P2 | requested |
| `bld_algae_farm.png` | 3×3 → 96×128 | **Algae Farm** (Generator building; built on water only) — wooden platform over water with square algae tanks and floating mats. | P2 | requested |
| `bld_herb_garden.png` | 3×3 → 96×128 | **Herb Garden** (Generator building) — fenced raised herb beds, glowing spirit-herb plants, little bamboo shade and a watering ladle. | P2 | requested |

#### 4.2.1 Animated building variants (optional polish)

Same canvas as the static file; same pivot. 4 frames, loops. Frame 1 = the static art's state.

| Key / file | Depicts | Pri | Status |
|---|---|---|---|
| `bld_workbench_working_96x128_4f.png` | saw moves, wood chips pop | P3 | requested |
| `bld_kiln_working_96x192_4f.png` | mouth fire flickers, smoke from chimney | P3 | requested |
| `bld_paper_mill_working_96x128_4f.png` | press descends, paper sheet appears | P3 | requested |
| `bld_infusion_array_working_96x128_4f.png` | runes pulse, crystal spins | P3 | requested |
| `bld_loom_working_96x128_4f.png` | shuttle passes through threads | P3 | requested |
| `bld_mill_working_96x128_4f.png` | grinding stone turns, flour dust | P3 | requested |
| `bld_brewery_working_96x128_4f.png` | jars bubble, steam | P3 | requested |
| `bld_cauldron_working_96x128_4f.png` | liquid boils, vapour curls | P3 | requested |
| `bld_jade_carver_working_96x128_4f.png` | wheel spins, green chips | P3 | requested |
| `bld_pill_furnace_working_96x192_4f.png` | mouth blazing, lid steam | P3 | requested |
| `bld_star_anvil_working_96x192_4f.png` | hammer strikes, violet sparks | P3 | requested |
| `bld_talisman_atelier_working_96x128_4f.png` | brush paints, strip glows | P3 | requested |
| `bld_charcoal_pit_working_96x128_4f.png` | smoke billows from the vent | P3 | requested |
| `bld_forge_working_96x192_4f.png` | furnace roars, sparks | P3 | requested |
| `bld_algae_farm_working_96x128_4f.png` | bubbles, algae sway | P3 | requested |
| `bld_herb_garden_working_96x128_4f.png` | herbs glow and sway | P3 | requested |
| `bld_meditation_pavilion_occupied_96x128_4f.png` | glowing qi motes drift while disciples cultivate | P3 | requested |
| `bld_ascension_gate_pulse_160x192_4f.png` | portal swirls, gold glow pulses (once built) | P3 | requested |
| `bld_wisp_lantern_glow_32x48_4f.png` | wisp bobs, light flickers | P3 | requested |
| `bld_gathering_stone_pulse_32x48_4f.png` | rune pulses (when pulling items) | P3 | requested |
| `bld_furnace_spirit_flame_32x48_4f.png` | flame dances | P3 | requested |
| `bld_dragon_shrine_glow_96x128_4f.png` | gold aura pulses | P3 | requested |

### 4.3 Resource nodes (`nodes`) and fixtures (`fixtures`)

Nodes are harvested by clicking and then relocate. Frame convention: **break/chop nodes deliver 2 frames: intact + cracked/stripped** (shown after hits). Instant nodes: 1 frame. Pivot bottom-centre. Interactive nodes get a ground-hugging pad ellipse in code (see FX table).

| Key / file | Island, size (cells) → canvas | What it depicts | Frames | Pri | Status |
|---|---|---|---|---|---|
| `node_bush_32x48_2f.png` | Center, 1×1 → 32×48 | Small leafy shrub with blossoms (chopped for leaves); frame 2 = trimmed, bare twigs | 2 | P1 | integrated |
| `node_crop.png` | Farm, 3×3 → 96×112 | Ripe **rice paddy plot**: water-flooded square with golden rice stalks ready to harvest, earth bund edges | 1 | P2 | requested |
| `node_cotton.png` | Farm, 2×2 → 64×80 | Patch of cotton plants, fluffy white bolls on green stalks | 1 | P2 | requested |
| `node_ore_1x1_32x48_2f.png` | Mine, 1×1 | Small grey rock outcrop with a dark clay seam; frame 2 = cracked | 2 | P2 | requested |
| `node_ore_2x2_64x80_2f.png` | Mine, 2×2 | Large grey boulder cluster; frame 2 = cracked | 2 | P2 | requested |
| `node_ironvein_64x80_2f.png` | Mine, 2×2 | Dark rock face with rusty-orange and silver iron veins; frame 2 = cracked open | 2 | P2 | requested |
| `node_jadevein_64x80_2f.png` | Mine, 2×2 | Grey rock with bright green jade veins glinting; frame 2 = cracked | 2 | P2 | requested |
| `node_fish_32x48_4f.png` | Fishing, 1×1 | **Surfacing koi**: ripple ring plus an orange-white koi leaping/bobbing out of the water (4f loop: surface, rise, peak, fall). Water tile is the background (transparent). | 4 | P2 | requested |
| `node_algae_32x48_4f.png` | Fishing, 1×1 | **Floating algae mat** with a few lotus buds bobbing; 4f loop. | 4 | P2 | requested |
| `node_obsidian_1x1_32x48_2f.png` | Volcano, 1×1 | Jagged black obsidian crystal with violet sheen; frame 2 = cracked | 2 | P2 | requested |
| `node_obsidian_2x2_64x80_2f.png` | Volcano, 2×2 | Large cluster of obsidian spires; frame 2 = cracked | 2 | P2 | requested |
| `node_firevein_64x80_2f.png` | Volcano, 2×2 | Black rock with bright orange lava veins glowing; frame 2 = cracked, brighter | 2 | P2 | requested |
| `node_herbbush_32x48_2f.png` | Spirit Grove, 1×1 | Bush with glowing pale-jade spirit herbs; frame 2 = picked, dim | 2 | P2 | requested |
| `node_bamboostalk_1x1_32x48_2f.png` | Spirit Grove, 1×1 | Single tall jade bamboo stalk (top leaves cropped by canvas edge); frame 2 = cut | 2 | P2 | requested |
| `node_bamboostalk_2x2_64x80_2f.png` | Spirit Grove, 2×2 | Clump of three tall bamboo stalks; frame 2 = one cut | 2 | P2 | requested |
| `node_starrock_1x1_32x48_2f.png` | Celestial Peak, 1×1 | Violet-white meteorite chunk with floating star dust; frame 2 = cracked | 2 | P2 | requested |
| `node_starrock_2x2_64x80_2f.png` | Celestial Peak, 2×2 | Large star rock with glowing violet core and floating shards; frame 2 = cracked | 2 | P2 | requested |
| `node_moonshrub_32x48_2f.png` | Celestial Peak, 1×1 | Shrub with pale white-pink **moonpetal** flowers, soft glow; frame 2 = bare | 2 | P2 | requested |

**Fixtures** (fixed objects, placed once per island):

| Key / file | Island, size → canvas | What it depicts | Frames | Pri | Status |
|---|---|---|---|---|---|
| `fix_spirittree.png` | Center, 4×4 → **160×192** | **Spirit Tree** — the big sacred tree: gnarled trunk wrapped with red prayer ribbons and paper charms, wide layered jade canopy with gold-green glints, gold qi motes; roots grip the ground. Trunk base within the 128×128 footprint; canopy overhangs 16 px left/right and 64 px above. | 1 | P1 | integrated |
| `fix_spirittree_sparkle_160x192_4f.png` | Center | Transparent overlay (same canvas): drifting gold sparkles and canopy shimmer, loop (replaces the old ✨ marks) | 4 | P2 | requested |
| `fix_quarry.png` | Center, 2×2 → 64×80 | **Quarry Rock** — large grey boulder face with chisel marks, a pickaxe leaning on it, pale stone chips at the foot, a hint of jade glint. | 1 | P1 | integrated |
| `fix_spring.png` | Fishing, 2×2 → 64×80 | **Spring** — carved stone basin with a small spout / bubbling spring; clear blue-cyan water, mossy rim, lotus bud. | 1 | P2 | requested |
| `fix_spring_flow_64x80_4f.png` | Fishing | Animated version of the spring (water bubbling, ripple loop); same canvas | 4 | P3 | requested |

### 4.4 Creatures — category `creatures` (enemies, dragon, wisp, disciples)

Facing right. Feet at bottom. Transparent. Pivot bottom-centre (wisp: centre). Hit = brief flinch/flash (white-tinted frame 1, 2 frames, played once). HP pips and floating text are drawn in code.

| Key / file | Depicts | Size | Frames | Pri | Status |
|---|---|---|---|---|---|
| `enemy_fox_idle_32x32_4f.png` | **Fox Spirit** — white-orange nine-tail-style fox with cyan qi flame at the tail tip, ears twitching, sitting/standing idle | 32×32 | 4 | P1 | integrated |
| `enemy_fox_move_32x32_4f.png` | Fox Spirit trotting / pouncing run cycle | 32×32 | 4 | P1 | integrated |
| `enemy_fox_hit_32x32_2f.png` | Fox flinch, flash white then recoils | 32×32 | 2 | P1 | integrated |
| `enemy_fox_die_32x32_4f.png` | Fox dissolves into spirit-essence motes (cyan wisps), played once | 32×32 | 4 | P3 | requested |
| `enemy_boar_idle_48x32_4f.png` | **Spirit Boar** — stocky dark-bristled boar with glowing amber tusks and bone-white rune markings, breathing | 48×32 | 4 | P2 | requested |
| `enemy_boar_move_48x32_4f.png` | Boar charging trot | 48×32 | 4 | P2 | requested |
| `enemy_boar_hit_48x32_2f.png` | Boar flinch | 48×32 | 2 | P2 | requested |
| `enemy_boar_die_48x32_4f.png` | Boar falls, bone fragments and essence motes, once | 48×32 | 4 | P3 | requested |
| `dragon_sleeping_160x192_4f.png` | **Sleeping Dragon** — huge grey-jade eastern (serpentine) dragon curled asleep on a mossy rocky nest; closed eyes, whiskers, tiny moss patches on its scales; **breathing idle** (4f: chest rise/fall, whisker sway, a puff of mist from the nostril). 5×5 cells; pivot bottom-centre. | 160×192 | 4 | P1 | integrated |
| `dragon_awake_160x192_4f.png` | **Awakened Dragon** — same pose/nest but eyes open and glowing gold, scales turned gold-green, golden aura and floating qi motes, head lifted watchfully; breathing idle 4f. | 160×192 | 4 | P2 | integrated |
| `dragon_stir_160x192_3f.png` | Optional one-shot: eye cracks open → yawns plume of steam → settles (used on tribute stage-up) | 160×192 | 3 | P3 | requested |
| `fx_wisp_12x12_4f.png` | **Wisp** — tiny glowing spirit orb, bright white-cyan core with a 1-px soft halo; 4f glow pulse. Cargo icon is drawn separately above it. | 12×12 | 4 | P1 | integrated |
| `fx_wisp_returning_12x12_4f.png` | Same wisp, **red/pink glow** (delivery refused, returning) | 12×12 | 4 | P1 | integrated |
| `npc_disciple_meditate_32x32_4f.png` | NEW — Disciple cultivating: young robed figure (white robe, red sash), cross-legged, qi motes drifting up (idle 4f) | 32×32 | 4 | P3 | requested |

### 4.5 UI icon keys — category `ui` (icons, 32×32, centre pivot, 1 frame each)

Icons share one visual language: 1 px ink outline, flat 3–4-tone fill, readable at 16–24 px. Area icons are also shown beside the island name; action icons beside the verb ("Chop", "Mine").

#### 4.5.1 Island and action icons

| Key | Depicts | Pri | Status |
|---|---|---|---|
| `ui_area_center` | Spirit tree + small pagoda silhouette (Center) | P3 | requested |
| `ui_action_center` | Hatchet (Chop) | P3 | requested |
| `ui_area_farm` | Rice sprout in a paddy square (Farm) | P3 | requested |
| `ui_action_farm` | Rice sickle / sheaf (Harvest) | P3 | requested |
| `ui_area_mine` | Grey rock peak with pickaxe (Mine) | P3 | requested |
| `ui_action_mine` | Pickaxe (Mine) | P3 | requested |
| `ui_area_fishing` | Fishing rod with a hook and ripple (Fishing) | P3 | requested |
| `ui_action_fishing` | Fishing line and hook (Reel) | P3 | requested |
| `ui_area_volcano` | Smoking volcano cone with lava (Volcano) | P3 | requested |
| `ui_action_volcano` | Pickaxe with orange spark (Mine) | P3 | requested |
| `ui_area_grove` | Bamboo stalks (Spirit Grove) | P3 | requested |
| `ui_action_grove` | Leaf bundle (Gather) | P3 | requested |
| `ui_area_celestial` | Cloud with a star above a peak (Celestial Peak) | P3 | requested |
| `ui_action_celestial` | Sparkle / star-dust (Gather) | P3 | requested |

#### 4.5.2 Vow icons

| Key | Depicts | Pri | Status |
|---|---|---|---|
| `ui_vow_burden` | Heavy pack / sack (Vow of Burden) | P3 | requested |
| `ui_vow_coldhearth` | Ice-blue cold brazier / snowflake (Vow of the Cold Hearth) | P3 | requested |
| `ui_vow_restless` | Angry dragon eye (Vow of the Restless Dragon) | P3 | requested |
| `ui_vow_solitude` | Single candle / lone lantern (Vow of Solitude) | P3 | requested |

#### 4.5.3 Quest icons (14 quests)

| Key | Depicts | Pri | Status |
|---|---|---|---|
| `ui_quest_wood` | Log (First timber) | P3 | requested |
| `ui_quest_leaves` | Leaf (Bush whacker) | P3 | requested |
| `ui_quest_dragon1` | Dragon head, one eye opening (Wake the sleeper) | P3 | requested |
| `ui_quest_fox` | Fox head (Fox hunt) | P3 | requested |
| `ui_quest_build` | Hammer and nails (Raise a building) | P3 | requested |
| `ui_quest_upgrade` | Altar / shrine (First insight) | P3 | requested |
| `ui_quest_link` | Wisp lantern (Wisp wrangler) | P3 | requested |
| `ui_quest_explore` | Open lock / gate (Beyond the woods) | P3 | requested |
| `ui_quest_dragon2` | Dragon head, steam plume (Stone & clay for the dragon) | P3 | requested |
| `ui_quest_iron` | Iron bar with a magnet-like horseshoe (Iron for the dragon) | P3 | requested |
| `ui_quest_waters` | Fishing hook over water (Unlock the waters) | P3 | requested |
| `ui_quest_dragon3` | Dragon head, fully open eye (The dragon tastes iron) | P3 | requested |
| `ui_quest_weaver` | Rope knot / spool (Weaver's path) | P3 | requested |
| `ui_quest_cultivate` | Meditating figure (Gather disciples) | P3 | requested |

#### 4.5.4 HUD / general UI icons

| Key | Depicts | Pri | Status |
|---|---|---|---|
| `ui_hand` | Open hand (carry capacity pill) | P3 | requested |
| `ui_wisp` | Small cyan wisp orb (HUD/legend icon) | P3 | requested |
| `ui_sparkle` | Four-point gold sparkle | P3 | requested |
| `ui_lock` | Padlock (also the locked-island glyph, drawn at 64 px — deliver cleanly at 32 px) | P3 | requested |
| `ui_unlock` | Open padlock | P3 | requested |
| `ui_target` | Target / bullseye ring (milestone, quest goal) | P3 | requested |
| `ui_quests` | Scroll (quest panel chip) | P3 | requested |
| `ui_alert` | Red exclamation mark (claimable) | P3 | requested |
| `ui_ascend` | Taiji (yin-yang) with a golden rim (Ascension Points) | P3 | requested |
| `ui_disciple` | Head-and-shoulders silhouette (disciple count) | P3 | requested |
| `ui_add` | Plus sign | P3 | requested |
| `ui_demolish` | Trash bin / hammer-smash | P3 | requested |
| `ui_stats` | Bar chart | P3 | requested |
| `ui_claim` | Check mark | P3 | requested |
| `ui_sprint` | Running figure / wind lines | P3 | requested |
| `ui_wilds` | Fog / mist cloud (locked "wilds") | P3 | requested |
| `ui_controls` | Joystick / mouse glyph | P3 | requested |
| `ui_build` | Building under construction with a crane (Build button) | P3 | requested |
| `ui_welcome_moon` | Crescent moon (welcome back / offline) | P3 | requested |
| `ui_link` | Chain link (wisp link row) | P3 | requested |
| `ui_gathering_stone` | Gathering Stone glyph (matches the building) | P3 | requested |

#### 4.5.5 Text glyphs (inline in text, **16×16**)

| Key | Depicts | Size | Pri | Status |
|---|---|---|---|---|
| `glyph_arrow` | Right arrow "→" (recipe flow), gold | 16×16 | P3 | requested |
| `glyph_cross` | Red cross "✗" (refused) | 16×16 | P3 | requested |
| `glyph_redo` | Circular arrow "↺" (reset) | 16×16 | P3 | requested |

#### 4.5.6 Perk icons (Ascension Shrine, 15)

| Key | Perk | Depicts | Pri | Status |
|---|---|---|---|---|
| `perk_haste` | Eternal Haste | Lightning bolt with gold trail | P3 | requested |
| `perk_hall` | Master's Hall | Small temple hall | P3 | requested |
| `perk_slumber` | Long Slumber | Moon and a "z" | P3 | requested |
| `perk_hands` | Fleet Hands | Two cupped hands | P3 | requested |
| `perk_frugal` | Frugal Frontier | Compass rose | P3 | requested |
| `perk_ember` | Ember Heart | Flame heart | P3 | requested |
| `perk_apgain` | Ascendant Insight | Glowing star | P3 | requested |
| `perk_autoboost` | Keen Automation | Cog with qi wisp | P3 | requested |
| `perk_regrow` | Deep Roots | Sprout with long roots | P3 | requested |
| `perk_gale` | Wisp Gale | Swirling wind spiral | P3 | requested |
| `perk_fury` | Battle Fury | Crossed swords | P3 | requested |
| `perk_bless` | Heaven's Favor | Shooting star over clouds | P3 | requested |
| `perk_bounty` | Astral Bounty | Meteor with sparkles | P3 | requested |
| `perk_paths` | Remembered Paths | Folded map / winding path | P3 | requested |
| `perk_legacy` | Legacy Automation | Gear with a scroll | P3 | requested |

#### 4.5.7 Altar upgrade-node icons (19)

| Key | Upgrade | Depicts | Pri | Status |
|---|---|---|---|---|
| `upg_hand` | Hand Size | Open hand with a plus | P3 | requested |
| `upg_wisps` | Wisp Haste | Lantern with speed lines | P3 | requested |
| `upg_affinity` | Dragon Affinity | Dragon coil with heart | P3 | requested |
| `upg_disciples` | Disciple Mastery | Meditating figure with star | P3 | requested |
| `upg_spd_c` | Regrow Speed (Center) | Stopwatch over a bush | P3 | requested |
| `upg_auto_c` | Automation (Center) | Gear with arrow | P3 | requested |
| `upg_act_fi` | Reel Speed | Reel with speed lines | P3 | requested |
| `upg_act_c` | Action Speed (Center) | Hatchet with speed lines | P3 | requested |
| `upg_quarry` | Quarry Output | Pickaxe and stone | P3 | requested |
| `upg_act_m` | Mine Speed | Hammer with speed lines | P3 | requested |
| `upg_spd_f` | Growth Speed | Water drop over sprout | P3 | requested |
| `upg_act_f` | Harvest Speed | Rice sheaf with speed lines | P3 | requested |
| `upg_auto_f` | Farm Automation | Plough / tractor-like cart | P3 | requested |
| `upg_spd_m` | Respawn Speed (Mine) | Rock with circular arrow | P3 | requested |
| `upg_spd_fi` | Bite Speed | Wave with fish | P3 | requested |
| `upg_auto_m` | Mine Automation | Mining cart with gear | P3 | requested |
| `upg_foe_cap` | Spirit Call | Fox head with plus | P3 | requested |
| `upg_foe_dmg` | Spirit Blade | Sword with glow | P3 | requested |
| `upg_foe_aoe` | Spirit Wave | Burst of concentric rings | P3 | requested |

**Counts of manifest keys covered:** items 46 + buildings 26 + nodes 14 + fixtures 3 + enemies 2 + dragon 2 + ui 53 + perks 15 + upgrades 19 + glyphs 3 = **183**. Node/enemy/dragon/fixture keys expand into the sized/animated filenames in 4.3/4.4.

### 4.6 Key-to-file cross-reference for the manifest (nodes, fixtures, creatures)

| Manifest key | Delivered file(s) |
|---|---|
| `node_bush` | `node_bush_32x48_2f.png` |
| `node_crop` | `node_crop.png` |
| `node_cotton` | `node_cotton.png` |
| `node_ore` | `node_ore_1x1_32x48_2f.png`, `node_ore_2x2_64x80_2f.png` |
| `node_ironvein` | `node_ironvein_64x80_2f.png` |
| `node_jadevein` | `node_jadevein_64x80_2f.png` |
| `node_fish` | `node_fish_32x48_4f.png` |
| `node_algae` | `node_algae_32x48_4f.png` |
| `node_obsidian` | `node_obsidian_1x1_32x48_2f.png`, `node_obsidian_2x2_64x80_2f.png` |
| `node_firevein` | `node_firevein_64x80_2f.png` |
| `node_herbbush` | `node_herbbush_32x48_2f.png` |
| `node_bamboostalk` | `node_bamboostalk_1x1_32x48_2f.png`, `node_bamboostalk_2x2_64x80_2f.png` |
| `node_starrock` | `node_starrock_1x1_32x48_2f.png`, `node_starrock_2x2_64x80_2f.png` |
| `node_moonshrub` | `node_moonshrub_32x48_2f.png` |
| `fix_quarry` / `fix_spirittree` / `fix_spring` | `fix_quarry.png` / `fix_spirittree.png` / `fix_spring.png` |
| `enemy_fox` / `enemy_boar` | `enemy_fox_{idle,move,hit,die}_*`, `enemy_boar_{idle,move,hit,die}_*` |
| `dragon_sleeping` / `dragon_awake` | `dragon_sleeping_160x192_4f.png` / `dragon_awake_160x192_4f.png` |

---

## 5. UI art — category `ui`

The game UI is Unity uGUI. Provide **9-slice** sprites with the stated insets (L,R,T,B in px); Unity sets the Sprite Border to those values and stretches the centre. Every 9-slice centre must tile/stretch cleanly (flat or fine pattern with no obvious features). Theme: dark-ink lacquer panels with jade frames and gold trim; parchment scroll for story / lore panels.

| Key / file | Depicts | Size | 9-slice insets L,R,T,B | Pri | Status |
|---|---|---|---|---|---|
| `ui_panel_scroll_64x64.png` | **Scroll panel**: aged rice-paper parchment with rolled wooden rod ends top and bottom, soft vignette; for story modals, help, welcome | 64×64 | 20,20,18,18 | P3 | requested |
| `ui_panel_jade_64x64.png` | **Jade-framed panel**: dark ink-lacquer centre (P02), 3-px jade frame (P09/P10/P11) with gold (P24) corner studs; main modal box | 64×64 | 16,16,16,16 | P3 | requested |
| `ui_panel_dark_32x32.png` | Plain dark panel: P02 fill, 1-px P04 border, rounded 2-px corners; for strips and minor popups | 32×32 | 8,8,8,8 | P3 | requested |
| `ui_bottombar_bg_64x48.png` | **Bottom-bar background**: lacquered wood/ink strip with a thin gold top line and tiny cloud-scroll pattern; stretches horizontally | 64×48 | 12,12,10,0 | P3 | requested |
| `ui_questpanel_frame_64x64.png` | **Quest panel frame**: scroll-ish, parchment-dark panel with a cinnabar seal-stamp corner | 64×64 | 20,20,20,20 | P3 | requested |
| `ui_tooltip_frame_24x24.png` | **Tooltip frame**: near-black P01 box, 1-px P04 border, small cinnabar corner mark | 24×24 | 8,8,8,8 | P3 | requested |
| `ui_handchip_frame_24x24.png` | Hand-chip frame (follows cursor): dark rounded box with a thin gold border | 24×24 | 8,8,8,8 | P3 | requested |
| `ui_pill_green_24x24.png` | Status pill, green rim (hand pill OK / area) | 24×24 | 8,8,8,8 | P3 | requested |
| `ui_pill_gold_24x24.png` | Status pill, gold rim (hand ≥ 90%, rewards) | 24×24 | 8,8,8,8 | P3 | requested |
| `ui_pill_red_24x24.png` | Status pill, red rim (hand full, error) | 24×24 | 8,8,8,8 | P3 | requested |
| `ui_pill_purple_24x24.png` | Status pill, violet rim (blessing / buff) | 24×24 | 8,8,8,8 | P3 | requested |

### 5.1 Buttons (each state is a separate file)

| Key / file | State | Size | 9-slice insets | Pri | Status |
|---|---|---|---|---|---|
| `ui_btn_normal_48x24.png` | Normal — jade-rimmed dark button, subtle top highlight | 48×24 | 8,8,8,8 | P3 | requested |
| `ui_btn_hover_48x24.png` | Hover — brighter jade rim and fill | 48×24 | 8,8,8,8 | P3 | requested |
| `ui_btn_pressed_48x24.png` | Pressed — content shifted down 1 px, darker, inset shadow | 48×24 | 8,8,8,8 | P3 | requested |
| `ui_btn_disabled_48x24.png` | Disabled — desaturated grey-blue, no highlight | 48×24 | 8,8,8,8 | P3 | requested |
| `ui_btn_primary_normal_48x24.png` | Primary (Claim / Ascend / Unlock) — gold rim, cinnabar-tinted fill | 48×24 | 8,8,8,8 | P3 | requested |
| `ui_btn_primary_hover_48x24.png` | Primary hover | 48×24 | 8,8,8,8 | P3 | requested |
| `ui_btn_primary_pressed_48x24.png` | Primary pressed | 48×24 | 8,8,8,8 | P3 | requested |
| `ui_btn_primary_disabled_48x24.png` | Primary disabled | 48×24 | 8,8,8,8 | P3 | requested |
| `ui_btn_close_20x20.png` | Round "✕" close button (fixed size, 4 states in `ui_btn_close_20x20_4f.png`: normal/hover/pressed/disabled) | 20×20 | none | P3 | requested |
| `ui_btn_toggle_on_48x24.png` | Toggle button, on (jade filled) — used for Build / Demolish | 48×24 | 8,8,8,8 | P3 | requested |
| `ui_btn_toggle_danger_48x24.png` | Toggle on in danger red (Demolish on) | 48×24 | 8,8,8,8 | P3 | requested |

### 5.2 Cards, slots, bars, frames

| Key / file | Depicts | Size | 9-slice insets | Pri | Status |
|---|---|---|---|---|---|
| `ui_card_normal_48x48.png` | Build-menu card, normal (dark lacquer, 1-px jade line) | 48×48 | 10,10,10,10 | P3 | requested |
| `ui_card_target_48x48.png` | Card for a quest/milestone target (gold border + inner glow) | 48×48 | 10,10,10,10 | P3 | requested |
| `ui_card_affordable_48x48.png` | Card you can afford (green border) | 48×48 | 10,10,10,10 | P3 | requested |
| `ui_card_dim_48x48.png` | Card dimmed (can't afford, 62% look) | 48×48 | 10,10,10,10 | P3 | requested |
| `ui_iconframe_36x36.png` | Item icon frame (holds a 32×32 icon; 2-px border): normal | 36×36 | none (fixed) | P3 | requested |
| `ui_iconframe_gold_36x36.png` | Icon frame, gold (active recipe / selected) | 36×36 | none | P3 | requested |
| `ui_iconframe_locked_36x36.png` | Icon frame, locked (grey with chain corner) | 36×36 | none | P3 | requested |
| `ui_recipe_cell_58x58.png` | Recipe-picker cell (58×58, frame for 32 px output icon) | 58×58 | none | P3 | requested |
| `ui_recipe_cell_active_58x58.png` | Recipe cell, active (gold border glow) | 58×58 | none | P3 | requested |
| `ui_treenode_52x52.png` | Altar upgrade-tree node base (dark rounded square); border colour added by tint | 52×52 | none | P3 | requested |
| `ui_treenode_border_green_56x56.png` | Node selectable border (green) | 56×56 | none | P3 | requested |
| `ui_treenode_border_gold_56x56.png` | Node maxed border (gold) | 56×56 | none | P3 | requested |
| `ui_treenode_border_red_56x56.png` | Node locked / mystery border (red) | 56×56 | none | P3 | requested |
| `ui_treenode_brackets_72x72.png` | Hover/selected corner brackets (4 white corner arms) | 72×72 | none | P3 | requested |
| `ui_tree_edge_8x8.png` | Tileable connector line segment between tree nodes (cinnabar-tinted stone beads) | 8×8 | none | P3 | requested |
| `ui_progress_track_16x8.png` | **Progress bar track** (dark, inset) | 16×8 | 3,3,3,3 | P3 | requested |
| `ui_progress_fill_gold_16x8.png` | Progress fill, gold (crafting) | 16×8 | 3,3,3,3 | P3 | requested |
| `ui_progress_fill_green_16x8.png` | Progress fill, green (quests, food) | 16×8 | 3,3,3,3 | P3 | requested |
| `ui_progress_fill_red_16x8.png` | Progress fill, red (empty / warning) | 16×8 | 3,3,3,3 | P3 | requested |
| `ui_hp_pip_8x8_2f.png` | HP pip: frame 1 filled red, frame 2 empty grey (enemy health) | 8×8 | none | P3 | requested |
| `ui_badge_count_24x16.png` | Small count badge (gold text sits on it) | 24×16 | 6,6,6,6 | P3 | requested |
| `ui_unlockbtn_frame_48x48.png` | Island-unlock button frame (dashed look), states via tint | 48×48 | 12,12,12,12 | P3 | requested |
| `ui_checkbox_16x16_2f.png` | Checkbox off/on (vows) | 16×16 | none | P3 | requested |
| `ui_scrollbar_8x32.png` | Scrollbar track + handle (9-slice both) | 8×32 | 3,3,3,3 | P3 | requested |
| `ui_scrim_16x16.png` | Dither-pattern modal scrim tile (1-px checker, 50% of P01) | 16×16 | none | P3 | requested |

### 5.3 Fuel rack and cursor

| Key / file | Depicts | Size | Notes | Pri | Status |
|---|---|---|---|---|---|
| `ui_fuel_rack_96x64.png` | **Fuel-rack slot panel**: a 3-column × 2-row wooden/iron rack (6 slots of 32×32) drawn to the **left of a burner, outside its footprint** (top edge flush with the building top). Dark charred-wood back, warm orange rim. | 96×64 | fixed size; 3 cols × 2 rows | P2 | requested |
| `ui_fuel_slot_32x32.png` | Single empty fuel slot (sunken, 1-px orange line); used if slots are placed individually | 32×32 | | P2 | requested |
| `ui_cursor_hand_open_32x32.png` | **Cursor: open hand** (default over the world; pointing hand with open palm). Hotspot at (**12, 4**) | 32×32 | hotspot noted | P3 | requested |
| `ui_cursor_hand_closed_32x32.png` | **Cursor: closed/grab hand** (while holding the mouse button, vacuuming / dragging). Hotspot (**12, 8**) | 32×32 | | P3 | requested |
| `ui_cursor_target_32x32.png` | Optional crosshair cursor for placement / link picking. Hotspot (16,16) | 32×32 | | P3 | requested |

### 5.4 Font recommendation

| Use | Font | Licence | Notes |
|---|---|---|---|
| Body, labels, numbers | **Pixelify Sans** (Google Fonts) | SIL Open Font License 1.1 (free for commercial use; bundle the licence text) | Clear lowercase and digits at 10–16 px; weights 400–700. Used with integer-multiple sizes (e.g. 8/16/24 px) so it stays crisp. |
| Headlines / small caps | **Silkscreen** (Google Fonts) | SIL OFL 1.1 | Only for short titles in capitals. |
| Fallback | **m6x11** or **Press Start 2P** | free/OFL (check each page) | Only if the first choice lacks a glyph. |

Text colours: body `#E8ECF3` (P07), muted `#7A809A` (P05), gold `#F2C94C` (P24), danger `#C8322B` on dark / `#EE6A4A` on light. Always use a 1-px P01 outline or drop-shadow for legibility over the world.

---

## 6. FX sprites — category `fx`

Particles and effects. Centre pivot. 4-alpha rule applies to glow parts only. Colour-tint-friendly: where noted "white" the art is white/greyscale and Unity tints it.

| Key / file | Depicts | Size | Frames | Pri | Status |
|---|---|---|---|---|---|
| `fx_spark_burst_32x32_6f.png` | **Spark burst**: white-gold radial sparks expanding and fading (swing hit, click feedback) | 32×32 | 6 | P3 | requested |
| `fx_spark_dot_4x4.png` | Single bright spark particle, white (Unity tints and fades; gravity particle) | 4×4 | 1 | P3 | requested |
| `fx_gold_sparkle_16x16_4f.png` | **Gold sparkle**: four-point gold star twinkle (rare drop, quest reward, valuable item) | 16×16 | 4 | P3 | requested |
| `fx_pickup_pulse_24x24_4f.png` | Small green ring pulse where items are vacuumed | 24×24 | 4 | P3 | requested |
| `fx_hit_slash_32x32_3f.png` | White slash arc on enemy hit | 32×32 | 3 | P3 | requested |
| `fx_dust_puff_24x24_5f.png` | Dust puff (building placed, node relocates) | 24×24 | 5 | P3 | requested |
| `fx_build_complete_96x96_6f.png` | Construction complete: gold-white ring + sparkles | 96×96 | 6 | P3 | requested |
| `fx_qi_swirl_96x96_6f.png` | **Qi swirl for crafting**: translucent cyan-white spiral ribbon rising (white/greyscale, tinted per recipe) over a 3×3 building | 96×96 | 6 | P3 | requested |
| `fx_qi_swirl_small_32x32_6f.png` | Same, small (1×1 buildings) | 32×32 | 6 | P3 | requested |
| `fx_craft_done_32x32_6f.png` | "Ding": ring burst at output on batch complete | 32×32 | 6 | P3 | requested |
| `fx_levelup_glow_64x64_6f.png` | **Level-up glow**: golden radial pulse and rising motes (Altar upgrade applied, perk bought) | 64×64 | 6 | P3 | requested |
| `fx_ascend_pillar_96x256_6f.png` | Pillar of gold-white qi rising from the Gate (ascension) | 96×256 | 6 | P3 | requested |
| `fx_dragon_breath_32x32_4f.png` | Steam/mist puff from the dragon nostril | 32×32 | 4 | P3 | requested |
| `fx_smoke_16x16_4f.png` | Rising grey smoke puff (chimneys, charcoal pit) — white/greyscale | 16×16 | 4 | P3 | requested |
| `fx_flame_16x24_4f.png` | Small flame loop (fuel slot burning, forge mouth) | 16×24 | 4 | P3 | requested |
| `fx_node_pad_32x12.png` | Ground-hugging soft pad ellipse under interactive nodes (4-alpha rule, green-white tint) | 32×12 | 1 | P3 | requested |
| `fx_shadow_ellipse_32x12.png` | Generic soft blob shadow for creatures/items (4-alpha, black) | 32×12 | 1 | P3 | requested |
| `fx_quest_ring_64x64_4f.png` | Gold pulsing ring marker around quest targets (4f pulse) | 64×64 | 4 | P3 | requested |

### 6.1 Harvest chips (per material)

Each is a strip of **4 different small fragments**, 8×8 each (`8x8_4f`); Unity picks a frame at random and flings it as a gravity particle (about 5–8 per hit). Keep fragment shapes distinct per material, coloured by the item's palette.

| Key / file | Material | Depicts | Pri | Status |
|---|---|---|---|---|
| `fx_chip_wood_8x8_4f.png` | Wood (Spirit Tree, logs) | Bark chips and splinters, tan/brown | P3 | requested |
| `fx_chip_leaf_8x8_4f.png` | Leaves (bush, herb) | Green leaf fragments, one with gold dot | P3 | requested |
| `fx_chip_stone_8x8_4f.png` | Stone (quarry) | Grey rock chips | P3 | requested |
| `fx_chip_clay_8x8_4f.png` | Clay | Red-brown crumbs | P3 | requested |
| `fx_chip_ore_8x8_4f.png` | Iron ore | Dark rock with silver glints | P3 | requested |
| `fx_chip_jade_8x8_4f.png` | Jade | Bright green translucent slivers | P3 | requested |
| `fx_chip_obsidian_8x8_4f.png` | Obsidian | Black shards, violet sheen | P3 | requested |
| `fx_chip_ember_8x8_4f.png` | Firestone / fire vein | Orange sparks and ember bits | P3 | requested |
| `fx_chip_bamboo_8x8_4f.png` | Bamboo | Pale green segments / shreds | P3 | requested |
| `fx_chip_petal_8x8_4f.png` | Moonpetal / herb / cotton | White-pink petals and fluff | P3 | requested |
| `fx_chip_star_8x8_4f.png` | Star rock | Violet-white star shards | P3 | requested |
| `fx_chip_water_8x8_4f.png` | Water / fish / algae | Blue droplets and splash bits | P3 | requested |
| `fx_chip_rice_8x8_4f.png` | Rice / crop | Golden grain flecks | P3 | requested |
| `fx_chip_scale_8x8_4f.png` | Dragon scale / spirit essence | Gold flakes and cyan motes | P3 | requested |

### 6.2 Floating-text style (spec, no sprite)

Floating numbers/labels (e.g. "+2 Wood", "Hand full") rise 30 px over 0.85 s and fade. Use the UI font, fill colour + 1-px P01 outline + 1-px offset dark halo.

| Case | Fill colour |
|---|---|
| Normal pickup / yield | Green `#5FCF9C` (P11) |
| Valuable (essence, scale, pill, elixir, talisman, jade) | Gold `#F2C94C` (P24) |
| Metals (iron, steel, star, tools, glass) | Steel `#B7BCCD` (P06) |
| Error / refused / locked | Red `#EE6A4A` (P19) |
| Icon + text | 16 px item icon (downscaled by Unity from 32) at left, text after it |

---

## 7. How to deliver

Checklist before uploading each file:

- [ ] File is **PNG RGBA** (or `.aseprite`), sRGB, 8 bits per channel, **transparent background**.
- [ ] Canvas size **exactly** matches the table (and the filename's `WxH`).
- [ ] Strips are **horizontal**, equal frame size, no gaps; frame count in the filename (`_4f`).
- [ ] Filename is **lowercase with underscores**, uses the existing key, includes `WxH_Nf` for animated/multi-frame art.
- [ ] Only the 32 palette colours (plus the 4 allowed alpha steps for glows/mist/clouds). No anti-aliasing against transparency; no blur; no semi-transparent edge pixels on world art.
- [ ] Object base touches the bottom edge of the canvas; art stays within the footprint (+ allowed roof/canopy overhang).
- [ ] Tiles tile: centre variants seamless; clouds, trail, waterfall, veil, water seamless in the stated direction(s). Loop frames connect.
- [ ] Light from the upper left; 1-px outline rules applied.
- [ ] 9-slice art: centre region stretches without visible seams; insets as in the table.
- [ ] Uploaded to the right folder: `AI files/Idle Grounds Art/incoming/<category>/` where category is one of `items`, `buildings`, `nodes`, `fixtures`, `creatures`, `islands`, `sky`, `ui`, `fx`.

Where each table's files go:

| Section | Category folder |
|---|---|
| 4.1 Items | `items` |
| 4.2 Buildings + 3.4 Spirit Bridge | `buildings` |
| 4.3 Nodes | `nodes` |
| 4.3 Fixtures | `fixtures` |
| 4.4 Creatures, dragon, wisp, disciples | `creatures` (wisp/qi trail: `fx`) |
| 3.1 / 3.2 / 3.5 Islands, tiles, cliffs, veil, stele | `islands` |
| 3.3 Sky | `sky` |
| 4.5 / 5 UI | `ui` |
| 6 FX (incl. `fx_wisp_*`, `fx_qi_trail*`, chips) | `fx` |

Priorities: deliver **P1 first** (the first playable loop), then P2, then P3. Please upload in batches by category; for each batch note any deviation from this spec in the filename or in a short `NOTES.txt` next to the files.

Review process: files in `incoming` are imported into Unity, checked in-game, and the **Status** column here is updated (`requested` → `delivered` → `integrated`, or `needs-fix: <reason>`).

---

## 8. Changelog

| Version | Date | Change |
|---|---|---|
| v1 — initial request | 2026-10-04 | First full art request: 32-colour xianxia palette, technical spec, floating-island tilesets, sky, Spirit Bridge, all 183 manifest keys, UI chrome and FX. All statuses `requested`. |
| v2 — irregular islands | 2026-10-04 | New §3.0: islands are hand-painted irregular shapes (never squares); the coast is a visual margin 8–30 cells (bold lobes, bays, islets) around the square 93×93 playable area. Ground tilesets become 47-tile blob sets per biome (`island_<biome>_ground_blob_32x32_47f.png`; the 19-tile strip stays as a minimum); cliff rim must cover diagonals, corners and end caps; underside decorations must combine under any coastline. |

### Delivery log

Intake: `node tools/art-intake/pull.js` (Drive `incoming/<category>/` -> `Assets/_Project/Art/Incoming/`, log in `intake-log.json`), then Unity menu `Idle Grounds/Art/Integrate Incoming Art`.

| Date | File | Source zip | Status |
|---|---|---|---|
| 2026-10-04 | `item_wood.png` | `incoming/items/Idle-Grounds-Items-001.zip` | integrated (ItemAsset `wood` icon; ground items, hand, HUD, costs verified in Play mode) |
| 2026-10-04 | `item_leaves.png`, `item_stone.png`, `item_clay.png`, `item_plank.png`, `item_brick.png` | `incoming/items/Idle-Grounds-Items-002.zip` | integrated (ItemAsset icons; P1 items complete). Feedback: good — keep this style. |
| 2026-10-04 | `bld_gathering_stone.png`, `bld_wisp_lantern.png`, `bld_warding_seal.png` | `incoming/buildings/Idle-Grounds-Buildings-001.zip` | integrated (rendered full-size as the building body). Feedback: great style — keep going with the remaining P1 buildings (Altar, Workbench, Kiln, Storehouse), the Spirit Tree, bush, quarry rock and the Center island tiles. |
| 2026-10-04 | `bld_workbench.png`, `bld_storehouse.png` | `incoming/buildings/Idle-Grounds-Buildings-002.zip` | integrated (full-size). Feedback: excellent — exactly the target look. Next please: `bld_center` (Altar), `bld_kiln`, `fix_spirittree`, `node_bush`, `fix_quarry`, Center island tiles + cliff, sky layers, wisp and fox. |
| 2026-10-04 | `bld_center.png` (Altar), `bld_kiln.png` | `incoming/buildings/Idle-Grounds-Buildings-003.zip`, `-004.zip` | integrated (full-size). Feedback: the Altar is outstanding; the Kiln reads well. Next please: `fix_spirittree`, `node_bush`, `fix_quarry`, Center island ground blob tiles + cliff, sky layers, `fx_wisp`, `enemy_fox`, `dragon_sleeping`. |
| 2026-10-04 | `fix_spirittree.png`, `fix_quarry.png` | `incoming/fixtures/Idle-Grounds-Fixtures-001.zip` | integrated (correct scale in-world). Feedback: superb. Note: the decorative tree ring around islands still uses emoji — add `deco_tree_32x48_3f.png` (3 small round-canopy trees/pines, P2). Next please: `node_bush`, Center island ground blob tiles + cliff, sky layers, `fx_wisp`, `enemy_fox`, `dragon_sleeping`, `fix_spirittree_sparkle`. |
| 2026-10-04 | `node_bush_32x48_2f.png` | `incoming/nodes/Idle-Grounds-Nodes-001.zip` | integrated (both frames used: intact / trimmed after hits). Feedback: good. Next please: Center island ground blob tiles + cliff, sky layers, `fx_wisp`, `enemy_fox`, `dragon_sleeping`, `fix_spirittree_sparkle`, `deco_tree`. |
| 2026-10-04 | `island_center_cliff_32x32_8f.png` | `incoming/islands/Idle-Grounds-Islands-001.zip` | integrated. Bit convention from `blob_mask_reference.json` adopted as canonical (see §3.0). |
| 2026-10-04 | `island_center_ground_blob_32x32_47f.png` | `incoming/islands/Idle-Grounds-Islands-001.zip` | superseded by v2 (Islands-002) — was rejected: the mask mapping is correct, but the art does not work in game: (1) every frame is the same repeating teal "mandala" texture — it reads as a tiled pattern, not grass; it must be soft **jade-green grass** matching the Center palette (P13 P14 P15) with organic, non-repeating detail and the 3 centre-fill variants; (2) coast edges are hard **square 8-px cut-outs**, so a coastline looks like a row of disconnected blocks — edges must be **organic and rounded**: a lip of grass curling over a 1–2 px earthy rim (P20/P21), rounded outer corners, soft inner corners, and edge tiles must connect seamlessly with each other along a straight coast; (3) the fully-surrounded frame (mask 255) must be seamless plain grass with only sparse detail. A 4× review sheet of the delivered frames is in Drive `Idle Grounds Art/feedback/island_center_ground_blob_review_4x.png`. Use Stardew Valley / Eastward grass-cliff tilesets as the quality bar. |
| 2026-10-04 | `island_center_ground_blob_32x32_47f.png` v2, `island_center_ground_fill_32x32_3f.png` | `incoming/islands/Idle-Grounds-Islands-002.zip` | **integrated — one fix requested.** Big improvement: jade grass, rounded outer corners and the earthy rim read well, and the fill variants now cover the playable area. Remaining issue: frames for **straight edges** (e.g. masks 55 W-edge = N+E+S+NE+SE, 31 etc.) still round off / inset their corners at the tile seams, so a straight coastline shows a small notch of sky between every pair of tiles. Where a cardinal neighbour exists (N or S for a W/E edge, E or W for an N/S edge) the rim and grass must run **straight to the tile border** so neighbouring edge tiles join seamlessly; only round a corner where BOTH adjacent cardinals are empty (outer corner) or where the diagonal alone is empty (inner corner). Please deliver v3 of the blob strip (fill variants are fine — keep them). Bit convention unchanged. |
| 2026-10-04 | `island_center_ground_blob_32x32_47f.png` v3 | `incoming/islands/Idle-Grounds-Islands-003.zip` | **integrated — accepted.** Straight coasts are now seamless; corners and inner bites read correctly in game. Optional polish (low priority): at medium zoom the fill grass shows a regular repeating motif (paired light dots in a grid) — fewer, more irregular highlights would hide the tiling. Next please: the other 6 biome blob sets + cliffs in this same quality (Farm, Mine, Fishing, Volcano, Spirit Grove, Celestial Peak), sky layers, `fx_wisp`, `enemy_fox`, `dragon_sleeping`, `fix_spirittree_sparkle`, `deco_tree`. |
| 2026-10-04 | `fx_wisp_12x12_4f.png`, `fx_wisp_returning_12x12_4f.png` | `incoming/fx/Idle-Grounds-FX-001.zip` | integrated (4f loop at 8 fps, per-wisp phase; returning variant swaps in). Feedback: good and readable. Next please: other 6 biome blob sets + cliffs, sky layers, `enemy_fox`, `dragon_sleeping`, `fix_spirittree_sparkle`, `deco_tree`. |
| 2026-10-04 | `enemy_fox_idle_32x32_4f.png`, `enemy_fox_move_32x32_4f.png`, `enemy_fox_hit_32x32_2f.png` | `incoming/creatures/Idle-Grounds-Creatures-001.zip` | integrated (idle/move/hit animated, flips by direction). Feedback: readable and on-theme; the run cycle could use a bit more leg/tail motion (low priority). **Raised to requested:** `island_center_ground_fill_32x32_3f.png` v2 — over large open areas the fill shows a strong regular grid of paired light dots (see Drive `feedback/fill_grid_pattern.png`); please make the highlights sparse and irregular so tiling is invisible (keep palette). Next please: other 6 biome blob sets + cliffs, sky layers, `dragon_sleeping`, `fix_spirittree_sparkle`, `deco_tree`, `enemy_boar_*`. |
| 2026-10-05 | `island_center_ground_fill_32x32_3f.png` v2 | `incoming/islands/Idle-Grounds-Islands-004.zip` | **integrated — accepted.** Open grass now reads calm and even in game. **New request — blob v4:** the coast-edge frames of `island_center_ground_blob_32x32_47f.png` still use the old, darker grass with the regular paired-dot motif, so every coastline shows a darker dotted band next to the new fill (Drive `feedback/coast_band_mismatch.png`). Please repaint the grass inside all 47 blob frames with the v2 fill colour/texture (same base green, same sparse irregular detail) so edges blend invisibly; keep the v3 geometry, rim and mask order exactly. Also spread fill detail more evenly up to the tile borders (detail currently clusters in tile centres, leaving a faint grid at far zoom — minor). |
| 2026-10-05 | `island_center_ground_blob_32x32_47f.png` v4, `island_center_ground_fill_32x32_3f.png` v2.1 | `incoming/islands/Idle-Grounds-Islands-005.zip` | Fill v2.1: **integrated — accepted** (calm, tiles well). Blob v4: **REJECTED — regression.** The delivered PNG is NOT the v3 geometry with repainted grass as the README claims: every frame is again the teal (P10/P11) "mandala" pattern with square 8-px cut-outs from the rejected v1 (Drive `feedback/blob_v4_REJECTED_frames.png`, `blob_v4_REJECTED_ingame.png`). v3 restored in game. Please take the **v3 PNG (Islands-003)** as the base and change ONLY the grass pixels inside each frame to the v2.1 fill texture (same light jade base, sparse irregular P13/P15 detail) — keep v3 silhouettes, rim, alpha and frame order bit-for-bit. Sanity check before delivery: frame 46 (mask 255) must be pixel-identical to one of the three fill variants, and no pixel may use P10/P11/P12. |
| 2026-10-05 | `island_center_ground_blob_32x32_47f.png` v5 | `incoming/islands/Idle-Grounds-Islands-006.zip` | **integrated — accepted. Center ground tileset complete.** Automated checks pass (0 teal pixels, frame 46 = fill variant 0, silhouette identical to v3); in game the coast now blends seamlessly into the open grass. Thank you — use this exact process (v3 geometry, fill texture, sanity checks) for the other 6 biomes. Next please: Farm, Mine, Fishing, Volcano, Spirit Grove, Celestial Peak blob sets + fills + cliffs; sky layers; `dragon_sleeping`; `fix_spirittree_sparkle`; `deco_tree`; `enemy_boar_*`. |
| 2026-10-05 | `dragon_sleeping_160x192_4f.png` | `incoming/creatures/Idle-Grounds-Creatures-002.zip` | integrated (full-size Dragon building, breathing loop at 6 fps). Feedback: superb — the centrepiece of the Center island. Next please: `dragon_awake_160x192_4f.png` in the same pose/nest (eyes open, gold accents), then the other 6 biome tilesets, sky layers, `fix_spirittree_sparkle`, `deco_tree`, `enemy_boar_*`. Animated building variants (`bld_*_working_*`) are now supported in game whenever you get to them. |
| 2026-10-05 | `sky_gradient_512x1024.png` | `incoming/sky/Idle-Grounds-Sky-001.zip` | **integrated — accepted** (dusk dither reads well). Small fix wanted: the moon/sun-haze is baked into the gradient, so it gets stretched into an oval when the gradient fills a 16:9 screen — please deliver the gradient WITHOUT the moon plus a separate `sky_moon_96x96.png` (round, centre pivot) that the game places itself. |
| 2026-10-05 | `sky_clouds_far/mid/near_1024x256.png` | `incoming/sky/Idle-Grounds-Sky-002 (1).zip` | **REJECTED — please redo** (placeholder clouds restored; see Drive `feedback/sky_clouds_v1_REJECTED_ingame.png`, `sky_v1_composite.png`). Issues: (1) every cloud is the same rounded blob repeated at the same size/spacing — reads as a pattern, not a sky; (2) flat **purple ellipses** under clouds and pasted **white rectangles** look like errors; (3) the far layer ends in a hard flat horizontal band; (4) no dithered shading — clouds are flat discs. Wanted: varied cumulus silhouettes (big/small, overlapping, ragged tops), shading in 3–4 tones with dithered transitions (P06/P07 lit, P05 shadow, a touch of P28/P29 rim light), soft 4-step alpha at the bottoms, NO flat ellipses/rectangles, the far layer a low hazy bank that fades out (no hard edge), the near layer sparse wispy streaks. Each layer must tile horizontally. Reference mood: shan-shui sea of clouds. |
| 2026-10-05 | `sky_clouds_far/mid/near_1024x256.png` v2, `sky_gradient_512x1024.png` v2 (moonless), `sky_moon_96x96.png` | `incoming/sky/Idle-Grounds-Sky-003.zip` | **integrated — accepted, excellent.** The cloud sea now looks great in game (varied cumulus, dithered shading, hazy far bank, wispy near layer). The moon is placed by the game. Next: `sky_peaks_1024x256.png` (distant shan-shui spires) is now the most visible placeholder — the current smooth vector-like silhouettes clash with the pixel clouds; please make it the next sky item. |
| 2026-10-05 | `bld_dragon.png` | `incoming/buildings/Idle-Grounds-Buildings-005.zip` | integrated (compatibility key, frame 1 of dragon_sleeping). |
| 2026-10-05 | `island_underside_rock_256x192.png`, `island_underside_roots_192x160.png` | `incoming/islands/Idle-Grounds-Islands-008.zip` | integrated — excellent; hung at native scale over a solid tapering underside body (35% of coast width). New placeholder to replace: `island_underside_fill_32x32_3f.png` (3 seamless dark earth/rock fill variants, P02–P04/P20–P21, dithered shading, tiles in both axes) — it fills the inverted-mountain body behind your hanging pieces. |
| 2026-10-05 | `island_center_path_32x32_15f.png`, `island_center_plaza_32x32_3f.png` | `incoming/islands/Idle-Grounds-Islands-007.zip` | delivered (stored; paths/plaza are not painted in game yet). Note for a later pass: both read plain — flagstones need more stone shading/variation and the plaza more visible gold bagua engraving. Low priority. |
| 2026-10-06 | `sky_peaks_1024x256.png`, `dragon_awake_160x192_4f.png`, `deco_tree_32x48_3f.png` | `incoming/sky/Idle-Grounds-Sky-004.zip`, `creatures/...-Creatures-003.zip`, `fixtures/...-Fixtures-002.zip` | integrated — all excellent. The sky is now complete (gradient, moon, peaks, 3 cloud layers). Awake dragon swaps in when the dragon wakes; decorative trees replace the emoji tree ring. Next please: the other 6 biome tilesets (blob + fill + cliff, same process as Center), `island_underside_fill_32x32_3f.png`, `fix_spirittree_sparkle`, `enemy_boar_*`, remaining P2 buildings/nodes/items. |
| 2026-10-07 | Islands-009…014: `island_underside_fill`, Farm/Mine/Fishing/Volcano/Grove `*_ground_blob_47f` + `*_ground_fill_3f` + `*_cliff_8f` | `incoming/islands/Idle-Grounds-Islands-009…014.zip` | All pass the automated checks (silhouettes identical to Center v5, frame 46 = fill v0). **Accepted:** underside fill, all 5 cliffs, Mine + Volcano ground (provisional — could be a touch calmer). **REJECTED — please redo Farm, Fishing, Spirit Grove ground (blob + fill):** in game the fills tile visibly as a harsh grid (Drive `feedback/biome_farm_REJECTED.png`, `biome_fishing_REJECTED.png`, `biome_grove_REJECTED.png`). Farm: brick-like tan blocks with bright blue water stripes repeating every tile; Fishing: noisy high-contrast yellow/teal speckle; Grove: dense high-contrast moss. Follow the **Center v2.1 recipe**: ONE calm base colour per biome, sparse irregular detail (≤ ~8% of pixels), neighbouring tones at most one palette step apart, detail spread up to tile borders. Big features (paddy water, ponds, moss patches) must NOT be in the base fill — they belong to separate overlay/zone tiles (`zone_*_patch`, `island_fishing_water_4f`). Keep the v5 geometry/rim/mask order and the frame-46 = fill rule. Celestial Peak set still outstanding. |
| 2026-10-07 | Items-003…005 (17 items: wheat, algae, cotton, fish, iron bar/ore, sand, spirit essence, water, bamboo, glass, jade shard, paper, spirit herb, spirit jade, spirit stone, tools); Buildings-006 (`bld_mill`, `bld_loom`, `bld_paper_mill`, `bld_infusion_array` + `_working_96x128_4f`); Fixtures-003 `fix_spirittree_sparkle_160x192_4f`; Fixtures-004 `fix_spring` + `fix_spring_flow_64x80_4f`; Islands-015…018 (Farm/Fishing/Grove ground redo, Celestial blob + fill + cliff) | `incoming/items|buildings|fixtures|islands/...zip` | **Integrated — excellent batch.** Items and the four workshops are accepted (crisp, on-palette, working loops read well). The sparkle overlay plays at 8 fps above the tree, which replaces the emoji sparkles. The spring flow loop is in. **Accepted:** Farm ground (calm tan), Spirit Grove ground (calm jade), Celestial blob/fill/cliff (provisional: at mid zoom the stipple of the marble fill still forms a faint regular dot grid, so it could be made more irregular, but this is low priority). **Fishing ground — one more redo please:** it is calm now, but the flat saturated blue reads as **open water**, so the rocks, trees and buildings on it look like they float on a lake. The ground must read as *walkable shore*. Use pale wet sand / fine shingle: a P20/P21/P06 base, sparse irregular pebbles or shell flecks, and at most a hint of blue-grey damp patches (one palette step apart). Water belongs to the separate `island_fishing_water_4f` overlay. Keep the v5 geometry, mask order and the frame-46 = fill rule. Next please: Fishing ground v3, `enemy_boar_*`, then the remaining P2 buildings/nodes/items/UI/FX. |
| 2026-10-07 | Items-006…008 (23 items: cloth, flour, robe, rope, spirit buns, spirit wine, beast bait, charcoal, firestone, jade, qi elixir, vitality pill, beast bone, dragon scale, ember pill, moonpetal, obsidian, star fragment, star steel, stoneheart/swiftwind/verdant pill, talisman); Nodes-002 `node_crop`, `node_cotton`; Creatures-004 `enemy_boar_idle/move/hit`; Islands-019 Fishing ground v3 | `incoming/items|nodes|creatures|islands/...zip` | **Integrated, all accepted.** **Fishing ground v3** now reads as a walkable pale shingle shore, which fixes the open-water problem. **Items** are on-style. Dragon scale and obsidian are a bit noisy at 1×, but that's acceptable. **Boar:** strong silhouette, and the gold tusks read well. **Crop/cotton nodes** are wired and render on the Farm island once it is unlocked. Minor note: `node_crop` uses a diamond/isometric bund, while the rest of the world is 3/4 top-down, so a squarer bund would match better (low priority). Next please: the remaining P2 buildings, nodes and items, then UI chrome (§5) and FX (§6). |
| 2026-10-08 | Nodes-003 `node_ore_1x1_32x48_2f`, `node_ore_2x2_64x80_2f`, `node_ironvein_64x80_2f`, `node_jadevein_64x80_2f`; Nodes-004 `node_fish_32x48_4f`, `node_algae_32x48_4f` | `incoming/nodes/Idle-Grounds-Nodes-003.zip`, `-004.zip` | **Integrated and accepted.** The Mine nodes switch from intact to cracked after the first hit. Size-2 ore spawns now use the `_2x2` art (new game convention: `<key>_2x2` = the art for big spawner nodes). The koi and algae loops are wired on Fishing. Good readable silhouettes, and the iron/jade glow colours separate the veins well. Next please: `node_firevein` (Volcano) and any remaining P2 nodes, then the remaining P2 buildings and items, UI chrome (§5) and FX (§6). |
| 2026-10-08 | Nodes-005 `node_firevein_64x80_2f`, `node_obsidian_1x1/2x2`; Nodes-006 `node_bamboostalk_1x1/2x2`, `node_herbbush_32x48_2f` | `incoming/nodes/Idle-Grounds-Nodes-005.zip`, `-006.zip` | **Integrated and accepted.** Verified in Play: every Volcano and Grove node uses its art, and size-2 obsidian and bamboo spawns pick the `_2x2` sheets. Glowing magma cracks and the bamboo clumps read very well. Only two nodes are still placeholders: Celestial `node_starrock` and `node_moonshrub`. Please send those next, then the remaining P2 buildings and items, UI chrome (§5) and FX (§6). |
