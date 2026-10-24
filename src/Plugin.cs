using BepInEx;
using HarmonyLib;
using RepoTrainer.Features;
using RepoTrainer.UI;
using UnityEngine;

namespace RepoTrainer
{
    [BepInPlugin(Guid, "R.E.P.O Trainer", Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "zen.repo.trainer";
        public const string Version = "1.0.2";

        private Harmony _harmony;

        private void Awake()
        {
            Settings.Bind(Config);
            Menu.Init();
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo($"R.E.P.O Trainer {Version} by Zenato loaded. Menu key: {Settings.MenuKey.Value}");
#if SELFTEST
            SelfTest.Start(gameObject, Logger);
#endif
        }

        private void OnDestroy()
        {
            ResetAll();
            Menu.Close();
            _harmony?.UnpatchSelf();
        }

        public static void ResetAll()
        {
            Self.Reset();
            Vision.Reset();
            Enemies.Reset();
            Loot.Reset();
            Team.Reset();
        }

        private void Update()
        {
            if (!Settings.Rebinding && !Game.Typing)
            {
                if (Input.GetKeyDown(Settings.MenuKey.Value)) Menu.Toggle();
                if (Game.InGame && !Menu.IsOpen && GUIUtility.keyboardControl == 0) Hotkeys();
            }

            Scan.Tick();
            Self.Tick();
            Enemies.Tick();
            Loot.Tick();
            Team.Tick();
            Teleport.Tick();
        }

        private static void Hotkeys()
        {
            if (Input.GetKeyDown(Settings.NoclipKey.Value))
            {
                Self.Noclip = !Self.Noclip;
                Toasts.Show(Self.Noclip ? L.T("Noclip activé.", "Noclip on.") : L.T("Noclip désactivé.", "Noclip off."));
            }
            if (Input.GetKeyDown(Settings.CrosshairKey.Value)) Teleport.ToCrosshair();
            if (Input.GetKeyDown(Settings.VisionKey.Value))
            {
                bool on = !(Vision.Enemies && Vision.Valuables);
                Vision.Enemies = Vision.Valuables = on;
                Toasts.Show(on ? L.T("Ennemis et objets visibles à travers les murs.", "Enemies and valuables visible through walls.")
                    : L.T("Vision à travers les murs coupée.", "Wall vision off."));
            }
        }

        private void LateUpdate()
        {
            Self.LateTick();
            Menu.Enforce();
        }

        private void OnGUI()
        {
            Settings.CaptureKey();
            GUI.depth = -1000;
            Menu.OnGUI();
        }
    }
}
