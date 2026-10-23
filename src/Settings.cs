using BepInEx.Configuration;
using RepoTrainer.Features;
using RepoTrainer.UI;
using UnityEngine;

namespace RepoTrainer
{
    internal static class Settings
    {
        public static ConfigEntry<KeyCode> MenuKey;
        public static ConfigEntry<KeyCode> NoclipKey;
        public static ConfigEntry<KeyCode> CrosshairKey;
        public static ConfigEntry<KeyCode> VisionKey;
        public static ConfigEntry<float> UiScale;
        public static ConfigEntry<bool> ShowToasts;
        public static ConfigEntry<bool> ShowHud;

        private static ConfigEntry<KeyCode> _rebinding;
        private static float _pendingScale = -1f;

        public static void Bind(ConfigFile cfg)
        {
            L.Bind(cfg);
            MenuKey = cfg.Bind("Touches", "Menu", KeyCode.Insert, "Ouvre et ferme le trainer / Opens and closes the trainer.");
            NoclipKey = cfg.Bind("Touches", "Noclip", KeyCode.F6, "Active ou coupe le noclip / Toggles noclip.");
            CrosshairKey = cfg.Bind("Touches", "TeleportViseur", KeyCode.F7, "Téléporte là où tu vises / Teleports where you aim.");
            VisionKey = cfg.Bind("Touches", "Vision", KeyCode.F8, "Affiche ennemis et objets de valeur à travers les murs / Shows enemies and valuables through walls.");
            UiScale = cfg.Bind("Interface", "Echelle", 1f, new ConfigDescription("Taille du menu / Menu size.", new AcceptableValueRange<float>(0.7f, 1.6f)));
            ShowToasts = cfg.Bind("Interface", "Notifications", true, "Affiche les confirmations / Shows confirmations.");
            ShowHud = cfg.Bind("Interface", "BandeauActifs", true, "Rappelle les options actives / Lists active options.");
            Toasts.Enabled = ShowToasts.Value;
        }

        public static bool Rebinding => _rebinding != null;

        public static void CancelRebind() => _rebinding = null;

        /// <summary>Captures the next key press while a rebind is pending. Call from OnGUI.</summary>
        public static void CaptureKey()
        {
            var e = Event.current;
            if (_rebinding == null || e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;
            if (e.keyCode != KeyCode.Escape)
            {
                _rebinding.Value = e.keyCode;
                Toasts.Show(L.T($"Touche « {e.keyCode} » enregistrée.", $"Key \"{e.keyCode}\" saved."));
            }
            _rebinding = null;
            e.Use();
        }

        private static void KeyRow(Ui ui, string id, string label, ConfigEntry<KeyCode> entry)
        {
            bool waiting = _rebinding == entry;
            string caption = waiting ? L.T("Appuie sur une touche… (Échap pour annuler)", "Press a key… (Esc to cancel)")
                : L.T("Touche actuelle : ", "Current key: ") + entry.Value;
            if (ui.ListRow(id, label, caption, Ui.Open, null, new Btn(waiting ? L.T("En attente", "Waiting") : L.T("Changer", "Change"), ButtonKind.Primary)) == 0)
                _rebinding = waiting ? null : entry;
        }

        public static void Draw(Ui ui)
        {
            ui.Section(L.T("Langue", "Language"));
            int lang = ui.Buttons("set.lang", false,
                new Btn(L.T("Automatique", "Automatic"), L.Setting.Value == Language.Auto ? ButtonKind.Primary : ButtonKind.Secondary),
                new Btn("Français", L.Setting.Value == Language.Francais ? ButtonKind.Primary : ButtonKind.Secondary),
                new Btn("English", L.Setting.Value == Language.English ? ButtonKind.Primary : ButtonKind.Secondary));
            if (lang >= 0) L.Setting.Value = (Language)lang;

            ui.Section(L.T("Touches", "Keys"));
            KeyRow(ui, "key.menu", L.T("Ouvrir le menu", "Open the menu"), MenuKey);
            KeyRow(ui, "key.noclip", "Noclip", NoclipKey);
            KeyRow(ui, "key.tp", L.T("Téléportation au viseur", "Teleport to crosshair"), CrosshairKey);
            KeyRow(ui, "key.vision", L.T("Vision ennemis et objets", "Enemy and loot vision"), VisionKey);

            ui.Section(L.T("Interface", "Interface"));
            // Applied on release: rescaling mid-drag would move the slider out from under the cursor.
            float shown = _pendingScale > 0f ? _pendingScale : UiScale.Value;
            float s = ui.Slider("set.scale", L.T("Taille du menu", "Menu size"), shown, 0.7f, 1.6f, v => (v * 100f).ToString("0") + " %", step: 0.05f);
            if (!Mathf.Approximately(s, shown)) _pendingScale = s;
            if (GUIUtility.hotControl == 0 && _pendingScale > 0f) { UiScale.Value = _pendingScale; _pendingScale = -1f; }

            bool toasts = ui.Toggle("set.toasts", "Notifications",
                L.T("Confirme chaque action par une ligne de terminal en bas à droite.", "Confirms each action with a terminal line in the bottom-right corner."), ShowToasts.Value);
            if (toasts != ShowToasts.Value) { ShowToasts.Value = toasts; Toasts.Enabled = toasts; }
            ShowHud.Value = ui.Toggle("set.hud", L.T("Bandeau des options actives", "Active options strip"),
                L.T("Liste ce qui est activé en haut à gauche quand le menu est fermé.", "Lists what is switched on in the top-left corner while the menu is closed."), ShowHud.Value);

            ui.Section(L.T("Remise à zéro", "Reset"));
            if (ui.Buttons("set.reset", false, new Btn(L.T("Tout désactiver", "Turn everything off"), ButtonKind.Danger)) == 0)
            {
                Plugin.ResetAll();
                Toasts.Show(L.T("Toutes les options sont désactivées.", "Every option is switched off."));
            }
            ui.Note(L.T("Coupe toutes les options du trainer et rend au jeu ses valeurs d'origine. L'argent et les améliorations déjà donnés restent acquis.",
                "Switches off every trainer option and gives the game back its original values. Money and upgrades already given stay."));

            ui.Section(L.T("À propos", "About"));
            ui.Stat("R.E.P.O Trainer", "v" + Plugin.Version + " by Zenato", Theme.Amber);
        }
    }
}
