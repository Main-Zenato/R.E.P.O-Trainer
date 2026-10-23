using System.Collections.Generic;
using RepoTrainer.UI;
using UnityEngine;

namespace RepoTrainer.Features
{
    /// <summary>
    /// Cosmetic tokens and unlocks. R.E.P.O cosmetics are earned in play (tokens found during runs
    /// are spent at the cosmetic machine); none are paid. Everything here lives in the local meta
    /// save, so it works on any screen and in any game.
    /// </summary>
    internal static class Cosmetics
    {
        private static readonly SemiFunc.Rarity[] Rarities =
            { SemiFunc.Rarity.Common, SemiFunc.Rarity.Uncommon, SemiFunc.Rarity.Rare, SemiFunc.Rarity.UltraRare };

        private static float _confirmUntil;

        private static bool Ready => MetaManager.instance && MetaManager.instance.saveReady;

        private static string RarityName(SemiFunc.Rarity r)
        {
            switch (r)
            {
                case SemiFunc.Rarity.Common: return L.T("Commun", "Common");
                case SemiFunc.Rarity.Uncommon: return L.T("Peu commun", "Uncommon");
                case SemiFunc.Rarity.Rare: return L.T("Rare", "Rare");
                default: return L.T("Ultra rare", "Ultra rare");
            }
        }

        public static void AddTokens(SemiFunc.Rarity rarity, int amount)
        {
            var m = MetaManager.instance;
            for (int i = 0; i < amount; i++) m.cosmeticTokens.Add((int)rarity);
            // The token strip only exists on some screens; refresh it when it does.
            if (CosmeticTokenUI.instance) CosmeticTokenUI.instance.Setup();
            m.Save();
        }

        public static int TokenCount(SemiFunc.Rarity rarity)
        {
            int n = 0;
            foreach (int t in MetaManager.instance.cosmeticTokens) if (t == (int)rarity) n++;
            return n;
        }

        private static int Total()
        {
            int n = 0;
            foreach (var c in MetaManager.instance.cosmeticAssets) if (c) n++;
            return n;
        }

        public static void Draw(Ui ui)
        {
            if (!Ready) { ui.Empty(L.T("Ta sauvegarde de cosmétiques n'est pas encore chargée.", "Your cosmetics save isn't loaded yet.")); return; }
            var m = MetaManager.instance;

            ui.Section(L.T("Collection", "Collection"));
            ui.Stat(L.T("Cosmétiques débloqués", "Cosmetics unlocked"), m.cosmeticUnlocks.Count + " / " + Total(), Theme.Amber);
            ui.Stat(L.T("Jetons en réserve", "Tokens in reserve"), m.cosmeticTokens.Count.ToString());

            ui.Section(L.T("Jetons", "Tokens"));
            foreach (var r in Rarities)
            {
                int c = ui.ListRow("cos.tok." + r, RarityName(r), L.T("En réserve : ", "In reserve: ") + TokenCount(r), Ui.Open, null,
                    new Btn("+1", ButtonKind.Primary), new Btn("+5"));
                if (c < 0) continue;
                int add = c == 0 ? 1 : 5;
                AddTokens(r, add);
                Toasts.Show(L.T($"{add} jeton(s) {RarityName(r).ToLowerInvariant()} ajouté(s).", $"{add} {RarityName(r).ToLowerInvariant()} token(s) added."));
            }
            ui.Note(L.T("Les jetons se dépensent à la machine à cosmétiques du lobby, qui tire un cosmétique de la rareté choisie.",
                "Tokens are spent at the cosmetics machine in the lobby, which draws a cosmetic of that rarity."));

            ui.Section(L.T("Tout débloquer", "Unlock everything"));
            bool confirming = Time.unscaledTime < _confirmUntil;
            int u = ui.Buttons("cos.unlock", false, new Btn(confirming ? L.T("Clique encore pour confirmer", "Click again to confirm")
                : L.T("Débloquer tous les cosmétiques", "Unlock every cosmetic"), ButtonKind.Danger));
            if (u == 0)
            {
                if (!confirming)
                {
                    _confirmUntil = Time.unscaledTime + 4f;
                    Toasts.Show(L.T("C'est définitif : clique une deuxième fois pour confirmer.", "This is permanent: click a second time to confirm."));
                }
                else
                {
                    _confirmUntil = 0f;
                    bool changed = m.CosmeticUnlockAll();
                    Toasts.Show(changed ? L.T("Tous les cosmétiques sont débloqués.", "Every cosmetic is unlocked.")
                        : L.T("Tu avais déjà tout.", "You already had everything."));
                }
            }
            ui.Note(L.T("Enregistré dans ta sauvegarde de cosmétiques, pas dans une partie : ça ne peut pas être annulé depuis le trainer.",
                "Saved in your cosmetics save, not in a run: the trainer can't undo it."));
        }

        public static void CollectActive(List<string> list) { }
    }
}
