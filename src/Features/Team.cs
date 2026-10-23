using System.Collections.Generic;
using RepoTrainer.UI;
using UnityEngine;

namespace RepoTrainer.Features
{
    /// <summary>Everyone in the room: going to them, healing, reviving, upgrading and kicking.</summary>
    internal static class Team
    {
        public static bool TeamInvisible, Regen;
        public static float RegenAmount = 5f;
        private static readonly List<string> Hidden = new List<string>();
        private static float _nextRegen;

        public static void Reset()
        {
            TeamInvisible = Regen = false;
            Tick();
        }

        /// <summary>
        /// Tops every living player up a little every two seconds. Healing goes to each player's
        /// own machine through the game's heal call, which accepts it from anyone.
        /// </summary>
        private static void RegenTick()
        {
            if (!Regen || Time.unscaledTime < _nextRegen || !GameDirector.instance) return;
            _nextRegen = Time.unscaledTime + 2f;
            foreach (var p in GameDirector.instance.PlayerList)
            {
                var hp = p ? p.playerHealth : null;
                if (!hp || p.deadSet || p.isDisabled || hp.health >= hp.maxHealth) continue;
                hp.HealOther(Mathf.Min((int)RegenAmount, hp.maxHealth - hp.health), false);
            }
        }

        public static void Tick()
        {
            if (Game.InGame) RegenTick();
            var dir = EnemyDirector.instance;
            if (!dir || !Game.InGame) { Hidden.Clear(); return; }
            bool on = TeamInvisible && Game.IsHost;
            if (on && GameDirector.instance)
                foreach (var p in GameDirector.instance.PlayerList)
                {
                    if (!p || string.IsNullOrEmpty(p.steamID) || Hidden.Contains(p.steamID)) continue;
                    if (!dir.debugNoVision.Contains(p.steamID)) dir.debugNoVision.Add(p.steamID);
                    Hidden.Add(p.steamID);
                }
            if (!on && Hidden.Count > 0)
            {
                foreach (var id in Hidden)
                    if (!(Enemies.Invisible && Game.Me && Game.Me.steamID == id)) dir.debugNoVision.Remove(id);
                Hidden.Clear();
            }
        }

        private static void Heal(PlayerAvatar p)
        {
            var hp = p.playerHealth;
            if (!hp || p.deadSet) return;
            int missing = hp.maxHealth - hp.health;
            if (missing > 0) hp.HealOther(missing, true);
        }

        private static bool Revive(PlayerAvatar p)
        {
            if (!p || !p.deadSet || !p.playerDeathHead) return false;
            p.Revive();
            return true;
        }

        /// <summary>
        /// Moves a player with the game's own host-only spawn call, which repositions them on
        /// their machine. Tumbling robots and dead players' heads are physics objects the host
        /// can move directly.
        /// </summary>
        public static void Move(PlayerAvatar p, Vector3 pos)
        {
            if (!p) return;
            if (p.deadSet)
            {
                if (p.playerDeathHead && p.playerDeathHead.physGrabObject)
                    p.playerDeathHead.physGrabObject.Teleport(pos, Quaternion.identity);
                return;
            }
            if (p.isLocal) { Game.TeleportTo(pos); return; }
            if (p.isTumbling && p.tumble && p.tumble.physGrabObject)
                p.tumble.physGrabObject.Teleport(pos, p.transform.rotation);
            p.Spawn(pos, p.transform.rotation);
        }

        /// <summary>Sends everyone to a point, spaced on a small circle so nobody lands inside someone else.</summary>
        private static void MoveAll(List<PlayerAvatar> players, Vector3? target, bool includeMe, string done)
        {
            if (target == null) { Toasts.Show(L.T("Destination introuvable dans ce niveau.", "Destination not found in this level.")); return; }
            int i = 0, n = 0;
            foreach (var p in players)
            {
                if (!p || (p.isLocal && !includeMe)) continue;
                float a = i++ * 2.399f; // golden angle keeps points spread whatever the count
                var offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (0.9f + 0.25f * i);
                Move(p, target.Value + offset + Vector3.up * 0.3f);
                n++;
            }
            Toasts.Show(n == 0 ? L.T("Personne d'autre dans la partie.", "Nobody else in the game.") : string.Format(done, n));
        }

        public static void CollectActive(List<string> list)
        {
            if (TeamInvisible && Game.IsHost) list.Add(L.T("équipe invisible", "team invisible"));
            if (Regen) list.Add(L.T("régénération de l'équipe", "team regeneration"));
        }

        public static void Draw(Ui ui)
        {
            bool host = Game.IsHost, inGame = Game.InGame;
            var players = GameDirector.instance ? GameDirector.instance.PlayerList : null;

            ui.Section(L.T("Toute l'équipe", "Whole team"));
            Regen = ui.Toggle("team.regen", L.T("Régénération de l'équipe", "Team regeneration"),
                L.T("Tout le monde récupère de la santé toutes les deux secondes. Marche aussi chez les autres.",
                    "Everyone recovers health every two seconds. Also works in other people's games."), Regen);
            RegenAmount = ui.Slider("team.regenamt", L.T("Santé rendue", "Health restored"), RegenAmount, 1f, 25f,
                v => "+" + v.ToString("0") + L.T(" / 2 s", " / 2 s"), step: 1f);
            TeamInvisible = ui.Toggle("team.invis", L.T("Équipe invisible pour les ennemis", "Team invisible to enemies"),
                L.T("Personne n'est pris en chasse.", "Nobody gets chased."), TeamInvisible, !host);
            int a = ui.Buttons("team.all", !inGame,
                new Btn(L.T("Soigner tout le monde", "Heal everyone"), ButtonKind.Primary),
                L.T("Ranimer tout le monde", "Revive everyone"),
                L.T("Améliorations +1", "Upgrades +1"));
            if (a == 0 && players != null)
            {
                foreach (var p in players) if (p) Heal(p);
                Toasts.Show(L.T("Toute l'équipe est soignée.", "The whole team is healed."));
            }
            if (a == 1 || a == 2)
            {
                if (!host) Toasts.Show(Ui.HostOnlyMessage);
                else if (players != null && a == 1)
                {
                    int n = 0;
                    foreach (var p in players) if (Revive(p)) n++;
                    Toasts.Show(n == 0 ? L.T("Personne n'est mort.", "Nobody is dead.") : L.T($"{n} joueur(s) ranimé(s).", $"{n} player(s) revived."));
                }
                else if (players != null)
                {
                    foreach (var p in players) if (p) Run.GiveAll(p, 1);
                    Toasts.Show(L.T("Chaque joueur gagne un niveau dans toutes les améliorations.", "Every player gains a level in every upgrade."));
                }
            }
            ui.Note(L.T("Soigner marche partout. Ranimer, les améliorations et l'expulsion sont réservés à l'hôte. Un joueur mort doit encore avoir sa tête dans le niveau pour être ranimé.",
                "Healing works anywhere. Reviving, upgrades and kicking are host-only. A dead player's head must still be in the level to revive them."));

            ui.Section(L.T("Rassembler l'équipe", "Gather the team"));
            int g = ui.Buttons("team.gather", !host || !inGame,
                new Btn(L.T("Tous vers moi", "Everyone to me"), ButtonKind.Primary),
                L.T("Tous au camion", "Everyone to the truck"),
                L.T("Tous à l'extraction", "Everyone to extraction"));
            if (g >= 0 && players != null)
            {
                if (g == 0) MoveAll(players, Game.InFront(2f) + Vector3.up * 0.4f, false,
                    L.T("{0} joueur(s) amené(s) vers toi.", "{0} player(s) brought to you."));
                if (g == 1) MoveAll(players, Teleport.TruckPosition(), true, L.T("{0} joueur(s) envoyé(s) au camion.", "{0} player(s) sent to the truck."));
                if (g == 2) MoveAll(players, Teleport.ExtractionPosition(), true, L.T("{0} joueur(s) envoyé(s) à l'extraction.", "{0} player(s) sent to extraction."));
            }
            ui.Note(L.T("Les joueurs morts sont déplacés avec leur tête : ramène-les au camion pour les ranimer.",
                "Dead players move with their head: bring them to the truck to revive them."));

            ui.Section(L.T("Joueurs", "Players") + (players != null && players.Count > 0 ? "  ·  " + players.Count : ""));
            if (!inGame || players == null) { ui.Empty(L.T("Lance une partie pour voir les joueurs.", "Start a game to see the players.")); return; }

            foreach (var p in players)
            {
                if (!p) continue;
                var hp = p.playerHealth;
                string state = p.deadSet ? L.T("mort", "dead")
                    : hp ? L.T("santé ", "health ") + hp.health + "/" + hp.maxHealth : "";
                string caption = (p.isLocal ? L.T("toi", "you") : Game.Meters(Game.Distance(p.transform.position))) + "  ·  " + state;
                var accent = p.deadSet ? Theme.Alarm : Theme.Haul;

                Btn third = p.deadSet ? new Btn(L.T("Ranimer", "Revive"), ButtonKind.Primary) : new Btn(L.T("+1 amélio.", "+1 upgr."));
                int c = p.isLocal
                    ? ui.ListRow("team.row" + p.GetInstanceID(), p.playerName, caption, host ? Ui.Open : 1, accent,
                        new Btn(L.T("Soigner", "Heal"), ButtonKind.Primary), third)
                    : ui.ListRow("team.row" + p.GetInstanceID(), p.playerName, caption, host ? Ui.Open : 2, accent,
                        L.T("Y aller", "Go"), L.T("Soigner", "Heal"), L.T("Amener", "Bring"), third, new Btn(L.T("Expulser", "Kick"), ButtonKind.Danger));
                if (c < 0) continue;

                // Line every row up on the same indices: 0 go, 1 heal, 2 bring, 3 revive/upgrade, 4 kick.
                if (p.isLocal) c = c == 0 ? 1 : 3;
                switch (c)
                {
                    case 0:
                        var pos = p.deadSet && p.playerDeathHead ? p.playerDeathHead.transform.position : p.transform.position;
                        Game.TeleportTo(pos + Vector3.up * 0.5f);
                        Toasts.Show(L.T("Téléporté vers ", "Teleported to ") + p.playerName + ".");
                        break;
                    case 1:
                        if (p.deadSet) { Toasts.Show(p.playerName + L.T(" est mort : ranime-le d'abord.", " is dead: revive them first.")); break; }
                        Heal(p);
                        Toasts.Show(p.playerName + L.T(" est soigné.", " is healed."));
                        break;
                    case 2:
                        if (!host) { Toasts.Show(Ui.HostOnlyMessage); break; }
                        Move(p, Game.InFront(1.6f) + Vector3.up * 0.5f);
                        Toasts.Show(p.playerName + (p.deadSet ? L.T(" (sa tête) est devant toi.", " (their head) is in front of you.") : L.T(" est devant toi.", " is in front of you.")));
                        break;
                    case 3:
                        if (!host) { Toasts.Show(Ui.HostOnlyMessage); break; }
                        if (p.deadSet)
                            Toasts.Show(Revive(p) ? p.playerName + L.T(" est ranimé.", " is revived.")
                                : L.T("Sa tête n'est plus dans le niveau.", "Their head is no longer in the level."));
                        else
                        {
                            Run.GiveAll(p, 1);
                            Toasts.Show(p.playerName + L.T(" gagne un niveau partout.", " gains a level in everything."));
                        }
                        break;
                    case 4:
                        if (!host) { Toasts.Show(Ui.HostOnlyMessage); break; }
                        // Same call as the game's own kick button in the escape menu's player list.
                        NetworkManager.instance.BanPlayer(p);
                        Toasts.Show(p.playerName + L.T(" a été expulsé.", " was kicked."));
                        break;
                }
            }

            // The arena winner's crown, handed out by the host's own sync call; it shows up within a second.
            ui.Section(L.T("Couronne", "Crown"));
            string crowned = SessionManager.instance ? SessionManager.instance.crownedPlayerSteamID : null;
            foreach (var p in players)
            {
                if (!p) continue;
                bool has = !string.IsNullOrEmpty(crowned) && crowned == p.steamID;
                int c = ui.ListRow("team.crown" + p.GetInstanceID(), p.playerName,
                    has ? L.T("Porte la couronne", "Wears the crown") : L.T("Sans couronne", "No crown"),
                    host ? Ui.Open : 0, has ? Theme.Amber : (Color?)null,
                    has ? new Btn(L.T("Retirer", "Remove")) : new Btn(L.T("Couronner", "Crown"), ButtonKind.Primary));
                if (c != 0 || !PunManager.instance) continue;
                PunManager.instance.CrownPlayerSync(has ? "" : p.steamID);
                Toasts.Show(has ? L.T("Couronne retirée.", "Crown removed.") : p.playerName + L.T(" porte la couronne.", " wears the crown."));
            }
        }
    }
}
