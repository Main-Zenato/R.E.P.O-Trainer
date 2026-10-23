using System.Collections.Generic;
using UnityEngine;

namespace RepoTrainer.UI
{
    /// <summary>A small terminal readout in the top-left corner listing what is switched on.</summary>
    internal static class Hud
    {
        private static readonly List<string> Active = new List<string>();
        private static string _text = "";
        private static float _next;

        public static void Draw()
        {
            if (!Settings.ShowHud.Value || !Game.InGame) return;
            if (Time.unscaledTime >= _next)
            {
                // The list only changes when an option does: rebuild it four times a second.
                _next = Time.unscaledTime + 0.25f;
                Active.Clear();
                Menu.CollectAllActive(Active);
                _text = string.Join("  ·  ", Active);
            }
            if (Active.Count == 0) return;

            var size = Theme.Measure(_text, 12);
            var r = new Rect(16, 16, Mathf.Min(size.x + 30, Screen.width * 0.6f), 26);
            Theme.Fill(r, Theme.Glass.WithAlpha(0.82f), 5);
            Theme.Scanlines(r, 0.2f);
            Theme.Fill(new Rect(r.x + 10, r.center.y - 3, 6, 6), Theme.Amber, 1);
            Theme.Label(new Rect(r.x + 22, r.y, r.width - 28, r.height), _text, 12, Theme.Text);
        }
    }
}
