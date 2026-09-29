#!/usr/bin/env node
/* Regression: v52 slice D — guidance. Progressive build-menu reveal
   (DATA.REVEAL), the 14-quest spine + old-index migration, quest item
   rewards, the per-run first-pavilion bun seed, 'new' badges, DATA.SOURCES coverage;
   fix wave: v50/v51 unstamped chains, iron quest, starter tags, link refusal,
   placeReason.
   Standalone vm sandbox — loads js/data.js, js/state.js, js/engine.js.
   Usage: node tests/test-v52-guidance.js [repoRoot]   (default /home/user/idle-grounds)
   Exits 1 on any FAIL. */
"use strict";
const fs = require("fs"), path = require("path"), vm = require("vm");

const REPO = process.argv[2] || "/home/user/idle-grounds";
const JS = f => path.join(REPO, "js", f);

let fails = 0;
const check = (name, ok, info) => {
  console.log((ok ? "PASS " : "FAIL ") + name + (info !== undefined ? "  — " + info : ""));
  if (!ok) fails++;
};

// boot(saved): saved = object served from localStorage (undefined = fresh game)
function boot(saved) {
  const s = {}; s.window = s; s.console = console; s.Date = Date; s.Math = Math; s.JSON = JSON;
  s.location = { reload() {} };
  let stored = saved === undefined ? null : JSON.stringify(saved);
  s.localStorage = {
    getItem: () => stored,
    setItem: (k, v) => { stored = v; }, removeItem: () => { stored = null; },
  };
  vm.createContext(s);
  for (const f of ["data.js", "state.js", "engine.js"])
    vm.runInContext(fs.readFileSync(JS(f), "utf8"), s, { filename: f });
  s.onGroundDrop = null; s.onSfx = null;
  s._stored = () => stored;
  return s;
}
const initAll = s => { for (const k of Object.keys(s.DATA.AREAS)) s.ENGINE.initArea(k); };
const catalog = s => s.ENGINE.buildingCatalog().map(b => b.id).sort().join(",");
const qIdx = (s, id) => s.DATA.QUESTS.findIndex(q => q.id === id);
// a v51-shaped save (no quest.chain / builtTypes / buildSeen / pavilionSeeded;
// v51 always serialised dragonBlessed)
const oldSave = (idx, extra) => Object.assign({
  areas: {}, world: { unlocked: { center: true } }, quest: { idx, hidden: false },
  stats: {}, introSeen: true, dragonBlessed: false,
}, extra || {});
// a v50 (master) save: same, but no dragonBlessed field at all
const v50Save = (idx, extra) => { const o = oldSave(idx, extra); delete o.dragonBlessed; return o; };

try {
  // (a) data shape -----------------------------------------------------------
  {
    const s = boot(), D = s.DATA;
    const ids = D.QUESTS.map(q => q.id);
    check("QUESTS: 14 quests in the v52 order",
      ids.join(",") === "wood,leaves,dragon1,fox,build,upgrade,link,explore,dragon2,iron,waters,dragon3,weaver,cultivate",
      ids.join(","));
    check("recipe + craft quests deleted", !ids.includes("recipe") && !ids.includes("craft"));
    const missing = Object.keys(D.ITEM_NAMES).filter(k => !D.SOURCES[k] || typeof D.SOURCES[k] !== "string");
    const stray = Object.keys(D.SOURCES).filter(k => !D.ITEM_NAMES[k]);
    check("SOURCES covers every item key", missing.length === 0, missing.join(",") || "ok");
    check("SOURCES has no stray keys", stray.length === 0, stray.join(",") || "ok");
    const badReveal = Object.entries(D.REVEAL).filter(([t, cs]) => !D.BUILDINGS[t] || !Array.isArray(cs) || !cs.length ||
      cs.some(c => !((c.quest && ids.includes(c.quest)) || (c.region && D.WORLD.regions[c.region]) || Number.isFinite(c.stage))));
    check("REVEAL: valid types + conditions", badReveal.length === 0, badReveal.map(x => x[0]).join(",") || "ok");
    const uncovered = Object.keys(D.BUILDINGS).filter(t => !D.REVEAL[t] && D.BUILDINGS[t].stageUnlock == null && D.BUILDINGS[t].unlocked);
    check("every buildable type is reveal- or stage-gated", uncovered.length === 0, uncovered.join(",") || "ok");
    const badRw = [];
    for (const q of D.QUESTS) {
      const r = q.reward; if (!r) continue;
      for (const t of r.reveal || []) {
        const viaQuest = (D.REVEAL[t] || []).some(c => c.quest === q.id);
        const viaStage = D.BUILDINGS[t] && (D.BUILDINGS[t].stageUnlock != null || (D.REVEAL[t] || []).some(c => c.stage != null));
        if (!D.BUILDINGS[t] || !(viaQuest || viaStage)) badRw.push(q.id + ":" + t);
      }
      for (const [it, n] of Object.entries(r.items || {}))
        if (!D.ITEM_NAMES[it] || !(n > 0 && n <= 10)) badRw.push(q.id + ":" + it + "x" + n);
    }
    check("quest rewards: reveal types real + REVEAL-backed, items <= 10", badRw.length === 0, badRw.join(",") || "ok");
    const badT = [];
    for (const q of D.QUESTS) {
      const t = q.target; if (!t) continue;
      const area = t.area || "center";
      if (!D.AREAS[area]) { badT.push(q.id + ":area"); continue; }
      if (t.kind === "fixture" && !(D.AREAS[area].fixtures || []).some(f => f.kind === t.id)) badT.push(q.id + ":fixture");
      else if (t.kind === "building" && !D.BUILDINGS[t.id]) badT.push(q.id + ":building");
      else if (t.kind === "enemyZone" && !D.AREAS[area].enemies) badT.push(q.id + ":enemyZone");
      else if (!["fixture", "building", "dragon", "altar", "enemyZone"].includes(t.kind)) badT.push(q.id + ":kind");
    }
    check("quest targets point at real things", badT.length === 0, badT.join(",") || "ok");
    const bad = [];
    initAll(s);
    D.QUESTS.forEach((q, i) => {
      try { const p = s.ENGINE.questProgress(i); if (!p || !Number.isFinite(p.cur) || !(p.need > 0)) bad.push(q.id); }
      catch (e) { bad.push(q.id + ":" + e.message); }
    });
    check("every quest goal() works on a fresh state", bad.length === 0, bad.join(",") || "ok");
  }

  // (b) reveal tiers per condition ---------------------------------------------
  {
    const s = boot(); initAll(s); s.ENGINE.setupStarterNetwork();
    const E = s.ENGINE, G = s.GS;
    check("minute 0: build menu is empty (starter network doesn't reveal)", catalog(s) === "", catalog(s));
    G.quest.idx = qIdx(s, "wood") + 1;
    check("quest wood claimed -> Storehouse only", catalog(s) === "storehouse", catalog(s));
    G.dragon.stage = 1;
    check("dragon stage 1 -> Forge (stageUnlock kept)", E.isBuildingUnlocked("forge"));
    G.quest.idx = qIdx(s, "fox") + 1;
    check("quest fox claimed -> Infusion Array", E.isBuildingUnlocked("infusion_array") && !E.isBuildingUnlocked("workbench"));
    G.quest.idx = qIdx(s, "build") + 1;
    const t1 = ["gathering_stone", "wisp_lantern", "warding_seal", "workbench", "kiln", "paper_mill", "charcoal_pit"];
    check("quest build claimed -> the 7 logistics/T1 cards", t1.every(t => E.isBuildingUnlocked(t)) && !E.isBuildingUnlocked("furnace_spirit"));
    G.quest.idx = qIdx(s, "link") + 1;
    check("quest link claimed -> Furnace Spirit", E.isBuildingUnlocked("furnace_spirit"));
    check("before Farm/waters: no Loom/Mill/Brewery", !E.isBuildingUnlocked("loom") && !E.isBuildingUnlocked("mill") && !E.isBuildingUnlocked("brewery"));
    G.world.unlocked.farm = true;
    check("region farm -> Loom, Mill, Brewery", E.isBuildingUnlocked("loom") && E.isBuildingUnlocked("mill") && E.isBuildingUnlocked("brewery"));
    G.world.unlocked.farm = false;
    G.quest.idx = qIdx(s, "waters") + 1;
    check("quest waters (no Farm) -> Loom, Mill, Brewery", E.isBuildingUnlocked("loom") && E.isBuildingUnlocked("mill") && E.isBuildingUnlocked("brewery"));
    check("no Mine -> no Jade Carver/Cauldron", !E.isBuildingUnlocked("jade_carver") && !E.isBuildingUnlocked("cauldron"));
    G.world.unlocked.mine = true;
    check("region mine -> Jade Carver, Cauldron", E.isBuildingUnlocked("jade_carver") && E.isBuildingUnlocked("cauldron"));
    check("no weaver/grove -> no Pavilion", !E.isBuildingUnlocked("meditation_pavilion"));
    G.world.unlocked.grove = true;
    check("region grove -> Pavilion", E.isBuildingUnlocked("meditation_pavilion"));
    G.world.unlocked.grove = false; G.quest.idx = qIdx(s, "weaver") + 1;
    check("quest weaver -> Pavilion", E.isBuildingUnlocked("meditation_pavilion"));
    G.dragon.stage = 2;
    check("stage 2: Algae Farm yes, T3 no", E.isBuildingUnlocked("algae_farm") && !E.isBuildingUnlocked("pill_furnace"));
    G.dragon.stage = 3;
    const t3 = ["pill_furnace", "star_anvil", "talisman_atelier", "herb_garden"];
    check("stage 3 -> Pill Furnace, Star Anvil, Talisman Atelier, Herb Garden", t3.every(t => E.isBuildingUnlocked(t)) && !E.isBuildingUnlocked("ascension_gate"));
    G.dragon.stage = 4;
    check("stage 4 -> Dragon Shrine, Ascension Gate", E.isBuildingUnlocked("dragon_shrine") && E.isBuildingUnlocked("ascension_gate"));
    check("Altar / Dragon never in the menu", !E.isBuildingUnlocked("center") && !E.isBuildingUnlocked("dragon"));
    // owning one (player-built) keeps a type revealed
    const s2 = boot(); initAll(s2);
    check("owned: GS.builtTypes reveals its type", !s2.ENGINE.isBuildingUnlocked("loom") &&
      ((s2.GS.builtTypes.loom = true), s2.ENGINE.isBuildingUnlocked("loom")));
    check("owned also covers stage-tier REVEAL types (builtTypes resets per run)", ((s2.GS.builtTypes.pill_furnace = true), s2.ENGINE.isBuildingUnlocked("pill_furnace")) === true);
    check("owned does NOT bypass a stageUnlock type (Forge)", ((s2.GS.builtTypes.forge = true), s2.ENGINE.isBuildingUnlocked("forge")) === false);
  }

  // (c) veterans ---------------------------------------------------------------
  {
    const s = boot(); initAll(s);
    const E = s.ENGINE, G = s.GS;
    G.ascensions = 1; G.quest.idx = s.DATA.QUESTS.length; G.dragon.stage = 0;
    const qr = ["storehouse", "workbench", "furnace_spirit", "loom", "jade_carver", "cauldron", "meditation_pavilion", "infusion_array"];
    check("veteran: quest/region-gated cards all revealed", qr.every(t => E.isBuildingUnlocked(t)), qr.filter(t => !E.isBuildingUnlocked(t)).join(","));
    check("veteran: stage gates still apply", !E.isBuildingUnlocked("pill_furnace") && !E.isBuildingUnlocked("ascension_gate") && !E.isBuildingUnlocked("forge"));
    G.dragon.stage = 3;
    check("veteran at stage 3: T3 revealed", E.isBuildingUnlocked("pill_furnace"));
    check("veteran: no 'new' badges / dot", !E.isBuildingNew("storehouse") && !E.buildMenuHasNew());
    const s2 = boot(); initAll(s2);
    s2.GS.quest.idx = s2.DATA.QUESTS.length;   // chain finished, never ascended
    check("chain finished (no ascension) counts as veteran for reveal", s2.ENGINE.isBuildingUnlocked("cauldron") && !s2.ENGINE.isBuildingUnlocked("star_anvil"));
  }

  // (d) every quest's required building is revealed when the quest is active,
  //     along every Farm/Mine/Fishing unlock order ----------------------------
  {
    const perms = [["farm", "mine", "fishing"], ["farm", "fishing", "mine"], ["mine", "farm", "fishing"],
                   ["mine", "fishing", "farm"], ["fishing", "farm", "mine"], ["fishing", "mine", "farm"]];
    const probs = [];
    for (const order of perms) {
      const s = boot(); initAll(s);
      const E = s.ENGINE, G = s.GS, Q = s.DATA.QUESTS;
      for (let i = 0; i < Q.length; i++) {
        // state as the player reaches quest i: everything before it claimed
        G.quest.idx = i;
        const claimed = id => qIdx(s, id) < i;
        G.dragon.stage = claimed("dragon3") ? 3 : claimed("dragon2") ? 2 : claimed("dragon1") ? 1 : 0;
        for (const r of ["farm", "mine", "fishing"]) G.world.unlocked[r] = false;
        // explore opens order[0]; the chain's own asks open the rest; the
        // player's free choice may open the next one in `order` at any time
        // AFTER explore — take the MINIMAL set (worst case for reveals)
        if (claimed("explore")) G.world.unlocked[order[0]] = true;
        if (claimed("iron")) G.world.unlocked.mine = true;
        if (claimed("waters")) G.world.unlocked.fishing = true;
        for (const t of Q[i].builds || [])
          if (!E.isBuildingUnlocked(t)) probs.push(order.join(">") + " q" + i + ":" + Q[i].id + " needs " + t);
      }
    }
    check("every quest's builds[] revealed when active (6 unlock orders)", probs.length === 0, probs.slice(0, 4).join("; ") || "ok");
  }

  // (e) old quest idx migration (v51 13-quest chain) ----------------------------
  {
    // v51: wood leaves dragon1 fox build upgrade link recipe craft explore waters weaver cultivate | done
    const expect = [0, 1, 2, 3, 4, 5, 6, 7, 7, 7, 8, 11, 13, 14];
    const V51 = ["wood", "leaves", "dragon1", "fox", "build", "upgrade", "link", "recipe", "craft", "explore", "waters", "weaver", "cultivate"];
    const got = [], back = [];
    for (let old = 0; old <= 13; old++) {
      const s = boot(oldSave(old));
      const ids = s.DATA.QUESTS.map(q => q.id);
      got.push(s.GS.quest.idx);
      // never moved back past a completed quest
      for (let k = 0; k < old; k++) { const j = ids.indexOf(V51[k]); if (j >= 0 && s.GS.quest.idx <= j) back.push(old + ">" + V51[k]); }
      // idempotent: save + reload keeps it
      s.SAVE.saveState();
      const s2 = boot(JSON.parse(s._stored()));
      if (s2.GS.quest.idx !== s.GS.quest.idx) back.push(old + ":reload " + s2.GS.quest.idx);
      if (s2.GS.quest.chain !== s.DATA.QUEST_CHAIN) back.push(old + ":chain");
    }
    check("old idx 0..13 -> new idx", got.join(",") === expect.join(","), got.join(","));
    check("migration never moves back past a completed quest; reload-stable", back.length === 0, back.join(",") || "ok");
    const s = boot(oldSave(99));
    check("corrupt old idx clamps to the end", s.GS.quest.idx === s.DATA.QUESTS.length, String(s.GS.quest.idx));
    const s3 = boot(oldSave(undefined));
    check("missing old idx -> 0", s3.GS.quest.idx === 0, String(s3.GS.quest.idx));
    // a v52 save (chain stamped) is taken as-is
    const s4 = boot(Object.assign(oldSave(9), { quest: { idx: 9, hidden: false, chain: 2 } }));
    check("v52 save idx kept (no remap)", s4.GS.quest.idx === 9, String(s4.GS.quest.idx));
    // ascension's fresh state (chain default + idx = length) survives a reload
    const s5 = boot(); initAll(s5);
    const f = s5.SAVE.fresh(); f.quest.idx = s5.DATA.QUESTS.length; f.ascensions = 1;
    const s6 = boot(JSON.parse(JSON.stringify(f)));
    check("ascended fresh state keeps the chain finished", s6.GS.quest.idx === s6.DATA.QUESTS.length, String(s6.GS.quest.idx));
    // old saves don't badge everything; they keep types they already placed
    const s7 = boot(oldSave(12, { areas: { center: { buildings: [{ id: 1, type: "loom", row: 30, col: 30, paid: {}, built: true }] } } }));
    s7.GS.quest.idx = 0;   // even pushed back to the start, the owned Loom stays revealed
    check("old save: owned Loom stays revealed", s7.ENGINE.isBuildingUnlocked("loom"));
    s7.GS.quest.idx = s7.DATA.QUESTS.length - 1;
    check("old save: no 'new' dot/badges", !s7.ENGINE.buildMenuHasNew() && !s7.ENGINE.isBuildingNew("storehouse"));
    const s8 = boot(); initAll(s8);
    check("fresh save: new fields default", s8.GS.quest.chain === s8.DATA.QUEST_CHAIN && JSON.stringify(s8.GS.builtTypes) === "{}" &&
      JSON.stringify(s8.GS.buildSeen) === "{}" && s8.GS.pavilionSeeded === false && s8.GS.perkShopSeen === false);
  }

  // (f) claimQuest rewards -----------------------------------------------------
  {
    const s = boot(); initAll(s);
    const E = s.ENGINE, G = s.GS;
    G.quest.idx = qIdx(s, "upgrade");
    check("claim refused while unfinished", E.claimQuest() === false && G.quest.idx === qIdx(s, "upgrade"));
    G.stats.upgradesApplied = 1; G.hand = [];
    const r = E.claimQuest();
    check("upgrade reward: +10 wood into an empty hand", r && r.toHand.wood === 10 && E.handCount("wood") === 10 &&
      Object.keys(r.dropped).length === 0 && G.quest.idx === qIdx(s, "upgrade") + 1, JSON.stringify(r));
    // hand nearly full: the rest drops beside the Altar
    const s2 = boot(); initAll(s2);
    const E2 = s2.ENGINE, G2 = s2.GS;
    G2.quest.idx = qIdx(s2, "upgrade"); G2.stats.upgradesApplied = 1;
    G2.hand = [{ item: "stone", qty: G2.handCap - 3 }];
    const g0 = G2.areas.center.ground.filter(g => g.item === "wood").length;
    const r2 = E2.claimQuest();
    const g1 = G2.areas.center.ground.filter(g => g.item === "wood").length;
    const altar = G2.areas.center.buildings.find(b => b.type === "center");
    const ac = E2.buildingCenterPx(altar);
    const near = G2.areas.center.ground.filter(g => g.item === "wood" && Math.hypot(g.x - ac.x, g.y - ac.y) < 6 * 32).length;
    check("reward overflow: 3 to hand, 7 beside the Altar", r2 && r2.toHand.wood === 3 && r2.dropped.wood === 7 &&
      g1 - g0 === 7 && near >= 7 && r2.at && r2.at.area === "center", JSON.stringify(r2) + ` ground+${g1 - g0} near=${near}`);
    // a quest without item rewards just advances
    const s3 = boot(); initAll(s3);
    s3.GS.quest.idx = 0; s3.GS.hand = [{ item: "wood", qty: 5 }];
    const r3 = s3.ENGINE.claimQuest();
    check("no-item quest: claim advances, hand untouched", r3 && r3.id === "wood" && s3.GS.quest.idx === 1 &&
      s3.ENGINE.handCount("wood") === 5 && Object.keys(r3.toHand).length === 0);
    check("claiming wood reveals the Storehouse", s3.ENGINE.isBuildingUnlocked("storehouse"));
  }

  // (g) first Meditation Pavilion seed (once) ------------------------------------
  {
    const s = boot(); initAll(s);
    const E = s.ENGINE, G = s.GS;
    G.quest.idx = qIdx(s, "weaver") + 1;   // pavilion revealed
    const buildOne = () => {
      let spot = null;
      for (let r = 26; r < 66 && !spot; r++) for (let c = 26; c < 66 && !spot; c++)
        if (E.canPlaceBuilding("center", r, c, "meditation_pavilion")) spot = { r, c };
      const b = E.placeBuilding("center", "meditation_pavilion", spot.r, spot.c);
      G.hand = Object.entries(s.DATA.BUILDINGS.meditation_pavilion.cost).map(([item, qty]) => ({ item, qty }));
      const cx = (b.col + 1.5) * 32, cy = (b.row + 1.5) * 32;
      for (let k = 0; k < 60 && !b.built; k++) E.dropFromHand("center", cx, cy);
      return b;
    };
    const p1 = buildOne();
    const cap = s.DATA.BUILDINGS.meditation_pavilion.roster.foodCap;
    check("first pavilion of the run built with a full larder (foodCap) of Spirit Buns", p1.built && cap === 20 && p1.buns === cap && G.pavilionSeeded === true, `built=${p1.built} buns=${p1.buns}`);
    check("building it records the type as player-owned", G.builtTypes.meditation_pavilion === true);
    const p2 = buildOne();
    check("second pavilion gets no seed", p2.built && (p2.buns || 0) === 0, `buns=${p2.buns}`);
    // an old save that already had a built pavilion never gets the seed
    const s2 = boot(oldSave(13, { areas: { center: { buildings: [{ id: 5, type: "meditation_pavilion", row: 30, col: 30, paid: {}, built: true, disciples: 1, buns: 0 }] } } }));
    check("old save with a built pavilion: pavilionSeeded migrates true", s2.GS.pavilionSeeded === true);
    const s3 = boot(oldSave(5));
    check("old save without one: pavilionSeeded false", s3.GS.pavilionSeeded === false);
  }

  // (h) 'new' badges + Build-button dot -------------------------------------------
  {
    const s = boot(); initAll(s);
    const E = s.ENGINE, G = s.GS;
    check("minute 0: nothing new", !E.buildMenuHasNew());
    G.quest.idx = 1;   // wood claimed -> storehouse revealed
    check("reveal lights the dot + badge", E.buildMenuHasNew() && E.isBuildingNew("storehouse"));
    E.markBuildListed();
    check("opening the menu clears the dot, card badge stays", !E.buildMenuHasNew() && E.isBuildingNew("storehouse"));
    E.markBuildSeen("storehouse");
    check("hover clears the card badge", !E.isBuildingNew("storehouse"));
    G.quest.idx = qIdx(s, "build") + 1;
    check("next reveal lights the dot again (only the new cards)", E.buildMenuHasNew() && E.isBuildingNew("kiln") && !E.isBuildingNew("storehouse"));
    s.SAVE.saveState();
    const s2 = boot(JSON.parse(s._stored()));
    check("seen state persists across reload", !s2.ENGINE.isBuildingNew("storehouse") && s2.ENGINE.isBuildingNew("kiln"));
  }
  // (i) v50 (master) vs v51 unstamped chains --------------------------------------
  {
    // v50: wood leaves dragon1 fox build upgrade link recipe craft explore cultivate | done
    const V50 = ["wood", "leaves", "dragon1", "fox", "build", "upgrade", "link", "recipe", "craft", "explore", "cultivate"];
    const expect50 = [0, 1, 2, 3, 4, 5, 6, 7, 7, 7, 8, 14];
    const got = [], back = [];
    for (let old = 0; old <= 11; old++) {
      const s = boot(v50Save(old));
      const ids = s.DATA.QUESTS.map(q => q.id);
      got.push(s.GS.quest.idx);
      for (let k = 0; k < old; k++) { const j = ids.indexOf(V50[k]); if (j >= 0 && s.GS.quest.idx <= j) back.push(old + ">" + V50[k]); }
      s.SAVE.saveState();
      const s2 = boot(JSON.parse(s._stored()));
      if (s2.GS.quest.idx !== s.GS.quest.idx) back.push(old + ":reload " + s2.GS.quest.idx);
    }
    check("v50 (no dragonBlessed) old idx 0..11 -> new idx", got.join(",") === expect50.join(","), got.join(","));
    check("v50 migration never moves back past a completed quest; reload-stable", back.length === 0, back.join(",") || "ok");
    const s0 = boot();
    check("SAVE exposes both legacy chains (11 + 13 ids)", s0.SAVE.QUEST_IDS_V50.length === 11 && s0.SAVE.QUEST_IDS_V51.length === 13 &&
      s0.SAVE.QUEST_IDS_V50.join() === V50.join());
    // finished v50 player (idx 11 = chain done) must NOT land on quest 12/14
    const sF = boot(v50Save(11));
    check("finished v50 save -> chain finished", sF.GS.quest.idx === sF.DATA.QUESTS.length, String(sF.GS.quest.idx));
    // unstamped veterans go to the end whatever their idx, both chains
    const vet = [];
    for (const mk of [oldSave, v50Save])
      for (const extra of [{ ascensions: 1 }, { won: true }, { ascensions: 3, won: true }])
        for (const old of [0, 5, 10]) {
          const s = boot(mk(old, extra));
          if (s.GS.quest.idx !== s.DATA.QUESTS.length) vet.push((mk === oldSave ? "v51" : "v50") + JSON.stringify(extra) + "@" + old + "->" + s.GS.quest.idx);
        }
    check("unstamped save with ascensions>0 or won -> end (both chains)", vet.length === 0, vet.join(";") || "ok");
    // a stamped (v52) veteran keeps its idx
    const sS = boot(Object.assign(oldSave(4, { ascensions: 1 }), { quest: { idx: 4, hidden: false, chain: 2 } }));
    check("stamped save is never remapped (even ascended)", sS.GS.quest.idx === 4, String(sS.GS.quest.idx));
  }

  // (j) 'iron' quest: bars fed to the dragon count; need from the tribute --------------
  {
    const s = boot(); initAll(s);
    const E = s.ENGINE, G = s.GS, D = s.DATA;
    const qi = qIdx(s, "iron"), q = D.QUESTS[qi];
    const need = E.dragonTribute(2).iron_bar;
    const expNeed = Math.max(1, Math.ceil(D.TEST.ENABLED ? D.DRAGON_STAGES[2].needs.iron_bar * D.TEST.costScale : D.DRAGON_STAGES[2].needs.iron_bar));
    check("dragonTribute(2).iron_bar = scaled stage-3 iron tribute", need === expNeed, `${need} vs ${expNeed}`);
    G.dragon.stage = 1; G.hand = [{ item: "iron_bar", qty: 2 }];
    let p = E.questProgress(qi);
    check("iron: need = stage-3 tribute, cur = bars in hand", p.need === need && p.cur === Math.min(need, 2), JSON.stringify(p));
    G.dragon.stage = 2; G.dragon.paid = { iron_bar: 3 }; G.hand = [{ item: "iron_bar", qty: 1 }];
    p = E.questProgress(qi);
    check("iron at stage 2: cur = hand + bars already paid", p.cur === Math.min(need, 4) && p.need === need, JSON.stringify(p));
    // feeding the dragon moves bars hand -> paid without resetting progress
    G.dragon.paid = {}; G.hand = [{ item: "iron_bar", qty: need }];
    const before = E.questProgress(qi).cur;
    G.dragon.paid = { iron_bar: need }; G.hand = [];
    check("feeding the bars keeps the quest complete", before === need && E.questProgress(qi).cur === need && E.questProgress(qi).done);
    G.dragon.stage = 3; G.dragon.paid = {};
    check("stage 3 reached -> done", E.questProgress(qi).done);
    check("iron quest text: no literal 4, uses the ⛓️ vein sprite", !/\b4\b/.test(q.desc) && q.desc.includes("⛓️") && !q.desc.includes("⛏"), q.desc);
    // the ⛓️ glyph is the iron vein's real sprite; jade veins named in SOURCES
    const mineSp = D.AREAS.mine.spawners || [];
    const iron = mineSp.find(sp => (sp.drops || []).some(d => d.item === "iron_ore"));
    const jade = mineSp.find(sp => (sp.drops || []).some(d => d.item === "jade_shard"));
    check("iron sources use the iron-vein sprite (no ⛏)", iron && D.SOURCES.iron_ore.includes(iron.sprite) && D.SOURCES.iron_bar.includes(iron.sprite) &&
      !D.SOURCES.iron_ore.includes("⛏") && !D.SOURCES.iron_bar.includes("⛏"), D.SOURCES.iron_ore + " | " + D.SOURCES.iron_bar);
    check("SOURCES.jade_shard names the Mine jade veins + quarry", jade && D.SOURCES.jade_shard === "Mine jade veins 🟢 · quarry rock rare drop" &&
      D.SOURCES.jade_shard.includes(jade.sprite), D.SOURCES.jade_shard);
  }

  // (k) pavilion seed is per RUN: ascension re-arms it ------------------------------
  {
    const s = boot(); initAll(s);
    s.GS.pavilionSeeded = true; s.GS.quest.idx = s.DATA.QUESTS.length;
    s.ENGINE.ascend([]);
    check("ascend() starts the new run with pavilionSeeded=false", s.GS.pavilionSeeded === false && s.GS.ascensions === 1);
    initAll(s);
    const E = s.ENGINE, G = s.GS;
    let spot = null;
    for (let r = 26; r < 66 && !spot; r++) for (let c = 26; c < 66 && !spot; c++)
      if (E.canPlaceBuilding("center", r, c, "meditation_pavilion")) spot = { r, c };
    const b = E.placeBuilding("center", "meditation_pavilion", spot.r, spot.c);
    G.hand = Object.entries(s.DATA.BUILDINGS.meditation_pavilion.cost).map(([item, qty]) => ({ item, qty }));
    for (let k = 0; k < 60 && !b.built; k++) E.dropFromHand("center", (b.col + 1.5) * 32, (b.row + 1.5) * 32);
    check("run 2's first pavilion is seeded again (foodCap buns)", b.built && b.buns === s.DATA.BUILDINGS.meditation_pavilion.roster.foodCap, `buns=${b.buns}`);
  }

  // (l) builtTypes: starter buildings never count as player-built ------------------
  {
    const s = boot(); initAll(s); s.ENGINE.setupStarterNetwork();
    const starters = s.GS.areas.center.buildings.filter(b => b.starter);
    const placedTypes = new Set(starters.map(b => b.type));
    check("setupStarterNetwork tags every building it places (b.starter)", starters.length >= 12 &&
      s.GS.areas.center.buildings.filter(b => !b.starter).every(b => b.type === "center" || b.type === "dragon"), `${starters.length} tagged`);
    check("starter network doesn't touch builtTypes", JSON.stringify(s.GS.builtTypes) === "{}");
    s.SAVE.saveState();
    const s2 = boot(JSON.parse(s._stored()));
    check("starter tag survives save/load", s2.GS.areas.center.buildings.filter(b => b.starter).length === starters.length);
    // an OLD (v51-shaped) save: starter buildings carry no tag, no builtTypes
    const raw = JSON.parse(s._stored());
    for (const b of raw.areas.center.buildings) delete b.starter;
    delete raw.builtTypes; delete raw.buildSeen; delete raw.pavilionSeeded; delete raw.quest.chain;
    raw.quest.idx = 3; raw.dragonBlessed = false; raw.stats.buildingsBuilt = 0;
    const s3 = boot(JSON.parse(JSON.stringify(raw)));
    check("old early save: builtTypes excludes the starter network", JSON.stringify(s3.GS.builtTypes) === "{}", JSON.stringify(s3.GS.builtTypes));
    check("old early save keeps the progressive reveal (no Kiln/Workbench card)", !s3.ENGINE.isBuildingUnlocked("kiln") && !s3.ENGINE.isBuildingUnlocked("workbench") &&
      !s3.ENGINE.isBuildingUnlocked("gathering_stone"));
    check("old save: starter tag inferred from the known layout", s3.GS.areas.center.buildings.filter(b => b.starter).length === starters.length,
      s3.GS.areas.center.buildings.filter(b => !b.starter).map(b => b.type + "@" + b.row + "," + b.col).join(" "));
    // player-built buildings still count (even with starter spots inferred)
    const raw2 = JSON.parse(JSON.stringify(raw));
    raw2.stats.buildingsBuilt = 2;
    raw2.areas.center.buildings.push({ id: 900, type: "loom", row: 30, col: 60, paid: {}, built: true });
    raw2.areas.center.buildings.push({ id: 901, type: "kiln", row: 36, col: 60, paid: {}, built: false });   // a ghost
    raw2.areas.center.buildings.push({ id: 902, type: "workbench", row: 42, col: 60, paid: {}, built: true });
    const s4 = boot(raw2);
    check("old save: player-built types count, starters + ghosts don't", s4.GS.builtTypes.loom === true && s4.GS.builtTypes.workbench === true &&
      !s4.GS.builtTypes.kiln && !s4.GS.builtTypes.gathering_stone && !s4.GS.builtTypes.wisp_lantern, JSON.stringify(s4.GS.builtTypes));
    // a legacy v50 layout (75-grid spots) is recognised too
    const raw3 = v50Save(2, { starterPlaced: true, stats: { buildingsBuilt: 1 }, areas: { center: { buildings: [
      { id: 3, type: "gathering_stone", row: 62, col: 18, paid: {}, built: true },
      { id: 4, type: "wisp_lantern", row: 44, col: 52, paid: {}, built: true },
      { id: 9, type: "storehouse", row: 40, col: 40, paid: {}, built: true }] } } });
    const s5 = boot(raw3);
    check("pre-v46 starter spots recognised; the player's storehouse counts", s5.GS.builtTypes.storehouse === true &&
      !s5.GS.builtTypes.gathering_stone && !s5.GS.builtTypes.wisp_lantern, JSON.stringify(s5.GS.builtTypes));
  }

  // (m) link editor refuses links that can never carry anything --------------------
  {
    const s = boot(); initAll(s); s.ENGINE.setupStarterNetwork();
    const E = s.ENGINE, G = s.GS, A = G.areas.center.buildings;
    G.quest.idx = s.DATA.QUESTS.length;   // every card revealed
    const kiln = A.find(b => b.type === "kiln"), bench = A.find(b => b.type === "workbench");
    const lan = A.find(b => b.type === "wisp_lantern");
    const gs = A.find(b => b.type === "gathering_stone");
    const free = (type) => { for (let r = 26; r < 70; r++) for (let c = 26; c < 70; c++) if (E.canPlaceBuilding("center", r, c, type)) return { r, c }; return null; };
    const put = (type, extra) => { const sp = free(type); const b = E.placeBuilding("center", type, sp.r, sp.c); Object.assign(b, { built: true }, extra || {}); return b; };
    const plankSh = put("storehouse", { item: "plank", qty: 5 });
    const emptySh = put("storehouse", { item: null, qty: 0 });
    const woodSeal = A.find(b => b.type === "warding_seal" && b.item === "wood");
    const n0 = G.stats.linksAdded || 0, l0 = lan.links.length;
    const why = E.linkRefusal("center", plankSh.id, kiln.id);
    check("plank storehouse -> Kiln refused: 'Kiln can't use Plank'", why && why.code === "types" && why.text === "Kiln can't use Plank", JSON.stringify(why));
    check("addLink refuses it, link quest not credited", E.addLink("center", lan.id, plankSh.id, kiln.id) === false &&
      (G.stats.linksAdded || 0) === n0 && lan.links.length === l0);
    check("plank storehouse -> Workbench allowed (Tools uses Plank, any recipe counts)", E.linkRefusal("center", plankSh.id, bench.id) === null);
    check("wood seal -> Kiln allowed (wood is fuel)", E.linkRefusal("center", woodSeal.id, kiln.id) === null);
    check("Gathering Stone (unknown content) -> Kiln allowed", E.linkRefusal("center", gs.id, kiln.id) === null);
    check("empty storehouse -> Kiln allowed", E.linkRefusal("center", emptySh.id, kiln.id) === null);
    check("plank storehouse -> empty storehouse allowed", E.linkRefusal("center", plankSh.id, emptySh.id) === null);
    check("self-link refused", E.linkRefusal("center", plankSh.id, plankSh.id) !== null);
    check("valid addLink still credits the quest", E.addLink("center", lan.id, plankSh.id, bench.id) === true && G.stats.linksAdded === n0 + 1);
  }

  // (n) placeReason mirrors canPlaceBuilding on every cell ------------------------
  {
    const s = boot(); initAll(s); s.ENGINE.setupStarterNetwork();
    const E = s.ENGINE, D = s.DATA, N = D.GRID.cells;
    // a ghost burner so rack cells exist too
    const fsp = (() => { for (let r = 30; r < 60; r++) for (let c = 30; c < 60; c++) if (E.canPlaceBuilding("center", r, c, "kiln")) return { r, c }; })();
    E.placeBuilding("center", "kiln", fsp.r, fsp.c);
    const mism = [], seen = new Set();
    for (const [area, types] of [["center", ["storehouse", "kiln", "gathering_stone", "algae_farm", "meditation_pavilion"]],
                                 ["fishing", ["algae_farm", "storehouse"]], ["mine", ["forge"]]])
      for (const t of types)
        for (let r = -1; r < N + 1; r += 1) for (let c = -1; c < N + 1; c += 1) {
          const ok = E.canPlaceBuilding(area, r, c, t), why = E.placeReason(area, r, c, t);
          if (ok !== (why === null)) mism.push(`${area}:${t}@${r},${c} ok=${ok} why=${why}`);
          if (why) seen.add(why);
        }
    check("placeReason === null  <=>  canPlaceBuilding (every cell, 8 area/type pairs)", mism.length === 0, mism.slice(0, 3).join("; ") || "ok");
    const want = ["Wild land — build in the clearing", "Blocked", "Fuel rack blocked", "Water only"];
    check("reasons cover wild land / blocked / fuel rack / water", want.every(w => seen.has(w)), [...seen].join(" | "));
    check("algae farm on land: 'Water only'", E.placeReason("center", 40, 40, "algae_farm") === "Water only");
    const nb = D.ZONES[[].concat(D.AREAS.center.noBuild)[0]][0];
    check("a wild-land cell reads 'Wild land — build in the clearing'", E.placeReason("center", nb.r0, nb.c0 + 3, "storehouse") === "Wild land — build in the clearing",
      E.placeReason("center", nb.r0, nb.c0 + 3, "storehouse"));
  }
} catch (e) {
  console.log("FAIL exception — " + (e && e.stack || e));
  fails++;
}

console.log(fails ? `\n${fails} FAILED` : "\nALL PASSED");
process.exit(fails ? 1 : 0);
