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
    ///   help | stats | statsFlow | statsLifetime | tree | confirm | toast:TEXT | ending | dragonDialog:TEXT
    ///   questCollapse | questExpand
    ///   recipe:island,TYPE | link:island | bridge:island | pavilion:island   (first BUILT building of that type)
    ///   give:ITEM,N (hand) | clearHand
    /// </summary>
    public static partial class Batch
    {
        static void RunActs(Shot shot)
        {
            var acts = shot.act ?? new string[0];
            if (!(acts.Length > 0 && acts[0] == "keep")) CloseAll();
            foreach (var a in acts) { try { Act(a); } catch (Exception e) { Debug.LogError(Tag + "act '" + a + "' failed: " + e.Message); } }
            var m = MetaUiController.Instance;
            Debug.Log(Tag + "acts [" + string.Join(" | ", acts) + "] -> welcomeOpen=" + (m != null && m.Welcome.IsOpen) + " helpOpen=" + (m != null && m.Help.IsOpen) + " questHidden=" + GameRunner.Instance.State.quest.hidden);
        }

        static void CloseAll()
        {
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
                case "stats": meta.Stats.Show(false, false); meta.Stats.Open(); break;
                case "statsFlow": meta.Stats.Show(true, false); meta.Stats.Open(); break;
                case "statsLifetime": meta.Stats.Show(false, true); meta.Stats.Open(); break;
                case "tree": build.OpenUpgradeTree(); break;
                case "confirm": meta.Confirm.Ask(MetaUiController.AscendText, "Ascend to Immortality", null); break;
                case "toast": meta.Toast.Show(arg); break;
                case "ending": runner.State.ascended = true; runner.State.endingSeen = false; break;
                case "dragonDialog": runner.State.dragon.dialog = arg; break;
                case "questCollapse": runner.Sim.SetQuestPanelHidden(true); break;
                case "questExpand": runner.Sim.SetQuestPanelHidden(false); break;
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
                case "recipe": { var p = arg.Split(','); build.OpenRecipePicker(p[0].Trim(), FirstBuilt(p[0].Trim(), p[1].Trim())); break; }
                case "link": build.OpenLinkEditor(arg.Trim(), FirstBuilt(arg.Trim(), "wisp_lantern")); break;
                case "bridge": build.OpenBridgePanel(arg.Trim(), FirstBuilt(arg.Trim(), "spirit_bridge")); break;
                case "pavilion": build.OpenPavilion(arg.Trim(), FirstBuilt(arg.Trim(), "meditation_pavilion")); break;
                default: Debug.LogError(Tag + "unknown act '" + a + "'"); break;
            }
        }
    }
}
