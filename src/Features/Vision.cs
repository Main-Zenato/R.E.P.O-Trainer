using System.Collections.Generic;
using HarmonyLib;
using RepoTrainer.UI;
using UnityEngine;

namespace RepoTrainer.Features
{
    /// <summary>See-through-walls markers, full brightness and fog removal. All local, works anywhere.</summary>
    internal static class Vision
    {
        public static bool Enemies, Valuables, Items, Players, Extractions, DeadHeads;
        public static bool Fullbright, NoFog;
        public static float MaxDistance = 120f, MinValue = 0f, Brightness = 0.55f;

        private static bool _fogSaved, _fogOriginal;

        public static bool AnyEsp => Enemies || Valuables || Items || Players || Extractions || DeadHeads;

        public static void Reset()
        {
            Enemies = Valuables = Items = Players = Extractions = DeadHeads = false;
            Fullbright = NoFog = false;
            RestoreFog();
        }

        // ---- Lighting ----------------------------------------------------------------------

        [HarmonyPatch(typeof(EnvironmentDirector), "Update")]
        private static class EnvironmentPatch
        {
            private static void Postfix(EnvironmentDirector __instance)
            {
                if (Fullbright) RenderSettings.ambientLight = new Color(Brightness, Brightness, Brightness);
                if (NoFog)
                {
                    if (!_fogSaved) { _fogOriginal = RenderSettings.fog; _fogSaved = true; }
                    RenderSettings.fog = false;
                    var cam = __instance.MainCamera ? __instance.MainCamera : Game.Camera;
                    if (cam && (!SpectateCamera.instance || !SpectateCamera.instance.CheckState(SpectateCamera.State.Death)))
                        cam.farClipPlane = 600f;
                }
                else RestoreFog();
            }
        }

        private static void RestoreFog()
        {
            if (!_fogSaved) return;
            _fogSaved = false;
            RenderSettings.fog = _fogOriginal;
            // EnvironmentDirector puts the far clip plane back on its next frame.
        }

        // ---- ESP ---------------------------------------------------------------------------

        public static void DrawEsp(float sw, float sh)
        {
            if (!AnyEsp || !Game.InGame || Event.current.type != EventType.Repaint) return;
            var cam = Game.Camera;
            if (!cam) return;
            var me = Game.Me ? Game.Me.transform.position : cam.transform.position;
            float maxSq = MaxDistance * MaxDistance;

            if (Extractions)
                foreach (var ep in Scan.Extractions)
                    if (ep) Mark(cam, sw, sh, me, maxSq * 4f, ep.transform.position + Vector3.up, Theme.Haul,
                        L.T("Extraction", "Extraction"), null, 0, false);

            if (Valuables)
            {
                int drawn = 0;
                foreach (var v in Scan.Valuables)
                {
                    if (!v || v.dollarValueCurrent < MinValue) continue;
                    if (Mark(cam, sw, sh, me, maxSq, v.transform.position, Theme.Amber, Game.Dollars(v.dollarValueCurrent), null, 0, false) && ++drawn >= 120) break;
                }
            }

            if (Items)
                foreach (var it in Scan.Items)
                    if (it && !it.shopItem) Mark(cam, sw, sh, me, maxSq, it.transform.position, Theme.Signal, Scan.ItemName(it), null, 0, false);

            if (DeadHeads)
                foreach (var h in Scan.DeadHeads)
                    if (h && h.playerAvatar) Mark(cam, sw, sh, me, maxSq * 4f, h.transform.position, Theme.Alarm,
                        h.playerAvatar.playerName + L.T(" (tête)", " (head)"), null, 0, false);

            if (Players && GameDirector.instance)
                foreach (var p in GameDirector.instance.PlayerList)
                {
                    if (!p || p.isLocal || p.deadSet || p.isDisabled) continue;
                    var hp = p.playerHealth;
                    float frac = hp && hp.maxHealth > 0 ? hp.health / (float)hp.maxHealth : 0f;
                    Mark(cam, sw, sh, me, maxSq * 4f, p.transform.position + Vector3.up * 1.1f, Theme.Text, p.playerName, null, frac, true);
                }

            if (Enemies)
                foreach (var e in Scan.Enemies)
                {
                    if (!e) continue;
                    float frac = 0f;
                    if (e.Enemy && e.Enemy.HasHealth && e.Enemy.Health.health > 0)
                        frac = e.Enemy.Health.healthCurrent / (float)e.Enemy.Health.health;
                    Mark(cam, sw, sh, me, maxSq * 4f, Scan.EnemyPos(e), Theme.Alarm, Scan.EnemyName(e), null, frac, true, true);
                }
        }

        /// <summary>Draws one marker. Returns false when it was culled (behind, off screen or too far).</summary>
        private static bool Mark(Camera cam, float sw, float sh, Vector3 me, float maxSq, Vector3 world, Color color,
            string title, string extra, float health, bool showHealth, bool box = false)
        {
            float distSq = (world - me).sqrMagnitude;
            if (distSq > maxSq) return false;
            // The game renders through a low-res texture, so project to viewport, not screen pixels.
            var vp = cam.WorldToViewportPoint(world);
            if (vp.z <= 0.05f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f) return false;
            float x = vp.x * sw, y = (1f - vp.y) * sh;
            float dist = Mathf.Sqrt(distSq);

            if (box)
            {
                float s = Mathf.Clamp(520f / Mathf.Max(dist, 1f), 16f, 140f);
                Brackets(new Rect(x - s / 2f, y - s / 2f, s, s), color);
                y -= s / 2f + 4f;
            }
            else
            {
                Theme.Fill(new Rect(x - 4, y - 4, 8, 8), Theme.Glass.WithAlpha(0.8f), 2);
                Theme.Fill(new Rect(x - 3, y - 3, 6, 6), color, 2);
                y -= 8f;
            }

            string text = title + "  " + Game.Meters(dist);
            var size = Theme.Measure(text, 12, true);
            var pill = new Rect(x - size.x / 2f - 7, y - 20, size.x + 14, 18);
            Theme.Fill(pill, Theme.Glass.WithAlpha(0.78f), 4);
            Theme.Label(pill, text, 12, color, true, TextAnchor.MiddleCenter);
            if (showHealth && health > 0f)
                Theme.Segments(new Rect(pill.x + 4, pill.yMax + 2, pill.width - 8, 3), health, 10, Theme.Haul, Theme.Glass.WithAlpha(0.8f), 1f);
            return true;
        }

        private static void Brackets(Rect r, Color c)
        {
            BracketPass(r, Theme.Glass.WithAlpha(0.6f), 1f); // drop shadow keeps it readable on bright walls
            BracketPass(r, c, 0f);
        }

        private static void BracketPass(Rect r, Color col, float o)
        {
            float l = Mathf.Max(5f, r.width * 0.25f);
            const float t = 2f;
            {
                Theme.Fill(new Rect(r.x + o, r.y + o, l, t), col);
                Theme.Fill(new Rect(r.x + o, r.y + o, t, l), col);
                Theme.Fill(new Rect(r.xMax - l + o, r.y + o, l, t), col);
                Theme.Fill(new Rect(r.xMax - t + o, r.y + o, t, l), col);
                Theme.Fill(new Rect(r.x + o, r.yMax - t + o, l, t), col);
                Theme.Fill(new Rect(r.x + o, r.yMax - l + o, t, l), col);
                Theme.Fill(new Rect(r.xMax - l + o, r.yMax - t + o, l, t), col);
                Theme.Fill(new Rect(r.xMax - t + o, r.yMax - l + o, t, l), col);
            }
        }

        // ---- Map ---------------------------------------------------------------------------

        /// <summary>Marks every room as visited on your map tool. Local only.</summary>
        public static int RevealMap()
        {
            int n = 0;
            foreach (var room in Object.FindObjectsByType<RoomVolume>(FindObjectsSortMode.None))
                if (room && room.SetExplored()) n++;
            return n;
        }

        /// <summary>
        /// Puts every valuable on your map, the way finding one does, but without telling the
        /// others: this runs the discovery's local half only.
        /// </summary>
        public static int MapValuables()
        {
            if (!Map.Instance) return 0;
            int n = 0;
            foreach (var v in Scan.Valuables)
                if (v && !v.discovered) { v.DiscoverRPC(); n++; }
            return n;
        }

        public static void CollectActive(List<string> list)
        {
            if (AnyEsp) list.Add(L.T("vision à travers les murs", "see through walls"));
            if (Fullbright) list.Add(L.T("pleine lumière", "fullbright"));
            if (NoFog) list.Add(L.T("sans brouillard", "no fog"));
        }

        // ---- Tab ---------------------------------------------------------------------------

        public static void Draw(Ui ui)
        {
            ui.Section(L.T("À travers les murs", "Through walls"));
            Enemies = ui.Toggle("vis.enemies", L.T("Ennemis", "Enemies"),
                L.T("Cadre rouge, nom, distance et santé restante.", "Red frame, name, distance and remaining health."), Enemies);
            Valuables = ui.Toggle("vis.valuables", L.T("Objets de valeur", "Valuables"),
                L.T("Leur prix actuel, abîmés compris.", "Their current price, damage included."), Valuables);
            MinValue = ui.Slider("vis.minvalue", L.T("Valeur minimale affichée", "Lowest value shown"), MinValue, 0f, 20000f,
                v => v <= 0f ? L.T("tout", "all") : Game.Dollars(v), step: 500f);
            Players = ui.Toggle("vis.players", L.T("Joueurs", "Players"), L.T("Nom et santé de tes coéquipiers.", "Your teammates' names and health."), Players);
            DeadHeads = ui.Toggle("vis.heads", L.T("Têtes des joueurs morts", "Dead players' heads"),
                L.T("Pour les ramener au camion et les ranimer.", "To carry them back to the truck and revive them."), DeadHeads);
            Extractions = ui.Toggle("vis.extract", L.T("Points d'extraction", "Extraction points"), null, Extractions);
            Items = ui.Toggle("vis.items", L.T("Objets et armes", "Items and weapons"),
                L.T("Le matériel acheté ou trouvé dans le niveau.", "Gear bought or found in the level."), Items);
            MaxDistance = ui.Slider("vis.dist", L.T("Distance maximale", "Maximum distance"), MaxDistance, 20f, 400f, v => v.ToString("0") + " m", step: 10f,
                desc: L.T("Les ennemis, joueurs et extractions portent quatre fois plus loin.", "Enemies, players and extractions reach four times further."));

            ui.Section(L.T("Carte", "Map"));
            int m = ui.Buttons("vis.map", !Game.InGame,
                new Btn(L.T("Révéler toute la carte", "Reveal the whole map"), ButtonKind.Primary),
                L.T("Objets de valeur sur la carte", "Valuables on the map"));
            if (m == 0) { int n = RevealMap(); Toasts.Show(L.T($"{n} pièce(s) révélée(s) sur ta carte.", $"{n} room(s) revealed on your map.")); }
            if (m == 1) { int n = MapValuables(); Toasts.Show(L.T($"{n} objet(s) de valeur ajouté(s) à ta carte.", $"{n} valuable(s) added to your map.")); }
            ui.Note(L.T("Seule ta carte change : les autres joueurs ne voient rien de nouveau.",
                "Only your map changes: other players see nothing new."));

            ui.Section(L.T("Éclairage", "Lighting"));
            Fullbright = ui.Toggle("vis.full", L.T("Pleine lumière", "Fullbright"),
                L.T("Éclaire les zones sombres sans lampe torche.", "Lights up dark areas without a flashlight."), Fullbright);
            Brightness = ui.Slider("vis.bright", L.T("Intensité", "Intensity"), Brightness, 0.2f, 1.2f, v => (v * 100f).ToString("0") + " %", step: 0.05f);
            NoFog = ui.Toggle("vis.fog", L.T("Sans brouillard", "No fog"),
                L.T("Voit jusqu'au bout des couloirs.", "See to the end of every corridor."), NoFog);
        }
    }
}
