// Generates the shot specs of the visual tour. Usage: node tools/bg/tour/gen-specs.cjs <outDir>
// writes <outDir>/specs/*.json (saves are read from <outDir>/saves/*.json, made by tools/bg/tour.sh).
const fs = require("fs");
const T = process.argv[2];
if (!T) { console.error("usage: node gen-specs.cjs <outDir>"); process.exit(64); }
fs.mkdirSync(T + "/specs", { recursive: true });
const save = (n) => T + "/saves/" + n + ".json";
const base = (o) => Object.assign({ mode: "play", width: 1920, height: 1080, warmupFrames: 90, ui: false, hideVeils: false, save: "", timeoutSeconds: 900, shots: [] }, o);
const W = (specname, o, shots) => fs.writeFileSync(`${T}/specs/${specname}.json`, JSON.stringify(base({ ...o, shots }), null, 1));
const islands = ["center", "farm", "mine", "fishing", "volcano", "grove", "celestial"];
// ---------- world shots on the finished game (all islands unlocked, veils never shown)
{
  const shots = [];
  let n = 10;
  const nn = () => String(n++).padStart(2, "0");
  for (const I of islands) {
    shots.push({ name: `${nn()}_${I}_whole_zoom6`, island: I, zoom: 6 });
    shots.push({ name: `${nn()}_${I}_overview_fit`, island: I, fit: true });
    shots.push({ name: `${nn()}_${I}_field_zoom2`, island: I, zoom: 2, x: 0, y: 0 });
    shots.push({ name: `${nn()}_${I}_buildzone_zoom2`, island: I, zoom: 2, x: -26, y: 26 });
    shots.push({ name: `${nn()}_${I}_coastN_zoom2`, island: I, zoom: 2, x: 0, y: 54 });
    shots.push({ name: `${nn()}_${I}_coastS_zoom2`, island: I, zoom: 2, x: 0, y: -52 });
    shots.push({ name: `${nn()}_${I}_coastE_zoom2`, island: I, zoom: 2, x: 58, y: 0 });
    shots.push({ name: `${nn()}_${I}_coastW_zoom2`, island: I, zoom: 2, x: -60, y: 0 });
    shots.push({ name: `${nn()}_${I}_cornerNW_zoom2`, island: I, zoom: 2, x: -50, y: 50 });
    shots.push({ name: `${nn()}_${I}_cornerSE_zoom2`, island: I, zoom: 2, x: 50, y: -50 });
  }
  W("world_end", { save: save("end"), hideVeils: true }, shots);
}

// ---------- fresh run, HUD on
W("fresh_ui", { ui: true }, [
  { name: "f01_welcome", startView: true, act: ["keep"], frames: 20 },
  { name: "f02_firstminute", startView: true, act: ["welcomeClose"] },
  { name: "f04_hover_altar", island: "center", zoom: 2, x: 0, y: 0, act: ["welcomeClose", "hover:center,center"], frames: 10 },
  { name: "f05_buildmenu", island: "center", zoom: 2, x: 0, y: 0, act: ["welcomeClose", "build"] },
  { name: "f06_placing_valid", island: "center", zoom: 2, x: 0, y: 0, act: ["welcomeClose", "place:storehouse@center,38,52"], frames: 10 },
  { name: "f07_placing_blocked", island: "center", zoom: 2, x: 0, y: 0, act: ["welcomeClose", "place:kiln@center,44,44"], frames: 10 },
  { name: "f08_demolish_on", island: "center", zoom: 2, x: 0, y: -12, act: ["welcomeClose", "demolish", "hover:center,kiln"], frames: 10 },
  { name: "f09_help", startView: true, act: ["welcomeClose", "help"] },
  { name: "f10_stats", startView: true, act: ["welcomeClose", "stats"] },
  { name: "f11_stats_flow", startView: true, act: ["welcomeClose", "statsFlow"] },
  { name: "f12_quest_collapsed", startView: true, act: ["welcomeClose", "questCollapse"] },
  { name: "f13_zoom1_closest", island: "center", zoom: 1, x: 0, y: -8, act: ["welcomeClose", "questExpand"] },
  { name: "f14_zoom6_farthest", island: "center", zoom: 6, x: 0, y: 0, act: ["welcomeClose"] },
  { name: "f17_handchip_tree", island: "center", zoom: 2, x: -2.5, y: 28, act: ["welcomeClose", "give:wood,5", "cursor:center,16,44"], frames: 10 },
  { name: "f16_altar_tree_fresh", startView: true, act: ["welcomeClose", "tree"] },
  { name: "f15_toast", startView: true, act: ["welcomeClose", "toast:Not enough wood to build that."], frames: 10 },
]);

// ---------- fresh run, world only: locked islands, veils on / off
{
  const shots = [];
  const fits = ["farm", "mine", "fishing", "volcano", "grove", "celestial"];
  fits.forEach((I, k) => shots.push({ name: `v0${k + 1}_${I}_fit_veil_on`, island: I, fit: true, veils: "on" }));
  fits.forEach((I, k) => shots.push({ name: `v1${k + 1}_${I}_fit_veil_off`, island: I, fit: true, veils: "off" }));
  // as the game camera allows it (zoom limits + pan clamp to the unlocked Islands + sky margin)
  shots.push({ name: "v21_edgeW_zoom6", island: "center", zoom: 6, x: -200, y: 0, veils: "on" });
  shots.push({ name: "v22_edgeE_zoom6", island: "center", zoom: 6, x: 200, y: 0, veils: "on" });
  shots.push({ name: "v23_edgeS_zoom6", island: "center", zoom: 6, x: 0, y: -200, veils: "on" });
  shots.push({ name: "v24_edgeSW_zoom3", island: "center", zoom: 3, x: -200, y: -200, veils: "on" });
  shots.push({ name: "v25_edgeSE_zoom3", island: "center", zoom: 3, x: 200, y: -200, veils: "on" });
  shots.push({ name: "v26_edgeW_zoom2", island: "center", zoom: 2, x: -200, y: 0, veils: "on" });
  shots.push({ name: "v27_edgeS_zoom2", island: "center", zoom: 2, x: 0, y: -200, veils: "on" });
  shots.push({ name: "v28_edgeE_zoom2", island: "center", zoom: 2, x: 200, y: 0, veils: "on" });
  W("fresh_world", { hideVeils: false }, shots);
}

// ---------- mid game (t30), HUD on: every panel
const mid = { ui: true, save: save("t30") };
W("t30_ui", mid, [
  { name: "u01_hud_midgame", island: "center", zoom: 2, x: 0, y: -12, act: ["welcomeClose"] },
  { name: "u02_buildmenu", island: "center", zoom: 2, x: 0, y: -12, act: ["build"] },
  { name: "u03_stats_overview", island: "center", zoom: 2, x: 0, y: -12, act: ["stats"] },
  { name: "u04_stats_flow", island: "center", zoom: 2, x: 0, y: -12, act: ["statsFlow"] },
  { name: "u05_stats_lifetime", island: "center", zoom: 2, x: 0, y: -12, act: ["statsLifetime"] },
  { name: "u06_help", island: "center", zoom: 2, x: 0, y: -12, act: ["help"] },
  { name: "u07_altar_tree", island: "center", zoom: 2, x: 0, y: -12, act: ["tree"], frames: 10 },
  { name: "u08_quest_collapsed", island: "center", zoom: 2, x: 0, y: -12, act: ["questCollapse"] },
  { name: "u09_quest_expanded", island: "center", zoom: 2, x: 0, y: -12, act: ["questExpand"] },
  { name: "u10_tooltip_kiln", island: "center", zoom: 2, x: 0, y: -12, act: ["hover:center,kiln"], frames: 10 },
  { name: "u11_tooltip_forge", island: "center", zoom: 2, x: -12, y: 16, act: ["hover:center,forge"], frames: 10 },
  { name: "u12_recipe_kiln", island: "center", zoom: 2, x: 0, y: -12, act: ["recipe:center,kiln"], frames: 10 },
  { name: "u13_recipe_workbench", island: "center", zoom: 2, x: 0, y: -12, act: ["recipe:center,workbench"], frames: 10 },
  { name: "u14_link_editor", island: "center", zoom: 2, x: 0, y: -12, act: ["link:center"], frames: 10 },
  { name: "u15_bridge_panel", island: "center", zoom: 2, x: -12, y: 16, act: ["bridge:center"], frames: 10 },
  { name: "u16_pavilion", island: "center", zoom: 2, x: -12, y: 16, act: ["pavilion:center"], frames: 10 },
  { name: "u17_confirm_dialog", island: "center", zoom: 2, x: 0, y: -12, act: ["confirm"] },
  { name: "u20_demolish_hover", island: "center", zoom: 2, x: 0, y: -12, act: ["demolish", "hover:center,paper_mill"], frames: 10 },
  { name: "u21_place_ghost_valid", island: "center", zoom: 2, x: 0, y: -22, act: ["place:kiln@center,66,48"], frames: 10 },
  { name: "u22_place_ghost_blocked", island: "center", zoom: 2, x: 0, y: -12, act: ["place:kiln@center,55,48"], frames: 10 },
  { name: "u23_handchip_full", island: "center", zoom: 2, x: 0, y: -12, act: ["clearHand", "give:iron_ore,12", "give:wood,10", "give:stone,8", "hover:center,kiln"], frames: 10 },
  { name: "u24_placed_ghost_needs", island: "center", zoom: 2, x: 0, y: -18, act: ["ghost:kiln@center,66,48", "cursor:center,67,49"], frames: 10 },
  { name: "u25_buildmenu_zoom6", island: "center", zoom: 6, x: 0, y: -12, act: ["build"] },
  { name: "u18_dragon_dialog", island: "center", zoom: 2, x: -30, y: 30, act: ["dragonDialog:The dragon stirs. Its eye opens a sliver, and the whole valley holds its breath."], frames: 10 },
  { name: "u19_toast", island: "center", zoom: 2, x: 0, y: -12, act: ["toast:Spirit Bridge paired: Fishing to Center."], frames: 10 },
]);

// ---------- finished game, HUD on
W("end_ui", { ui: true, save: save("end") }, [
  { name: "e02_ascend_confirm", island: "celestial", zoom: 2, x: 0, y: -35, act: ["confirm"] },
  { name: "e03_gate_hover", island: "celestial", zoom: 1, x: 0, y: -35, act: ["hover:celestial,heavenly_gate"], frames: 10 },
  { name: "e04_altar_tree_endgame", island: "center", zoom: 2, x: 0, y: -12, act: ["tree"], frames: 10 },
  { name: "e05_stats_flow_endgame", island: "center", zoom: 2, x: 0, y: -12, act: ["statsFlow"] },
  { name: "e06_stats_overview_endgame", island: "center", zoom: 2, x: 0, y: -12, act: ["stats"] },
  { name: "e07_bottombar_demolish_endgame", island: "center", zoom: 2, x: 0, y: -12, act: ["demolish"] },
  { name: "e08_buildmenu_endgame", island: "center", zoom: 2, x: 0, y: -12, act: ["build"] },
  { name: "e09_tooltip_dragon", island: "center", zoom: 2, x: -30, y: 30, act: ["hover:center,dragon"], frames: 10 },
  { name: "e10_link_editor_endgame", island: "center", zoom: 2, x: 0, y: -12, act: ["link:center"], frames: 10 },
  { name: "e11_recipe_forge", island: "center", zoom: 2, x: -12, y: 16, act: ["recipe:center,forge"], frames: 10 },
  { name: "e01_ending_card", island: "center", zoom: 2, x: 0, y: -12, act: ["ending"], frames: 20 },
]);

// ---------- finished game, world only: building clusters, ghosts, sky
W("end_world2", { save: save("end"), hideVeils: true }, [
  { name: "b01_cluster_south_zoom1", island: "center", zoom: 1, x: 0, y: -12, frames: 60 },
  { name: "b02_cluster_south_zoom2", island: "center", zoom: 2, x: 0, y: -12, frames: 60 },
  { name: "b03_cluster_north_zoom1", island: "center", zoom: 1, x: -12, y: 16, frames: 60 },
  { name: "b04_cluster_north_zoom2", island: "center", zoom: 2, x: -12, y: 16, frames: 60 },
  { name: "b05_cluster_wide_zoom3", island: "center", zoom: 3, x: -6, y: 2, frames: 30 },
  { name: "b06_altar_zoom1", island: "center", zoom: 1, x: 0, y: 0, frames: 30 },
  { name: "b07_dragon_zoom1", island: "center", zoom: 1, x: -33, y: 34, frames: 30 },
  { name: "b08_dragon_zoom2", island: "center", zoom: 2, x: -33, y: 34, frames: 30 },
  { name: "b09_storehouse_lines_zoom2", island: "center", zoom: 2, x: -22, y: -30, frames: 30 },
  { name: "b10_gate_celestial_zoom1", island: "celestial", zoom: 1, x: 0, y: -35, frames: 30 },
  { name: "b11_gate_celestial_zoom2", island: "celestial", zoom: 2, x: 0, y: -35, frames: 30 },
  { name: "b12_fishing_bridge_zoom2", island: "fishing", zoom: 2, x: -26, y: 26, frames: 30 },
  { name: "b13_hover_highlight_kiln", island: "center", zoom: 1, x: 0, y: -12, act: ["hover:center,kiln"], frames: 30 },
  { name: "b14_ghost_valid_kiln", island: "center", zoom: 1, x: 0, y: -26, act: ["place:kiln@center,66,48"], frames: 30 },
  { name: "b15_ghost_blocked_kiln", island: "center", zoom: 1, x: 0, y: -12, act: ["place:kiln@center,55,48"], frames: 30 },
  { name: "b16_ghost_placed_unbuilt", island: "center", zoom: 1, x: 0, y: -26, act: ["ghost:kiln@center,66,48"], frames: 30 },
  { name: "s01_sky_gap_center_farm_zoom2", zoom: 2, x: 86, y: -55 },
  { name: "s02_sky_gap_center_farm_zoom6", zoom: 6, x: 86, y: -55 },
  { name: "s03_sky_gap_center_fishing_zoom6", zoom: 6, x: 171, y: -115 },
  { name: "s04_sky_between_four_islands_zoom6", zoom: 6, x: 232, y: -110 },
  { name: "s05_sky_below_celestial_zoom6", zoom: 6, x: 173, y: -360 },
  { name: "s06_sky_above_center_zoom6", zoom: 6, x: 170, y: 40 },
  { name: "s07_sky_gap_center_mine_zoom2", zoom: 2, x: 232, y: -55 },
  { name: "s08_whole_world_beyond_limit", size: 260, x: 173, y: -175 },
]);
console.log("specs written");
