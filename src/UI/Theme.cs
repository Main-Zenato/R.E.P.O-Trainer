using System.Collections.Generic;
using UnityEngine;

namespace RepoTrainer.UI
{
    /// <summary>
    /// Palette, typography and runtime-generated textures. The window reads as the truck's
    /// amber CRT terminal: warm dark glass, phosphor text, faint scanlines. Everything is drawn
    /// with white textures tinted through GUI.color so one texture serves every colour.
    /// </summary>
    internal static class Theme
    {
        public static readonly Color Glass = Hex(0x13100E);   // window background
        public static readonly Color Rail = Hex(0x1C1815);    // header band, sidebar, raised surfaces
        public static readonly Color Seam = Hex(0x3A3029);    // hairlines, tracks, inputs
        public static readonly Color Amber = Hex(0xFFA62B);   // accent: terminal phosphor
        public static readonly Color Haul = Hex(0x63DB7E);    // health and "goal reached" green
        public static readonly Color Alarm = Hex(0xFF5141);   // enemies and destructive actions
        public static readonly Color Text = Hex(0xF2E6D5);    // primary text
        public static readonly Color Dim = Hex(0x8F8172);     // secondary text
        public static readonly Color Signal = Hex(0x74C7F0);  // items on the ESP only

        public static Font Font;
        public static Texture2D White;
        public static Texture2D Scan;
        public static Texture2D Glow;

        private static readonly Dictionary<int, GUIStyle> Rounded = new Dictionary<int, GUIStyle>();
        private static readonly Dictionary<int, GUIStyle> Styles = new Dictionary<int, GUIStyle>();
        private static readonly GUIContent Content = new GUIContent();
        private static GUIStyle _field;
        private static bool _ready;

        public static void Ensure()
        {
            if (_ready && White != null) return;
            _ready = true;
            Rounded.Clear();
            Styles.Clear();
            _field = null;

            // Bahnschrift is a variable font and Unity squeezes its glyphs together; Segoe UI renders cleanly.
            Font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 14);
            White = NewTex(2, 2);
            White.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            White.Apply();
            Scan = BuildScanlines();
            Glow = BuildGlow();
        }

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        private static bool Repaint => Event.current.type == EventType.Repaint;

        // ---- Drawing primitives -------------------------------------------------------------

        public static void Fill(Rect r, Color c, int radius = 0)
        {
            if (!Repaint) return;
            var prev = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
            if (radius <= 0) GUI.DrawTexture(r, White);
            else RoundedStyle(Mathf.Min(radius, (int)(Mathf.Min(r.width, r.height) / 2f))).Draw(r, false, false, false, false);
            GUI.color = prev;
        }

        public static void Line(Rect r, Color c) => Fill(r, c);

        /// <summary>CRT scanlines tiled over a rect.</summary>
        public static void Scanlines(Rect r, float alpha)
        {
            if (!Repaint) return;
            var prev = GUI.color;
            GUI.color = new Color(0, 0, 0, alpha * prev.a);
            GUI.DrawTextureWithTexCoords(r, Scan, new Rect(0, 0, 1, r.height / Scan.height));
            GUI.color = prev;
        }

        /// <summary>Vertical fade from the colour at the top to transparent at the bottom.</summary>
        public static void GlowDown(Rect r, Color c)
        {
            if (!Repaint) return;
            var prev = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
            GUI.DrawTexture(r, Glow);
            GUI.color = prev;
        }

        /// <summary>A bar split into equal cells, the first <paramref name="t"/> share of them lit.</summary>
        public static void Segments(Rect r, float t, int cells, Color lit, Color unlit, float gap = 2f)
        {
            if (!Repaint) return;
            float w = (r.width - gap * (cells - 1)) / cells;
            float filled = Mathf.Clamp01(t) * cells;
            for (int i = 0; i < cells; i++)
            {
                var cell = new Rect(r.x + i * (w + gap), r.y, w, r.height);
                Fill(cell, unlit, 1);
                float part = Mathf.Clamp01(filled - i);
                if (part > 0f) Fill(new Rect(cell.x, cell.y, cell.width * part, cell.height), lit, 1);
            }
        }

        public static void Label(Rect r, string text, int size, Color c, bool bold = false,
            TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false)
        {
            if (!Repaint) return;
            var s = Style(size, bold, align, wrap);
            s.normal.textColor = c;
            Content.text = text;
            s.Draw(r, Content, false, false, false, false);
        }

        public static Vector2 Measure(string text, int size, bool bold = false)
        {
            Content.text = text;
            return Style(size, bold, TextAnchor.MiddleLeft, false).CalcSize(Content);
        }

        public static float MeasureHeight(string text, int size, float width)
        {
            Content.text = text;
            return Style(size, false, TextAnchor.UpperLeft, true).CalcHeight(Content, width);
        }

        public static GUIStyle Style(int size, bool bold, TextAnchor align, bool wrap)
        {
            int key = size * 1000 + (bold ? 500 : 0) + (int)align * 10 + (wrap ? 1 : 0);
            if (!Styles.TryGetValue(key, out var s))
            {
                s = new GUIStyle
                {
                    font = Font,
                    fontSize = size,
                    fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                    alignment = align,
                    wordWrap = wrap,
                    clipping = TextClipping.Clip,
                    richText = false,
                    padding = new RectOffset(0, 0, 0, 0),
                };
                Styles[key] = s;
            }
            return s;
        }

        public static GUIStyle FieldStyle()
        {
            if (_field != null) return _field;
            var bg = RoundedTexture(5);
            _field = new GUIStyle
            {
                font = Font,
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 12, 0, 0),
                border = new RectOffset(6, 6, 6, 6),
                clipping = TextClipping.Clip,
            };
            _field.normal.background = bg;
            _field.hover.background = bg;
            _field.focused.background = bg;
            _field.normal.textColor = Text;
            _field.hover.textColor = Text;
            _field.focused.textColor = Text;
            // Caret and selection colours live on the skin, not the style. FieldStyle runs inside OnGUI.
            GUI.skin.settings.cursorColor = Amber;
            GUI.skin.settings.selectionColor = Amber.WithAlpha(0.35f);
            return _field;
        }

        // ---- Texture generation -------------------------------------------------------------

        private static GUIStyle RoundedStyle(int radius)
        {
            radius = Mathf.Clamp(radius, 1, 24);
            if (!Rounded.TryGetValue(radius, out var s))
            {
                s = new GUIStyle
                {
                    normal = { background = RoundedTexture(radius) },
                    border = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1),
                };
                Rounded[radius] = s;
            }
            return s;
        }

        private static Texture2D RoundedTexture(int r)
        {
            int size = r * 2 + 4;
            var t = NewTex(size, size);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, r, size - r);
                float cy = Mathf.Clamp(fy, r, size - r);
                float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
                px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d + 0.5f));
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        /// <summary>One dark line every three pixels, tinted black when drawn.</summary>
        private static Texture2D BuildScanlines()
        {
            var t = NewTex(1, 3);
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Repeat;
            t.SetPixels(new[] { new Color(1, 1, 1, 1f), new Color(1, 1, 1, 0f), new Color(1, 1, 1, 0f) });
            t.Apply();
            return t;
        }

        private static Texture2D BuildGlow()
        {
            const int h = 64;
            var t = NewTex(1, h);
            var px = new Color[h];
            // Texture rows run bottom to top; the top row is the brightest.
            for (int y = 0; y < h; y++)
            {
                float k = y / (h - 1f);
                px[y] = new Color(1, 1, 1, k * k);
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        private static Texture2D NewTex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
    }
}
