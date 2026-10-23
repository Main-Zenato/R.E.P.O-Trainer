using System.Collections.Generic;
using RepoTrainer.UI;
using UnityEngine;

namespace RepoTrainer.Features
{
    /// <summary>Enemy list and controls. Enemy AI runs on the host, so most actions are host-only.</summary>
    internal static class Enemies
    {
        public static bool Invisible, FreezeAll;
        public static float SpeedMult = 1f;
        private static float _nextFreeze;
        private static string _hiddenId;

        public static void Reset()
        {
            Invisible = FreezeAll = false;
            SpeedMult = 1f;
            Tick();
        }

        // ---- Speed -------------------------------------------------------------------------

        // Enemies keep rewriting their agent speed (chases, overrides, resets to default). We remember
        // the last speed we wrote: if the agent now holds something else, the game set a new base.
        private struct Applied { public float Base, BaseAccel, Speed; }
        private static readonly Dictionary<EnemyNavMeshAgent, Applied> Speeds = new Dictionary<EnemyNavMeshAgent, Applied>();

        [HarmonyLib.HarmonyPatch(typeof(EnemyNavMeshAgent), "Update")]
        private static class AgentSpeedPatch
        {
            private static void Postfix(EnemyNavMeshAgent __instance)
            {
                var agent = __instance.Agent;
                if (!agent || !Game.IsHost) return;
                bool custom = !Mathf.Approximately(SpeedMult, 1f);
                bool known = Speeds.TryGetValue(__instance, out var a);
                if (!custom)
                {
                    if (!known) return;
                    if (Mathf.Approximately(agent.speed, a.Speed)) { agent.speed = a.Base; agent.acceleration = a.BaseAccel; }
                    Speeds.Remove(__instance);
                    return;
                }
                if (!known || !Mathf.Approximately(agent.speed, a.Speed))
                {
                    a.Base = agent.speed;
                    a.BaseAccel = agent.acceleration;
                }
                a.Speed = a.Base * SpeedMult;
                agent.speed = a.Speed;
                agent.acceleration = a.BaseAccel * Mathf.Max(1f, SpeedMult);
                Speeds[__instance] = a;
            }
        }

        public static void Tick()
        {
            var dir = EnemyDirector.instance;
            if (!Game.InGame || !dir) { _hiddenId = null; Speeds.Clear(); return; }

            // Enemies skip anyone on this list when looking for targets (the game's own tester switch).
            string id = Game.Me ? Game.Me.steamID : null;
            bool want = Invisible && Game.IsHost && !string.IsNullOrEmpty(id);
            if (want && !dir.debugNoVision.Contains(id)) dir.debugNoVision.Add(id);
            if (!want && _hiddenId != null && !(Team.TeamInvisible && Game.IsHost)) dir.debugNoVision.Remove(_hiddenId);
            _hiddenId = want ? id : null;

            if (FreezeAll && Game.IsHost && Time.unscaledTime >= _nextFreeze)
            {
                _nextFreeze = Time.unscaledTime + 0.5f;
                foreach (var e in Scan.Enemies)
                    if (e && e.Enemy) e.Enemy.Freeze(1f);
            }
        }

        public static void Kill(EnemyParent e)
        {
            if (!e || !e.Enemy) return;
            if (e.Enemy.HasHealth) e.Enemy.Health.Hurt(e.Enemy.Health.healthCurrent + 9999, Vector3.up);
            else e.Despawn(); // a few enemies can't be hurt: send them away instead
        }

        private static void ForAll(System.Action<EnemyParent> act, string done)
        {
            int n = 0;
            foreach (var e in Scan.Enemies.ToArray())
                if (e) { act(e); n++; }
            Toasts.Show(n == 0 ? L.T("Aucun ennemi dans le niveau.", "No enemies in the level.") : string.Format(done, n));
        }

        public static void CollectActive(List<string> list)
        {
            if (!Game.IsHost) return;
            if (Invisible) list.Add(L.T("invisible pour les ennemis", "invisible to enemies"));
            if (FreezeAll) list.Add(L.T("ennemis figés", "enemies frozen"));
            if (!Mathf.Approximately(SpeedMult, 1f)) list.Add(L.T("ennemis ×", "enemies ×") + SpeedMult.ToString("0.00"));
        }

        public static void Draw(Ui ui)
        {
            bool host = Game.IsHost, inGame = Game.InGame;

            ui.Section(L.T("Comportement", "Behaviour"));
            Invisible = ui.Toggle("en.invis", L.T("Invisible pour les ennemis", "Invisible to enemies"),
                L.T("Ils ne te voient plus et ne te prennent plus en chasse.", "They no longer see you or chase you."), Invisible, !host);
            FreezeAll = ui.Toggle("en.freeze", L.T("Figer tous les ennemis", "Freeze every enemy"),
                L.T("Ils restent immobiles tant que l'option est active.", "They stand still while this is on."), FreezeAll, !host);
            SpeedMult = ui.Slider("en.speed", L.T("Vitesse des ennemis", "Enemy speed"), SpeedMult, 0.25f, 2f,
                v => "×" + v.ToString("0.00"), !host, 0.05f,
                L.T("Ralentis-les pour fuir plus facilement, ou accélère-les pour corser la partie.", "Slow them to escape more easily, or speed them up for a harder game."));

            ui.Section(L.T("Tous les ennemis", "Every enemy"));
            int a = ui.Buttons("en.all", !host || !inGame,
                new Btn(L.T("Tuer", "Kill"), ButtonKind.Danger),
                L.T("Renvoyer", "Send away"),
                L.T("Figer 20 s", "Freeze 20 s"));
            if (a == 0) ForAll(Kill, L.T("{0} ennemi(s) éliminé(s).", "{0} enemy(ies) killed."));
            if (a == 1) ForAll(e => e.Despawn(), L.T("{0} ennemi(s) renvoyé(s).", "{0} enemy(ies) sent away."));
            if (a == 2) ForAll(e => { if (e.Enemy) e.Enemy.Freeze(20f); }, L.T("{0} ennemi(s) figé(s) 20 s.", "{0} enemy(ies) frozen for 20 s."));
            ui.Note(L.T("Les ennemis tués lâchent leur orbe de valeur, comme en jeu normal. Ceux qu'on renvoie reviennent plus tard.",
                "Killed enemies drop their valuable orb, as in normal play. Enemies sent away come back later."));

            ui.Section(L.T("Dans le niveau", "In the level") + (Scan.Enemies.Count > 0 ? "  ·  " + Scan.Enemies.Count : ""));
            if (!inGame) { ui.Empty(L.T("Lance une partie pour voir les ennemis.", "Start a game to see the enemies.")); return; }
            if (Scan.Enemies.Count == 0) { ui.Empty(L.T("Aucun ennemi actif pour l'instant.", "No active enemies right now.")); return; }

            for (int i = 0; i < Scan.Enemies.Count; i++)
            {
                var e = Scan.Enemies[i];
                if (!e) continue;
                string hp = e.Enemy && e.Enemy.HasHealth
                    ? L.T("santé ", "health ") + e.Enemy.Health.healthCurrent + "/" + e.Enemy.Health.health
                    : L.T("invulnérable", "can't be hurt");
                string caption = Game.Meters(Game.Distance(Scan.EnemyPos(e))) + "  ·  " + hp;
                int c = ui.ListRow("en.row" + e.GetInstanceID(), Scan.EnemyName(e), caption, host ? Ui.Open : 1, Theme.Alarm,
                    L.T("Y aller", "Go"), L.T("Figer", "Freeze"), new Btn(L.T("Tuer", "Kill"), ButtonKind.Danger));
                if (c == 0) { Game.TeleportTo(Scan.EnemyPos(e) + Vector3.up * 2.5f); Toasts.Show(L.T("Téléporté près de ", "Teleported near ") + Scan.EnemyName(e) + "."); }
                if (c == 1 && e.Enemy) { e.Enemy.Freeze(20f); Toasts.Show(Scan.EnemyName(e) + L.T(" figé 20 s.", " frozen for 20 s.")); }
                if (c == 2) { Kill(e); Toasts.Show(Scan.EnemyName(e) + L.T(" éliminé.", " killed.")); }
            }
        }
    }
}
