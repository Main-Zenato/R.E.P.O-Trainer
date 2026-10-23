using BepInEx.Configuration;
using UnityEngine;

namespace RepoTrainer
{
    internal enum Language { Auto, Francais, English }

    /// <summary>Two-language strings, written inline as L.T("français", "english").</summary>
    internal static class L
    {
        public static ConfigEntry<Language> Setting;

        public static bool French => Setting == null || Setting.Value == Language.Auto
            ? Application.systemLanguage == SystemLanguage.French
            : Setting.Value == Language.Francais;

        public static string T(string fr, string en) => French ? fr : en;

        public static void Bind(ConfigFile cfg) =>
            Setting = cfg.Bind("Interface", "Langue", Language.Auto,
                "Langue du menu / Menu language. Auto suit la langue de Windows / Auto follows the Windows language.");
    }
}
