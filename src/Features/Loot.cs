using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using RepoTrainer.UI;
using UnityEngine;

namespace RepoTrainer.Features
{
    /// <summary>Valuables and gear: where they are, bringing them over, keeping them intact.</summary>
    internal static class Loot
    {
        public static bool Indestructible, InfiniteBattery;
        private const int ListLimit = 25;

        public static void Reset()
        {
            Indestructible = InfiniteBattery = false;
            Tick();
        }

        // Valuables waiting to be brought over. A guest asks the host to move each one, so they are
        // sent a few times a second rather than in one burst that host-side anti-spam mods may kick for.
        private static readonly Queue<KeyValuePair<ValuableObject, Vector3>> Pending = new Queue<KeyValuePair<ValuableObject, Vector3>>();
        private static float _nextSend;
        private static bool _batteryApplied;

        public static void Tick()
        {
            // Battery levels are counted on the host, so only the host can keep them full.
            bool battery = InfiniteBattery && Game.IsHost;
            if (RoundDirector.instance && (battery || _batteryApplied))
            {
                RoundDirector.instance.debugInfiniteBattery = battery;
                _batteryApplied = battery;
            }

            if (Pending.Count == 0) return;
            if (!Game.InGame) { Pending.Clear(); return; }
            if (Game.IsHost)
            {
                while (Pending.Count > 0) { var p = Pending.Dequeue(); Bring(p.Key, p.Value); }
                return;
            }
            if (Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + 0.12f;
            var next = Pending.Dequeue();
            Bring(next.Key, next.Value);
        }

        /// <summary>Damage is decided on the host; skipping it there keeps the price intact for everyone.</summary>
        // The three impact entry points are patched as well as Break itself, in case the JIT inlines Break.
        [HarmonyPatch(typeof(PhysGrabObjectImpactDetector))]
        private static class BreakPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(PhysGrabObjectImpactDetector.Break))]
            [HarmonyPatch(nameof(PhysGrabObjectImpactDetector.BreakHeavy))]
            [HarmonyPatch(nameof(PhysGrabObjectImpactDetector.BreakMedium))]
            [HarmonyPatch(nameof(PhysGrabObjectImpactDetector.BreakLight))]
            private static bool Prefix(PhysGrabObjectImpactDetector __instance, bool _forceBreak) =>
                !(Indestructible && __instance.isValuable && !_forceBreak);
        }

        private static void Bring(ValuableObject v, Vector3 at)
        {
            if (!v || !v.physGrabObject) return;
            v.physGrabObject.Teleport(at, Quaternion.identity);
        }

        /// <summary>Stacks valuables in a small grid in front of the player so they don't explode on arrival.</summary>
        private static void BringAll()
        {
            int n = 0;
            var cam = Game.Camera;
            if (!cam) return;
            var fwd = cam.transform.forward; fwd.y = 0; fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            var origin = Game.Me.transform.position + fwd * 2f + Vector3.up * 0.6f;
            foreach (var v in Scan.Valuables)
            {
                if (!v) continue;
                int col = n % 5, row = n / 5 % 5, layer = n / 25;
                Pending.Enqueue(new KeyValuePair<ValuableObject, Vector3>(v,
                    origin + right * ((col - 2) * 0.9f) + fwd * (row * 0.9f) + Vector3.up * (layer * 0.9f)));
                n++;
            }
            Toasts.Show(n == 0 ? L.T("Aucun objet de valeur dans le niveau.", "No valuables in the level.")
                : Game.IsHost ? L.T($"{n} objet(s) de valeur devant toi.", $"{n} valuable(s) in front of you.")
                : L.T($"{n} objet(s) de valeur en route, quelques secondes…", $"{n} valuable(s) on their way, a few seconds…"));
        }

        /// <summary>
        /// Drops every valuable into the extraction point's zone, in a tight stack so they all
        /// count towards the haul. The point must be activated (button pressed) to collect them.
        /// </summary>
        internal static void SendAllToExtraction()
        {
            var ep = Teleport.FindExtraction();
            if (!ep) { Toasts.Show(L.T("Aucun point d'extraction dans ce niveau.", "No extraction point in this level.")); return; }
            // Same call as pressing its button; a guest's press is forwarded to the host.
            if (ep.StateIs(ExtractionPoint.State.Idle) && !ep.isLocked) ep.OnClick();
            var center = ep.transform.position + Vector3.up * 1.2f;
            int n = 0;
            foreach (var v in Scan.Valuables)
            {
                if (!v) continue;
                int col = n % 3, row = n / 3 % 3, layer = n / 9;
                Pending.Enqueue(new KeyValuePair<ValuableObject, Vector3>(v,
                    center + new Vector3((col - 1) * 0.6f, layer * 0.6f, (row - 1) * 0.6f)));
                n++;
            }
            Toasts.Show(n == 0 ? L.T("Aucun objet de valeur dans le niveau.", "No valuables in the level.")
                : L.T($"{n} objet(s) envoyé(s) à l'extraction, qui est activée.",
                    $"{n} valuable(s) sent to extraction, which is now active."));
        }

        private static void MultiplyValues(float factor)
        {
            int n = 0;
            foreach (var v in Scan.Valuables)
            {
                if (!v) continue;
                float value = Mathf.Round(v.dollarValueCurrent * factor);
                if (Game.Multiplayer && v.photonView) v.photonView.RPC("DollarValueSetRPC", RpcTarget.All, value);
                else v.DollarValueSetRPC(value);
                n++;
            }
            Toasts.Show(L.T($"Valeur de {n} objet(s) multipliée par {factor:0}.", $"{n} valuable(s) now worth {factor:0} times more."));
        }

        public static void CollectActive(List<string> list)
        {
            if (InfiniteBattery && Game.IsHost) list.Add(L.T("batterie infinie", "infinite battery"));
            if (Indestructible && Game.IsHost) list.Add(L.T("objets incassables", "unbreakable valuables"));
        }

        public static void Draw(Ui ui)
        {
            bool host = Game.IsHost, inGame = Game.InGame;

            ui.Section(L.T("Protection", "Protection"));
            Indestructible = ui.Toggle("loot.indestr", L.T("Objets de valeur incassables", "Unbreakable valuables"),
                L.T("Les chocs ne font plus baisser leur prix.", "Knocks no longer lower their price."), Indestructible, !host);
            InfiniteBattery = ui.Toggle("loot.battery", L.T("Batterie infinie", "Infinite battery"),
                L.T("Armes, drones et lampes ne se déchargent plus.", "Weapons, drones and lights never run flat."), InfiniteBattery, !host);

            ui.Section(L.T("Collecte", "Haul"));
            if (inGame)
            {
                ui.Stat(L.T("Objets de valeur restants", "Valuables left"), Scan.Valuables.Count.ToString());
                ui.Stat(L.T("Valeur totale", "Total value"), Game.Dollars(Scan.TotalValue), Theme.Amber);
                if (RoundDirector.instance && RoundDirector.instance.haulGoal > 0)
                    ui.Stat(L.T("Objectif d'extraction", "Extraction goal"),
                        Game.Dollars(RoundDirector.instance.currentHaul) + " / " + Game.Dollars(RoundDirector.instance.haulGoal));
            }
            int b = ui.Buttons("loot.all", !inGame,
                new Btn(L.T("Tout amener devant moi", "Bring everything to me"), ButtonKind.Primary),
                L.T("Tout envoyer à l'extraction", "Send everything to extraction"));
            if (b == 0) BringAll();
            if (b == 1) SendAllToExtraction();
            int m = ui.Buttons("loot.mult", !host || !inGame, L.T("Valeur ×2", "Value ×2"), L.T("Valeur ×5", "Value ×5"));
            if (m == 0) MultiplyValues(2f);
            if (m == 1) MultiplyValues(5f);
            ui.Note(L.T("« Tout amener » marche aussi chez les autres : c'est l'hôte qui déplace les objets à ta demande.",
                "\"Bring everything\" also works in other people's games: the host moves the objects on your request."));

            ui.Section(L.T("Les plus chers", "Most valuable"));
            if (!inGame) { ui.Empty(L.T("Lance une partie pour voir les objets.", "Start a game to see the valuables.")); return; }
            if (Scan.Valuables.Count == 0) { ui.Empty(L.T("Tout a été ramassé ou détruit.", "Everything has been collected or destroyed.")); return; }

            int shown = 0;
            foreach (var v in Scan.Valuables)
            {
                if (!v) continue;
                if (shown++ >= ListLimit) break;
                string name = v.name.Replace("(Clone)", "").Replace("Valuable ", "").Trim();
                string caption = Game.Meters(Game.Distance(v.transform.position));
                int c = ui.ListRow("loot.row" + v.GetInstanceID(), Game.Dollars(v.dollarValueCurrent) + "  " + name, caption, Ui.Open, Theme.Amber,
                    L.T("Y aller", "Go"), new Btn(L.T("Amener", "Bring"), ButtonKind.Primary));
                if (c == 0) { Game.TeleportTo(v.transform.position + Vector3.up * 1.5f); Toasts.Show(L.T("Téléporté.", "Teleported.")); }
                if (c == 1) { Bring(v, Game.InFront()); Toasts.Show(name + L.T(" est devant toi.", " is in front of you.")); }
            }
        }
    }
}
