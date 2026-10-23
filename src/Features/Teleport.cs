using System.Collections.Generic;
using RepoTrainer.UI;
using UnityEngine;

namespace RepoTrainer.Features
{
    /// <summary>
    /// Crosshair teleport, the truck, extraction points and saved spots. Levels are generated
    /// fresh every time, so saved spots only last for the current level.
    /// </summary>
    internal static class Teleport
    {
        private const int Slots = 5;
        private static readonly Vector3?[] Spots = new Vector3?[Slots];
        private static LevelGenerator _level;
        private static readonly RaycastHit[] Hits = new RaycastHit[32];

        public static void Tick()
        {
            if (_level == LevelGenerator.Instance) return;
            _level = LevelGenerator.Instance;
            for (int i = 0; i < Slots; i++) Spots[i] = null;
        }

        public static void ToCrosshair()
        {
            var cam = Game.Camera;
            if (!Game.InGame || !cam) return;
            int n = Physics.RaycastNonAlloc(cam.transform.position, cam.transform.forward, Hits, 300f, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit best = default;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (h.distance >= bestDist || IsMine(h.collider)) continue;
                best = h;
                bestDist = h.distance;
            }
            if (bestDist == float.MaxValue)
            {
                Toasts.Show(L.T("Rien en vue à moins de 300 m.", "Nothing in sight within 300 m."));
                return;
            }
            // Step back from walls and lift off floors so we don't land inside geometry.
            var dest = best.point + best.normal * 0.6f;
            if (best.normal.y > 0.5f) dest = best.point + Vector3.up * 0.3f;
            Game.TeleportTo(dest);
            Toasts.Show(L.T("Téléporté à ", "Teleported ") + Game.Meters(bestDist) + L.T(".", " away."));
        }

        private static bool IsMine(Collider c)
        {
            var pc = Game.Controller;
            var me = Game.Me;
            var t = c.transform;
            return (pc && t.IsChildOf(pc.transform)) || (me && t.IsChildOf(me.transform));
        }

        public static Vector3? TruckPosition()
        {
            if (TruckSafetySpawnPoint.instance) return TruckSafetySpawnPoint.instance.transform.position;
            var sp = Object.FindAnyObjectByType<SpawnPoint>();
            return sp ? sp.transform.position : (Vector3?)null;
        }

        private static void ToTruck()
        {
            var p = TruckPosition();
            if (p == null) { Toasts.Show(L.T("Camion introuvable dans ce niveau.", "No truck in this level.")); return; }
            Game.TeleportTo(p.Value + Vector3.up * 0.3f);
            Toasts.Show(L.T("De retour au camion.", "Back at the truck."));
        }

        /// <summary>The active extraction point, or the nearest one; a spot just in front of it.</summary>
        public static Vector3? ExtractionPosition()
        {
            var best = FindExtraction();
            return best ? best.transform.position + best.transform.forward * 2f + Vector3.up * 0.5f : (Vector3?)null;
        }

        /// <summary>The extraction point in use, otherwise the closest one.</summary>
        public static ExtractionPoint FindExtraction()
        {
            ExtractionPoint best = null;
            if (RoundDirector.instance && RoundDirector.instance.extractionPointCurrent)
                best = RoundDirector.instance.extractionPointCurrent;
            float bestD = float.MaxValue;
            if (!best)
                foreach (var ep in Scan.Extractions)
                {
                    if (!ep) continue;
                    float d = Game.Distance(ep.transform.position);
                    if (d < bestD) { bestD = d; best = ep; }
                }
            return best;
        }

        private static void ToExtraction()
        {
            var p = ExtractionPosition();
            if (p == null) { Toasts.Show(L.T("Aucun point d'extraction dans ce niveau.", "No extraction point in this level.")); return; }
            Game.TeleportTo(p.Value);
            Toasts.Show(L.T("Téléporté au point d'extraction.", "Teleported to the extraction point."));
        }

        public static void Draw(Ui ui)
        {
            bool inGame = Game.InGame;

            ui.Section(L.T("Destinations", "Destinations"));
            int d = ui.Buttons("tp.dest", !inGame,
                new Btn(L.T("Au viseur", "To crosshair"), ButtonKind.Primary),
                L.T("Au camion", "To the truck"),
                L.T("À l'extraction", "To extraction"));
            if (!inGame && d >= 0) Toasts.Show(L.T("Lance une partie d'abord.", "Start a game first."));
            else if (d == 0) ToCrosshair();
            else if (d == 1) ToTruck();
            else if (d == 2) ToExtraction();
            ui.Note(L.T($"Touche rapide pour le viseur : {Settings.CrosshairKey.Value}. Pour rejoindre un joueur ou un objet, passe par les onglets Équipe et Objets.",
                $"Crosshair shortcut: {Settings.CrosshairKey.Value}. To reach a player or an object, use the Team and Loot tabs."));

            ui.Section(L.T("Emplacements de ce niveau", "Spots in this level"));
            for (int i = 0; i < Slots; i++)
            {
                var s = Spots[i];
                string title = L.T("Emplacement ", "Spot ") + (i + 1);
                string caption = s.HasValue
                    ? Game.Meters(Game.Distance(s.Value)) + L.T(" d'ici", " from here")
                    : L.T("Vide", "Empty");
                int c = s.HasValue
                    ? ui.ListRow("tp.slot" + i, title, caption, Ui.Open, Theme.Amber,
                        new Btn(L.T("Y aller", "Go"), ButtonKind.Primary), L.T("Remplacer", "Replace"), new Btn(L.T("Effacer", "Clear"), ButtonKind.Danger))
                    : ui.ListRow("tp.slot" + i, title, caption, Ui.Open, null, L.T("Enregistrer ici", "Save here"));
                if (c < 0) continue;
                if (!inGame) { Toasts.Show(L.T("Lance une partie d'abord.", "Start a game first.")); continue; }
                if (!s.HasValue || c == 1)
                {
                    Spots[i] = Game.Me.transform.position + Vector3.up * 0.2f;
                    Toasts.Show(title + L.T(" enregistré.", " saved."));
                }
                else if (c == 0) { Game.TeleportTo(s.Value); Toasts.Show(L.T("Téléporté à l'", "Teleported to ") + title.ToLowerInvariant() + "."); }
                else if (c == 2) { Spots[i] = null; Toasts.Show(title + L.T(" effacé.", " cleared.")); }
            }
            ui.Note(L.T("Chaque niveau est généré au hasard : les emplacements s'effacent quand tu changes de niveau.",
                "Every level is randomly generated, so spots are cleared when the level changes."));
        }

        public static void CollectActive(List<string> list) { }
    }
}
