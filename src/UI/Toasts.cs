using System.Collections.Generic;
using UnityEngine;

namespace RepoTrainer.UI
{
    /// <summary>
    /// Confirmations printed bottom-right as terminal lines that type themselves out,
    /// the way the truck screen does.
    /// </summary>
    internal static class Toasts
    {
        private sealed class Toast
        {
            public string Text;
            public float Born;
            public float Y;
        }

        private const float Life = 2.8f, CharsPerSecond = 110f;
        private const int Max = 4;
        private static readonly List<Toast> Items = new List<Toast>();

        public static bool Enabled = true;

        public static void Show(string text)
        {
            if (!Enabled || string.IsNullOrEmpty(text)) return;
            if (Items.Count > 0 && Items[Items.Count - 1].Text == text)
            {
                Items[Items.Count - 1].Born = Time.unscaledTime - 0.5f; // refresh without retyping
                return;
            }
            Items.Add(new Toast { Text = text, Born = Time.unscaledTime, Y = -1 });
            if (Items.Count > Max) Items.RemoveAt(0);
        }

        public static void Draw(float screenW, float screenH)
        {
            if (Items.Count == 0) return;
            const float w = 320, pad = 14, margin = 24, gap = 8;
            float bottom = screenH - margin;
            bool repaint = Event.current.type == EventType.Repaint;

            for (int i = Items.Count - 1; i >= 0; i--)
            {
                var t = Items[i];
                float age = Time.unscaledTime - t.Born;
                if (age > Life + 0.35f) { Items.RemoveAt(i); continue; }

                float h = Theme.MeasureHeight(t.Text, 14, w - pad * 2 - 16) + pad * 2;
                float targetY = bottom - h;
                if (t.Y < 0) t.Y = targetY;
                if (repaint) t.Y = Mathf.Lerp(t.Y, targetY, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                bottom -= h + gap;

                float enter = Mathf.Clamp01(age / 0.22f);
                float exit = Mathf.Clamp01((age - Life) / 0.35f);
                float slide = (1f - Anim.EaseOutBack(enter)) * 40f + exit * 30f;
                float alpha = Mathf.Min(enter * 2f, 1f - exit);
                int shown = Mathf.Clamp((int)(age * CharsPerSecond), 0, t.Text.Length);
                string text = shown < t.Text.Length ? t.Text.Substring(0, shown) + "_" : t.Text;

                var r = new Rect(screenW - margin - w + slide, t.Y, w, h);
                var prev = GUI.color;
                GUI.color = new Color(1, 1, 1, alpha);
                Theme.Fill(new Rect(r.x + 2, r.y + 4, r.width, r.height), new Color(0, 0, 0, 0.35f), 6);
                Theme.Fill(r, Theme.Rail, 6);
                Theme.GlowDown(new Rect(r.x, r.y, r.width, Mathf.Min(r.height, 26)), Theme.Amber.WithAlpha(0.10f));
                Theme.Scanlines(r, 0.18f);
                Theme.Fill(new Rect(r.x, r.y + 8, 3, r.height - 16), Theme.Amber, 1);
                Theme.Label(new Rect(r.x + pad, r.y + pad, 14, 18), "›", 15, Theme.Amber, true, TextAnchor.UpperLeft);
                Theme.Label(new Rect(r.x + pad + 16, r.y + pad, r.width - pad * 2 - 16, r.height - pad * 2), text, 14,
                    Theme.Text, false, TextAnchor.UpperLeft, true);
                GUI.color = prev;
            }
        }
    }
}
