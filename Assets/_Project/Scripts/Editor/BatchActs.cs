using System;
using System.Globalization;
using IdleGrounds.Game;
using IdleGrounds.Sim;
using UnityEngine;

namespace IdleGrounds.Editor
{
    /// <summary>
    /// Scripted UI / state setup for the visual tour (<c>shots[].act</c> in a shot.sh spec): each entry is
    /// <c>name</c> or <c>name:args</c>, run in order before the shot is placed. Every shot starts with an implicit
    /// <c>closeall</c> (panels, placement/demolish, cursor released) unless its first act is <c>keep</c>.
    /// Commands (island = key like center/farm; cells are Island-local row,col):
    ///   welcome | welcomeClose | closeall | keep
    ///   build | demolish | place:TYPE@island,row,col | ghost:TYPE@island,row,col | cursor:island,row,col | cursorW:x,y | hover:island,TYPE | hoverNode:island,row,col (nearest interactable node)
    ///   help | stats | statsFlow | statsLifetime | tree | confirm | confirmReset | toast:TEXT | toasts:A|B|C | toastKind:Warning|TEXT | ending | dragonDialog:TEXT
    ///   questCollapse | questExpand
    ///   menu | settings (Menu on its Settings page) | font:OPTION_ID (Text font setting, applied live; kept in PlayerPrefs)
    ///   recipe:island,TYPE | link:island | bridge:island | pavilion:island   (first BUILT building of that type)
    ///   give:ITEM,N (hand) | clearHand
    ///   flowerSpot:TYPE@island (log free spots whose footprint covers a flower decor tile) | demolishAt:island,row,col | drop:ITEM,N@island,row,col
    /// </summary>
    public static partial class Batch
    {
        static void RunActs(Shot shot)
        {
            var acts = shot.act ?? new string[0];
            if (!(acts.Length > 0 && acts[0] == "keep")) CloseAll();
            foreach (var a in acts) { try { Act(a); } catch (Exception e) { Debug.LogError(Tag + "act '" + a + "' failed: " + e); } }
            var m = MetaUiController.Instance;
            Debug.Log(Tag + "acts [" + string.Join(" | ", acts) + "] -> welcomeOpen=" + (m != null && m.Welcome.IsOpen) + " helpOpen=" + (m != null && m.Help.IsOpen) + " questHidden=" + GameRunner.Instance.State.quest.hidden);
        }

        static void CloseAll()
        {
            if (MobileLayer.Instance != null) MobileLayer.Instance.ClosePause();
            else if (GameMenuView.Instance != null) GameMenuView.Instance.Close();
            var meta = MetaUiController.Instance;
            var build = UnityEngine.Object.FindFirstObjectByType<BuildController>();
            if (meta != null)
            {
                if (meta.Confirm != null && meta.Confirm.IsOpen) meta.Confirm.Close();
                if (meta.Stats != null && meta.Stats.IsOpen) meta.Stats.Close();
                if (meta.Help != null && meta.Help.IsOpen) meta.Help.Close();
                if (meta.Welcome != null && meta.Welcome.IsOpen) meta.Welcome.Close();
            }
            if (build != null)
            {
                build.CloseBuildMenu(); build.CloseBuildingPanels(); build.CancelModes();
                if (build.UpgradeTreeOpen) build.UpgradeTree.Close();
            }
            var gr = GameRunner.Instance;
            if (gr != null && gr.State != null) { gr.State.dragon.dialog = null; if (gr.State.ascended) gr.State.endingSeen = true; }   // story/ending cards are scripted per shot
            var hand = UnityEngine.Object.FindFirstObjectByType<HandController>();
            if (hand != null) { hand.CancelHolds(); hand.useDebugCursor = false; }
        }

        /// <summary>The quest panel rebuilds on a 0.2 s wall-clock timer, which a stepped capture never reaches: force it.</summary>
        static void RefreshQuestPanel()
        {
            var q = UnityEngine.Object.FindFirstObjectByType<QuestPanelView>();
            if (q != null) q.Rebuild(true);
        }

        static float F(string s) => float.Parse(s.Trim(), CultureInfo.InvariantCulture);

        static Vector2 CellWorld(string island, float row, float col)
        {
            var isl = FindIsland(island);
            if (isl == null) throw new Exception("no island " + island);
            return (Vector2)isl.CellToWorld(Mathf.FloorToInt(row), Mathf.FloorToInt(col)) + new Vector2(0.5f, -0.5f);
        }

        static Building FirstBuilt(string island, string type)
        {
            var area = GameRunner.Instance.State.Area(island);
            if (area == null) throw new Exception("no area " + island);
            foreach (var b in area.buildings) if (b.built && b.type == type) return b;
            throw new Exception("no built " + type + " on " + island);
        }

        static void Act(string a)
        {
            if (string.IsNullOrEmpty(a)) return;
            int i = a.IndexOf(':');
            string name = i < 0 ? a : a.Substring(0, i), arg = i < 0 ? "" : a.Substring(i + 1);
            var runner = GameRunner.Instance;
            var meta = MetaUiController.Instance;
            var build = UnityEngine.Object.FindFirstObjectByType<BuildController>();
            var hand = UnityEngine.Object.FindFirstObjectByType<HandController>();
            switch (name)
            {
                case "keep": break;
                case "closeall": CloseAll(); break;
                case "welcome": meta.Welcome.ShowIntro(); break;
                case "welcomeClose": if (meta.Welcome.IsOpen) meta.Welcome.Continue(); break;
                case "build": build.OpenBuildMenu(); break;
                case "demolish": build.ToggleDemolish(); break;
                case "help": meta.Help.Open(); break;
                case "pause": MobileLayer.Instance.OpenPause(); break;
                case "menu": { var gm = GameMenuView.Ensure(); if (gm != null && !gm.IsOpen) gm.Open(); break; }
                case "settings": { var gm = GameMenuView.Ensure(); if (gm != null) gm.OpenSettings(); break; }
                case "font": UiFont.SetChoice(arg.Trim()); break;     // Text font option id (TextFonts.Options): pixelify, pixelify_jersey, jersey10, vt323, pressstart2p, smooth
                case "pauseClose": MobileLayer.Instance.ClosePause(); break;
                case "stats": meta.Stats.Show(false, false); meta.Stats.Open(); break;
                case "statsFlow": meta.Stats.Show(true, false); meta.Stats.Open(); break;
                case "statsLifetime": meta.Stats.Show(false, true); meta.Stats.Open(); break;
                case "tree": build.OpenUpgradeTree(); break;
                case "confirm": meta.Confirm.Ask(MetaUiController.AscendTitle, MetaUiController.AscendText, "Ascend to Immortality", null); break;
                case "confirmReset": meta.Confirm.Ask(MetaBarView.NewGameTitle, MetaBarView.ResetText, MetaBarView.NewGameButton, null); break;
                case "toast": meta.Toast.Show(arg); break;
                case "ending": runner.State.ascended = true; runner.State.endingSeen = false; break;
                case "dragonDialog": runner.State.dragon.dialog = arg; break;
                case "questCollapse": runner.Sim.SetQuestPanelHidden(true); RefreshQuestPanel(); break;
                case "questExpand": runner.Sim.SetQuestPanelHidden(false); RefreshQuestPanel(); break;
                case "toasts": foreach (var t in arg.Split('|')) meta.Toast.Show(t, ToastKind.Info); break;
                case "toastKind": { var p = arg.Split('|'); meta.Toast.Show(p[1], (ToastKind)System.Enum.Parse(typeof(ToastKind), p[0])); break; }
                case "clearHand": runner.State.hand.Clear(); break;
                case "give":
                {
                    var p = arg.Split(',');
                    runner.State.hand.Add(new HandStack(p[0].Trim(), int.Parse(p[1])));
                    break;
                }
                case "cursor":
                {
                    var p = arg.Split(',');
                    hand.DebugPointAt(CellWorld(p[0].Trim(), F(p[1]), F(p[2])));
                    break;
                }
                case "cursorW": { var p = arg.Split(','); hand.DebugPointAt(new Vector2(F(p[0]), F(p[1]))); break; }
                case "place":
                {
                    int at = arg.IndexOf('@');
                    string type = arg.Substring(0, at);
                    var p = arg.Substring(at + 1).Split(',');
                    build.BeginPlacing(type);
                    hand.DebugPointAt(CellWorld(p[0].Trim(), F(p[1]), F(p[2])));
                    break;
                }
                case "ghost":   // unbuilt ghost placed straight through the sim (ghost:TYPE@island,row,col)
                {
                    int at = arg.IndexOf('@');
                    var p = arg.Substring(at + 1).Split(',');
                    var gb = runner.Sim.PlaceGhost(p[0].Trim(), arg.Substring(0, at), int.Parse(p[1]), int.Parse(p[2]));
                    if (gb == null) Debug.LogError(Tag + "ghost refused: " + arg);
                    break;
                }
                case "built":   // finished building placed straight through the sim near a cell (built:TYPE@island,row,col; art reviews)
                {
                    int at = arg.IndexOf('@');
                    var p = arg.Substring(at + 1).Split(',');
                    var bb = runner.Sim.Buildings.PlaceBuilt(p[0].Trim(), arg.Substring(0, at), int.Parse(p[1]), int.Parse(p[2]));
                    if (bb == null) Debug.LogError(Tag + "built refused (no free spot): " + arg);
                    else
                    {
                        Debug.Log(Tag + $"built {bb.type} at {p[0].Trim()} r{bb.row} c{bb.col}");
                        var bd = runner.Config.Building(bb.type);
                        lastBuiltCentre = CellWorld(p[0].Trim(), bb.row + bd.sizeH * 0.5f, bb.col + bd.sizeW * 0.5f);   // shot "focus": "built"
                    }
                    break;
                }
                case "working":   // every built TYPE on an island shows its working loop (working:TYPE@island; art reviews): a batch that never finishes
                {
                    int at = arg.IndexOf('@');
                    var area = runner.State.Area(arg.Substring(at + 1).Trim());
                    int n = 0;
                    if (area != null) foreach (var b in area.buildings) if (b.built && b.type == arg.Substring(0, at).Trim()) { b.smeltDoneAt = runner.SimNow + 1e12; n++; }
                    if (n == 0) Debug.LogError(Tag + "working: no built " + arg);
                    break;
                }
                case "focusNode":   // shot focus "built" = the n-th (default 0) node / fixture of a kind (focusNode:island,kind[,n]; art reviews)
                {
                    var p = arg.Split(',');
                    var ar = runner.State.Area(p[0].Trim());
                    string kind = p[1].Trim(); int want = p.Length > 2 ? int.Parse(p[2].Trim()) : 0, seen = 0;
                    IdleGrounds.Sim.Node fn = null;
                    if (ar != null) foreach (var n in ar.nodes) if (!n.deco && (n.kind == kind || n.spawnerKind == kind) && seen++ == want) { fn = n; break; }
                    if (fn == null) { Debug.LogError(Tag + "focusNode: no " + arg); break; }
                    lastBuiltCentre = CellWorld(p[0].Trim(), fn.row + fn.size * 0.5f, fn.col + fn.size * 0.5f);
                    Debug.Log(Tag + $"focusNode {kind}#{want} at {p[0].Trim()} r{fn.row} c{fn.col} size {fn.size}");
                    break;
                }
                case "hitNodes":   // every chop / break node on an island shows its hit state (frame 1) without being used up (hitNodes:island; art reviews)
                {
                    var ar = runner.State.Area(arg.Trim());
                    int k = 0;
                    if (ar != null) foreach (var n in ar.nodes) if (!n.deco && !n.isFixed && n.interaction != IdleGrounds.Sim.NodeInteraction.Surface) { n.clicks = System.Math.Max(1, n.clicks); k++; }
                    Debug.Log(Tag + $"hitNodes {arg.Trim()}: {k}");
                    break;
                }
                case "enemy":   // review only: a creature at a cell (enemy:island,row,col[,walkCols][,boar]); walkCols 0 = stands still,
                {               // +n / -n = walks n cells right / left (mirrored); shot focus "built" = the creature
                    var p = arg.Split(',');
                    string isl = p[0].Trim();
                    var ar = runner.State.Area(isl);
                    var ecfg = runner.Config.Region(isl)?.enemies;
                    if (ar == null || ecfg == null) { Debug.LogError(Tag + "enemy: no enemies on " + isl); break; }
                    bool boar = p.Length > 4 && p[4].Trim() == "boar";
                    float row = F(p[1]), col = F(p[2]), walk = p.Length > 3 ? F(p[3]) : 0f;
                    double cell = runner.Space.Cell, x = (col + 0.5) * cell, y = (row + 0.5) * cell;
                    int hp = boar && ecfg.baitSpawn != null && ecfg.baitSpawn.hp > 0 ? ecfg.baitSpawn.hp : ecfg.hp;
                    var en = new IdleGrounds.Sim.Enemy { id = ar.nextEnemyId++, x = x, y = y, hp = hp, maxHp = hp, tx = x + walk * cell, ty = y,
                        kind = boar ? "boss" : null, spd = walk == 0f ? 1e-6 : 0 };
                    ar.enemies.Add(en);
                    lastBuiltCentre = CellWorld(isl, row, col);
                    Debug.Log(Tag + $"enemy #{en.id} {(boar ? "boar" : "fox")} at {isl} r{row} c{col} walk {walk}");
                    break;
                }
                case "enemyHit":   // review only: every creature on an island flinches now (enemyHit:island): the hit strip plays
                {
                    var ar = runner.State.Area(arg.Trim());
                    if (ar != null) foreach (var en in ar.enemies) en.hitAt = runner.SimNow;
                    break;
                }
                case "enemyKill":   // review only: every creature on an island is killed through the sim (enemyKill:island): the die strip plays
                {
                    var ar = runner.State.Area(arg.Trim());
                    if (ar != null) foreach (var en in ar.enemies.ToArray()) runner.Sim.Combat.DamageEnemy(arg.Trim(), en, en.hp);
                    break;
                }
                case "unlockIsland":   // review only: mark an Island unlocked in the running game (unlockIsland:island), so its
                {                      // nodes animate like in play (a locked Island freezes; surfaced fish only bob / ripple when unlocked)
                    runner.State.world.SetUnlocked(arg.Trim(), true);
                    Debug.Log(Tag + "unlockIsland " + arg.Trim());
                    break;
                }
                case "surfaceNodes":   // every surfacing node (fish, algae) on an island stays surfaced (surfaceNodes:island; art reviews)
                {
                    var ar = runner.State.Area(arg.Trim());
                    int k = 0;
                    if (ar != null) foreach (var n in ar.nodes) if (n.interaction == IdleGrounds.Sim.NodeInteraction.Surface) { n.surfaceUntil = runner.SimNow + 1e9; k++; }
                    Debug.Log(Tag + $"surfaceNodes {arg.Trim()}: {k}");
                    break;
                }
                case "hoverNode":
                {
                    var p = arg.Split(',');
                    var ar = runner.State.Area(p[0].Trim());
                    float hr = F(p[1]), hc = F(p[2]); float best = float.MaxValue; IdleGrounds.Sim.Node bn = null;
                    foreach (var n in ar.nodes)
                    {
                        if (n.deco) continue;
                        float d = (n.row - hr) * (n.row - hr) + (n.col - hc) * (n.col - hc);
                        if (d < best) { best = d; bn = n; }
                    }
                    if (bn != null) hand.DebugPointAt(CellWorld(p[0].Trim(), bn.row + bn.size * 0.5f, bn.col + bn.size * 0.5f));
                    break;
                }
                case "hover":
                {
                    var p = arg.Split(',');
                    var b = FirstBuilt(p[0].Trim(), p[1].Trim());
                    var def = runner.Config.Building(b.type);
                    hand.DebugPointAt(CellWorld(p[0].Trim(), b.row + def.sizeH * 0.5f, b.col + def.sizeW * 0.5f));
                    break;
                }
                case "barTip":   // tooltip of a bottom-bar control as if hovered: barTip:StatsButton (names: Stats/Build/Demolish/Mute/Motion/Menu + Button)
                {
                    var bar = BottomBarView.Instance;
                    var tr = bar != null ? bar.transform.Find(arg.Trim()) : null;
                    var trig = tr != null ? tr.GetComponent<TooltipTrigger>() : null;
                    if (trig == null) { Debug.LogError(Tag + "no tooltip trigger on bar child " + arg); break; }
                    var rtb = (RectTransform)tr;
                    var wc = new Vector3[4]; rtb.GetWorldCorners(wc);
                    var cvs = tr.GetComponentInParent<Canvas>().rootCanvas; Vector2 sp = RectTransformUtility.WorldToScreenPoint(cvs.renderMode == RenderMode.ScreenSpaceOverlay ? null : cvs.worldCamera, (wc[0] + wc[2]) * 0.5f);
                    Tooltip.Show(trig, trig.provider != null ? trig.provider() : trig.text, null, sp);
                    break;
                }
                case "recipe": { var p = arg.Split(','); build.OpenRecipePicker(p[0].Trim(), FirstBuilt(p[0].Trim(), p[1].Trim())); break; }
                case "link": build.OpenLinkEditor(arg.Trim(), FirstBuilt(arg.Trim(), "wisp_lantern")); break;
                case "bridge": build.OpenBridgePanel(arg.Trim(), FirstBuilt(arg.Trim(), "spirit_bridge")); break;
                case "pavilion": build.OpenPavilion(arg.Trim(), FirstBuilt(arg.Trim(), "meditation_pavilion")); break;
                case "flowerSpot":   // log free spots for TYPE whose footprint covers a Flowers decor tile (flowerSpot:TYPE@island; decor-cover reviews)
                {
                    int at = arg.IndexOf('@');
                    string type = arg.Substring(0, at).Trim(), isl = arg.Substring(at + 1).Trim();
                    var island = runner.IslandObject(isl);
                    var fl = island != null ? island.transform.Find(DecorCover.DressingRoot + "/Flowers")?.GetComponent<UnityEngine.Tilemaps.Tilemap>() : null;
                    var def = runner.Config.Building(type);
                    if (fl == null || def == null) { Debug.LogError(Tag + "flowerSpot: no Flowers map / type " + arg); break; }
                    int found = 0;
                    var st = runner.State;
                    int qIdx = st.quest.idx, dStage = st.dragon.stage;
                    st.quest.idx = runner.Config.quests.Count; st.dragon.stage = 4;     // judge the spot with everything revealed (fresh runs have it locked)
                    foreach (var p in fl.cellBounds.allPositionsWithin)
                    {
                        if (fl.GetTile(p) == null || found >= 12) continue;
                        var ic = island.WorldToCell(fl.GetCellCenterWorld(p));
                        int r0 = ic.y - def.sizeH / 2, c0 = ic.x - def.sizeW / 2;
                        if (runner.Sim.PlaceReason(isl, type, r0, c0) != null) continue;
                        found++;
                        Debug.Log(Tag + $"flowerSpot {type}@{isl}: flower at r{ic.y} c{ic.x} -> place r{r0} c{c0}; shot x {c0 + def.sizeW * 0.5f - Island.Cells * 0.5f:0.#} y {Island.Cells * 0.5f - (r0 + def.sizeH * 0.5f):0.#}");
                    }
                    st.quest.idx = qIdx; st.dragon.stage = dStage;
                    if (found == 0) Debug.LogError(Tag + "flowerSpot: none for " + arg);
                    break;
                }
                case "drop":   // ground items straight through the sim at a cell centre (drop:ITEM,N@island,row,col), e.g. to fill a generator's output pile
                {
                    int at = arg.IndexOf('@');
                    var ip = arg.Substring(0, at).Split(',');
                    var p = arg.Substring(at + 1).Split(',');
                    int cell = runner.Sim.Ctx.Cell;
                    runner.Sim.Ground.DropGround(p[0].Trim(), ip[0].Trim(), int.Parse(ip[1]), (F(p[2]) + 0.5f) * cell, (F(p[1]) + 0.5f) * cell, IdleGrounds.Sim.GroundTag.Crafted);
                    break;
                }
                case "demolishAt":   // demolish the building covering a cell straight through the sim (demolishAt:island,row,col)
                {
                    var p = arg.Split(',');
                    string isl = p[0].Trim(); int r = int.Parse(p[1]), c = int.Parse(p[2]);
                    int id = -1;
                    foreach (var b in runner.State.Area(isl).buildings)
                    {
                        var d = runner.Config.Building(b.type);
                        if (d != null && r >= b.row && r < b.row + d.sizeH && c >= b.col && c < b.col + d.sizeW) { id = b.id; break; }
                    }
                    if (id < 0 || !runner.Sim.Demolish(isl, id)) Debug.LogError(Tag + "demolishAt: nothing demolished at " + arg);
                    break;
                }
                default: if (!CodeAnimAct(name, arg)) Debug.LogError(Tag + "unknown act '" + a + "'"); break;   // animTime / animPreview: BatchCodeAnimActs.cs
            }
        }
    }
}
