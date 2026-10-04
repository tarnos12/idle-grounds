# SFX

Rendered by `tools/sfx/render.js` (`node tools/sfx/render.js`) from the WebAudio
synthesis in `old-game/js/audio.js`. 44100 Hz mono 16-bit, JS master gain 0.18 baked in
(so peaks are quiet by design; set AudioSource volume accordingly).

| name | trigger event | audio.js line |
|---|---|---|
| harvest | harvest swing (JS pitch-jitters 620 +/- 60 Hz; centre rendered) | 66 |
| pickup | vacuum pickup / withdraw | 71 |
| swing | swing that dropped no loot | 76 |
| craft | converter finished a batch | 79 |
| build | building placed/completed | 84 |
| upgrade | altar upgrade applied | 89 |
| unlock | region unlocked | 95 |
| hit | fox spirit struck | 100 |
| kill | beast died | 104 |
| dragon | dragon stage-up / awakening | 109 |
| ascend | ascension | 114 |
| error | invalid action / refused / locked | 120 |
| click | UI tick (mute toggle, buttons) | 124 |
