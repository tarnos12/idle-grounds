# Idle Grounds

A browser-based sandbox/idle prototype with a cultivation/xianxia twist:
one continuous pannable world where resource nodes drop physical items on
the ground, a cursor "hand" carries them, and buildings are built by
feeding them resources. Feed the Sleeping Dragon 🐉 tribute to unlock new
recipes; fight Fox Spirits 🦊 for Spirit Essence; click the Spirit Tree
for wood.

**Vanilla HTML/CSS/JS — no build step, no dependencies.**

## Run

```
node server.js
```

then open http://localhost:5174 (or just double-click `index.html`).

## Controls

| Input | Action |
|---|---|
| Left click / hold | Harvest, attack, vacuum items, withdraw from storehouse |
| Right click / hold | Drop items / feed buildings, the Altar and the Dragon |
| WASD | Pan the camera (Shift toggles 2× sprint) |
| Mouse wheel | Zoom 1×–3× |
| B | Build menu |
| Click the Altar 🏛️ | Open the upgrade tree |
| F9 | Rendering self-diagnostic |

## For development

Read **[HANDOFF.md](HANDOFF.md)** first — it captures the architecture,
the performance invariants, the Firefox canvas-emoji fix, save migration
rules, testing knobs, and the agreed next steps.

Progress autosaves to localStorage every 5 s; the ↺ Reset button wipes it.
`DATA.TEST` in `js/data.js` holds the testing speed/cost multipliers.
