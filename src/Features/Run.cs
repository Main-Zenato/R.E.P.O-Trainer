using System.Collections.Generic;
using RepoTrainer.UI;
using UnityEngine;

namespace RepoTrainer.Features
{
    /// <summary>Run-wide state owned by the host: money, upgrades and level flow.</summary>
    internal static class Run
    {
        internal readonly struct Upgrade
        {
            public readonly string Key, Fr, En;
            public Upgrade(string key, string fr, string en) { Key = key; Fr = fr; En = en; }
            public string Name => L.T(Fr, En);
        }

        // Keys match the game's own "playerUpgrade…" dictionaries and its tester upgrade command.
        public static readonly Upgrade[] Upgrades =
        {
            new Upgrade("Health", "Santé", "Health"),
            new Upgrade("Stamina", "Endurance", "Stamina"),
            new Upgrade("Speed", "Vitesse de sprint", "Sprint speed"),
            new Upgrade("ExtraJump", "Saut supplémentaire", "Extra jump"),
            new Upgrade("Strength", "Force", "Strength"),
            new Upgrade("Range", "Portée", "Range"),
            new Upgrade("Throw", "Lancer", "Throw"),
            new Upgrade("Launch", "Propulsion en boule", "Tumble launch"),
            new Upgrade("TumbleWings", "Ailes en boule", "Tumble wings"),
            new Upgrade("TumbleClimb", "Escalade en boule", "Tumble climb"),
            new Upgrade("CrouchRest", "Repos accroupi", "Crouch rest"),
            new Upgrade("DeathHeadBattery", "Batterie de la tête", "Death head battery"),
            new Upgrade("MapPlayerCount", "Joueurs sur la carte", "Map player count"),
        };

        private static string _amount = "50";

        public static int Currency => StatsManager.instance ? StatsManager.instance.GetRunStatCurrency() : 0;

        public static void SetCurrency(int thousands)
        {
            if (!StatsManager.instance || !PunManager.instance) return;
            SemiFunc.StatSetRunCurrency(Mathf.Max(0, thousands));
        }

        public static int Level(PlayerAvatar p, string key)
        {
            if (!p || !StatsManager.instance) return 0;
            return StatsManager.instance.dictionaryOfDictionaries.TryGetValue("playerUpgrade" + key, out var d)
                ? d.GetValueOrDefault(p.steamID, 0) : 0;
        }

        /// <summary>Uses the game's tester upgrade RPC, which every client accepts from the host.</summary>
        public static void Give(PlayerAvatar p, string key, int amount)
        {
            if (!p || !PunManager.instance || string.IsNullOrEmpty(p.steamID)) return;
            if (Game.Multiplayer) PunManager.instance.photonView.RPC("TesterUpgradeCommandRPC", Photon.Pun.RpcTarget.All, p.steamID, key, amount);
            else PunManager.instance.TesterUpgradeCommandRPC(p.steamID, key, amount);
        }

        public static void GiveAll(PlayerAvatar p, int amount)
        {
            foreach (var u in Upgrades) Give(p, u.Key, amount);
        }

        /// <summary>
        /// An extraction succeeds once its haul reaches its goal; the goal is set by the host with
        /// the point's own sync call. At least one dollar must be inside, or the bar would divide by zero.
        /// </summary>
        internal static bool CompleteExtraction()
        {
            var ep = RoundDirector.instance ? RoundDirector.instance.extractionPointCurrent : null;
            if (!ep || !ep.StateIs(ExtractionPoint.State.Active))
            {
                Toasts.Show(L.T("Active d'abord un point d'extraction (son bouton).", "Activate an extraction point first (its button)."));
                return false;
            }
            if (ep.haulCurrent <= 0)
            {
                Toasts.Show(L.T("Mets au moins un objet de valeur dans l'extraction.", "Put at least one valuable in the extraction."));
                return false;
            }
            ep.HaulGoalSet(ep.haulCurrent);
            Toasts.Show(L.T("Objectif atteint : l'extraction se valide.", "Goal reached: the extraction completes."));
            return true;
        }

        public static void CollectActive(List<string> list) { }

        public static void Draw(Ui ui)
        {
            bool host = Game.IsHost, inGame = Game.InGame;
            bool locked = !host || !inGame;

            ui.Section(L.T("Argent", "Money"));
            if (inGame) ui.Stat(L.T("En caisse", "In the bank"), Game.Money(Currency), Theme.Amber);
            int q = ui.Buttons("run.add", locked, "+$10K", "+$50K", "+$100K", "+$1M");
            if (q >= 0)
            {
                int add = q == 0 ? 10 : q == 1 ? 50 : q == 2 ? 100 : 1000;
                SetCurrency(Currency + add);
                Toasts.Show(L.T("Ajouté : ", "Added: ") + Game.Money(add) + L.T(". Total : ", ". Total: ") + Game.Money(Currency) + ".");
            }
            _amount = ui.TextField("run.amount", L.T("Montant exact (en milliers)", "Exact amount (in thousands)"), _amount, 160);
            int s = ui.Buttons("run.set", locked, new Btn(L.T("Définir le montant", "Set the amount"), ButtonKind.Primary),
                new Btn(L.T("Remettre à zéro", "Reset to zero"), ButtonKind.Danger));
            if (s == 0)
            {
                if (int.TryParse(_amount.Replace(" ", ""), out int v) && v >= 0)
                {
                    SetCurrency(v);
                    Toasts.Show(L.T("Caisse : ", "Bank: ") + Game.Money(v) + ".");
                }
                else Toasts.Show(L.T("Entre un nombre entier, par exemple 250 pour $250K.", "Enter a whole number, for example 250 for $250K."));
            }
            if (s == 1) { SetCurrency(0); Toasts.Show(L.T("Caisse remise à zéro.", "Bank reset to zero.")); }
            ui.Note(L.T("L'argent sert au magasin entre deux niveaux. Il est partagé par toute l'équipe.",
                "Money is spent in the shop between levels. The whole team shares it."));

            ui.Section(L.T("Mes améliorations", "My upgrades"));
            var me = Game.Me;
            int all = ui.Buttons("run.upall", locked, new Btn(L.T("Tout +1", "All +1"), ButtonKind.Primary), L.T("Tout +5", "All +5"),
                new Btn(L.T("Tout retirer", "Remove all"), ButtonKind.Danger));
            if (all == 0 || all == 1) { GiveAll(me, all == 0 ? 1 : 5); Toasts.Show(L.T("Améliorations ajoutées.", "Upgrades added.")); }
            if (all == 2)
            {
                foreach (var u in Upgrades) Give(me, u.Key, -Level(me, u.Key));
                Toasts.Show(L.T("Améliorations retirées.", "Upgrades removed."));
            }
            foreach (var u in Upgrades)
            {
                int lvl = Level(me, u.Key);
                int c = ui.ListRow("run.up." + u.Key, u.Name, L.T("Niveau ", "Level ") + lvl, locked ? 0 : Ui.Open, null,
                    new Btn("−1"), new Btn("+1", ButtonKind.Primary), new Btn("+5"));
                if (c < 0) continue;
                Give(me, u.Key, c == 0 ? -1 : c == 1 ? 1 : 5);
            }

            ui.Section(L.T("Niveau", "Level"));
            if (inGame && StatsManager.instance)
                ui.Stat(L.T("Niveaux terminés", "Levels completed"), StatsManager.instance.runStats.GetValueOrDefault("level", 0).ToString());
            if (ui.Buttons("run.extract", locked, new Btn(L.T("Valider l'extraction en cours", "Complete the current extraction"), ButtonKind.Primary)) == 0)
                CompleteExtraction();
            ui.Note(L.T("Abaisse l'objectif du point d'extraction activé à ce qu'il contient déjà : il se valide une seconde et demie plus tard.",
                "Lowers the active extraction point's goal to what it already holds: it completes a second and a half later."));
            if (ui.Buttons("run.doors", locked, L.T("Casser toutes les portes", "Break every door")) == 0)
            {
                int n = 0;
                foreach (var h in Object.FindObjectsByType<PhysGrabHinge>(FindObjectsSortMode.None))
                    if (h && !h.dead) { h.DestroyHinge(); n++; }
                Toasts.Show(L.T($"{n} porte(s) arrachée(s) de leurs gonds.", $"{n} door(s) torn off their hinges."));
            }
            int lv = ui.Buttons("run.level", locked, new Btn(L.T("Passer au niveau suivant", "Skip to the next level"), ButtonKind.Danger));
            if (lv == 0 && RunManager.instance)
            {
                RunManager.instance.ChangeLevel(_completedLevel: true, _levelFailed: false);
                Toasts.Show(L.T("Chargement du niveau suivant…", "Loading the next level…"));
            }
            ui.Note(L.T("Compte comme un niveau réussi. Les objets non extraits sont perdus.",
                "Counts as a completed level. Anything not extracted is lost."));
        }
    }
}
