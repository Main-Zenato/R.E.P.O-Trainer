using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace RepoTrainer.UI
{
    internal enum ButtonKind { Primary, Secondary, Danger }

    internal readonly struct Btn
    {
        public readonly string Label;
        public readonly ButtonKind Kind;
        public Btn(string label, ButtonKind kind = ButtonKind.Secondary) { Label = label; Kind = kind; }
        public static implicit operator Btn(string label) => new Btn(label);
    }

    /// <summary>
    /// Immediate-mode row widgets stacked top to bottom inside the scrolling content group.
    /// While a search is typed, rows that don't match are skipped entirely, and tab and
    /// section headings are only printed above the first row that survives.
    /// </summary>
    internal sealed class Ui
    {
        public static string HostOnlyMessage => L.T("Réservé à l'hôte de la partie.", "Only the host can do this.");

        public float Y;
        public float Width;
        public Rect Clip;            // visible region, in group coordinates
        public bool Interactive;     // false while the window is animating closed

        private readonly Action<string> _toast;
        private string _filter;      // normalised search text, null when not searching
        private string _context;     // current section title, searched along with each row
        private string _pendingTab, _pendingSection;

        public Ui(Action<string> toast) { _toast = toast; }

        private static Event E => Event.current;

        public bool Searching => _filter != null;

        /// <summary>lockFrom value for a list row where every button is available.</summary>
        public const int Open = int.MaxValue;

        public void SetFilter(string text) => _filter = string.IsNullOrWhiteSpace(text) ? null : Normalize(text.Trim());

        // ---- Search ------------------------------------------------------------------------

        private static readonly Dictionary<string, string> NormCache = new Dictionary<string, string>();

        private static string Normalize(string s)
        {
            if (s == null) return "";
            if (NormCache.TryGetValue(s, out var n)) return n;
            var d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (char c in d)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            n = sb.ToString();
            if (NormCache.Count > 4000) NormCache.Clear();
            NormCache[s] = n;
            return n;
        }

        /// <summary>Matches at the start of a word, so "argent" doesn't find "déchargent".</summary>
        private bool Has(string s)
        {
            if (s == null) return false;
            var n = Normalize(s);
            for (int i = n.IndexOf(_filter, StringComparison.Ordinal); i >= 0; i = n.IndexOf(_filter, i + 1, StringComparison.Ordinal))
                if (i == 0 || !char.IsLetterOrDigit(n[i - 1])) return true;
            return false;
        }

        /// <summary>True when the row should be skipped because it doesn't match the search.</summary>
        private bool Hidden(string a, string b = null, string c = null)
        {
            if (_filter == null) return false;
            if (Has(_context) || Has(a) || Has(b) || Has(c)) { EmitHeadings(); return false; }
            return true;
        }

        private bool HiddenButtons(Btn[] buttons, string extra = null)
        {
            if (_filter == null) return false;
            bool any = Has(_context) || Has(extra);
            for (int i = 0; !any && i < buttons.Length; i++) any = Has(buttons[i].Label);
            if (any) { EmitHeadings(); return false; }
            return true;
        }

        /// <summary>Called by the menu before drawing each tab during a search.</summary>
        public void BeginTab(string title)
        {
            _pendingTab = _filter != null ? title : null;
            _pendingSection = null;
            _context = null;
        }

        private void EmitHeadings()
        {
            if (_pendingTab != null)
            {
                Y += Y > 0 ? 14 : 0;
                var r = new Rect(0, Y, Width, 26);
                if (Visible(r))
                {
                    Theme.Label(r, _pendingTab, 18, Theme.Text, true);
                    Theme.Line(new Rect(0, r.yMax + 1, Width, 1), Theme.Seam);
                }
                Y += 30;
                _pendingTab = null;
            }
            if (_pendingSection != null)
            {
                DrawSection(_pendingSection);
                _pendingSection = null;
            }
        }

        // ---- Hit testing -------------------------------------------------------------------

        private bool Hover(Rect r) => Interactive && Clip.Contains(E.mousePosition) && r.Contains(E.mousePosition);

        private bool Clicked(Rect r)
        {
            if (!Interactive || E.type != EventType.MouseDown || E.button != 0) return false;
            if (!Clip.Contains(E.mousePosition) || !r.Contains(E.mousePosition)) return false;
            E.Use();
            return true;
        }

        private bool Visible(Rect r) => r.yMax >= Clip.yMin - 4 && r.yMin <= Clip.yMax + 4;

        // ---- Structure ---------------------------------------------------------------------

        public void Section(string title)
        {
            _context = title;
            if (_filter != null) { _pendingSection = title; return; }
            DrawSection(title);
        }

        private void DrawSection(string title)
        {
            Y += Y > 0 ? 20 : 2;
            var r = new Rect(0, Y, Width, 20);
            if (Visible(r))
            {
                Theme.Fill(new Rect(0, r.center.y - 4, 3, 8), Theme.Amber, 1);
                Theme.Label(new Rect(12, r.y, Width - 12, r.height), title, 13, Theme.Amber, true);
            }
            Y += 26;
        }

        public void Note(string text, Color? color = null)
        {
            if (_filter != null) return;
            float h = Theme.MeasureHeight(text, 13, Width - 4) + 4;
            var r = new Rect(2, Y, Width - 4, h);
            if (Visible(r)) Theme.Label(r, text, 13, color ?? Theme.Dim, false, TextAnchor.UpperLeft, true);
            Y += h + 6;
        }

        public void Space(float h) { if (_filter == null) Y += h; }

        private void RowChrome(string id, Rect r, bool hover)
        {
            float hv = Anim.To(id + ".hover", hover ? 1f : 0f, 18f);
            if (hv > 0.01f) Theme.Fill(new Rect(r.x - 10, r.y + 1, r.width + 20, r.height - 2), Theme.Rail.WithAlpha(hv), 6);
            Theme.Line(new Rect(r.x, r.yMax - 1, r.width, 1), Theme.Seam.WithAlpha(0.6f));
        }

        private static void LockBadge(Rect labelRect, string label, int size)
        {
            float w = Theme.Measure(label, size, true).x;
            var badge = new Rect(labelRect.x + w + 10, labelRect.y + labelRect.height / 2f - 9, 44, 18);
            Theme.Fill(badge, Theme.Seam, 4);
            Theme.Label(badge, L.T("hôte", "host"), 11, Theme.Dim, true, TextAnchor.MiddleCenter);
        }

        // ---- Toggle ------------------------------------------------------------------------

        public bool Toggle(string id, string label, string desc, bool value, bool locked = false)
        {
            if (Hidden(label, desc)) return value;
            float h = desc == null ? 42 : 54;
            var r = new Rect(0, Y, Width, h);
            Y += h;
            bool hover = Hover(r);

            if (Clicked(r))
            {
                if (locked) _toast?.Invoke(HostOnlyMessage);
                else value = !value;
            }
            if (!Visible(r)) { Anim.Snap(id + ".knob", value ? 1 : 0); return value; }

            RowChrome(id, r, hover);
            var prev = GUI.color;
            if (locked) GUI.color = prev.WithAlpha(0.45f);

            var labelRect = new Rect(r.x + 2, r.y + (desc == null ? 0 : 8), r.width - 80, desc == null ? h : 20);
            Theme.Label(labelRect, label, 15, Theme.Text, true);
            if (desc != null) Theme.Label(new Rect(r.x + 2, r.y + 28, r.width - 80, 18), desc, 12, Theme.Dim);
            if (locked) LockBadge(labelRect, label, 15);

            // A squared industrial switch: the knob slides and the track lights up amber.
            float k = Anim.To(id + ".knob", value ? 1f : 0f, 16f);
            var track = new Rect(r.xMax - 44, r.y + h / 2f - 11, 42, 22);
            Theme.Fill(track, Color.Lerp(Theme.Seam, Theme.Amber, k), 5);
            if (k > 0.01f) Theme.Fill(new Rect(track.x - 3, track.y - 3, track.width + 6, track.height + 6), Theme.Amber.WithAlpha(0.16f * k), 7);
            var knob = new Rect(track.x + 3 + k * 20, track.y + 3, 16, 16);
            Theme.Fill(knob, Color.Lerp(Theme.Dim, Theme.Glass, k), 3);

            GUI.color = prev;
            return value;
        }

        // ---- Slider ------------------------------------------------------------------------

        private int _sliderControl;

        /// <summary>A segmented bar, like the haul meter on an extraction point.</summary>
        public float Slider(string id, string label, float value, float min, float max,
            Func<float, string> format, bool locked = false, float step = 0f, string desc = null)
        {
            if (Hidden(label, desc)) return value;
            float h = desc == null ? 58 : 74;
            var r = new Rect(0, Y, Width, h);
            Y += h;
            var track = new Rect(r.x + 2, r.yMax - 20, r.width - 4, 8);
            var hit = new Rect(track.x - 8, track.y - 12, track.width + 16, track.height + 24);

            int ctl = GUIUtility.GetControlID(FocusType.Passive);
            if (Interactive)
            {
                switch (E.GetTypeForControl(ctl))
                {
                    case EventType.MouseDown when E.button == 0 && Clip.Contains(E.mousePosition) && hit.Contains(E.mousePosition):
                        if (locked) { _toast?.Invoke(HostOnlyMessage); E.Use(); break; }
                        GUIUtility.hotControl = ctl;
                        _sliderControl = ctl;
                        value = ValueAt(track, min, max, step);
                        GUI.changed = true;
                        E.Use();
                        break;
                    case EventType.MouseDrag when GUIUtility.hotControl == ctl:
                        value = ValueAt(track, min, max, step);
                        GUI.changed = true;
                        E.Use();
                        break;
                    case EventType.MouseUp when GUIUtility.hotControl == ctl:
                        GUIUtility.hotControl = 0;
                        E.Use();
                        break;
                }
            }
            if (!Visible(r)) return value;

            bool active = GUIUtility.hotControl == ctl && _sliderControl == ctl;
            RowChrome(id, r, Hover(r) || active);
            var prev = GUI.color;
            if (locked) GUI.color = prev.WithAlpha(0.45f);

            var labelRect = new Rect(r.x + 2, r.y + 6, r.width - 130, 22);
            Theme.Label(labelRect, label, 15, Theme.Text, true);
            if (locked) LockBadge(labelRect, label, 15);
            if (desc != null) Theme.Label(new Rect(r.x + 2, r.y + 28, r.width - 4, 18), desc, 12, Theme.Dim);
            Theme.Label(new Rect(r.xMax - 150, r.y + 6, 148, 22), format(value), 15, Theme.Amber, true, TextAnchor.MiddleRight);

            float t = Mathf.InverseLerp(min, max, value);
            float shown = Anim.To(id + ".fill", t, active ? 40f : 18f);
            var lit = Color.Lerp(Theme.Amber, Color.white, active ? 0.15f : 0f);
            Theme.Segments(track, shown, 24, lit, Theme.Seam);

            GUI.color = prev;
            return value;
        }

        private static float ValueAt(Rect track, float min, float max, float step)
        {
            float v = Mathf.Lerp(min, max, Mathf.InverseLerp(track.xMin, track.xMax, E.mousePosition.x));
            if (step > 0f) v = Mathf.Round(v / step) * step;
            return Mathf.Clamp(v, min, max);
        }

        // ---- Buttons -----------------------------------------------------------------------

        /// <summary>Row of equal-width buttons. Returns the index clicked, or -1.</summary>
        public int Buttons(string id, bool locked, params Btn[] buttons)
        {
            if (HiddenButtons(buttons)) return -1;
            const float h = 34, gap = 8;
            int clicked = -1;
            float w = (Width - gap * (buttons.Length - 1)) / buttons.Length;
            Y += 6;
            for (int i = 0; i < buttons.Length; i++)
            {
                var r = new Rect(i * (w + gap), Y, w, h);
                if (Button(id + "." + i, r, buttons[i], locked)) clicked = i;
            }
            Y += h + 8;
            return clicked;
        }

        public bool Button(string id, Rect r, Btn b, bool locked = false)
        {
            bool hover = Hover(r);
            bool clicked = Clicked(r);
            if (clicked && locked) { _toast?.Invoke(HostOnlyMessage); clicked = false; }
            if (clicked) Anim.Snap(id + ".press", 1f);
            if (!Visible(r)) return clicked;

            float hv = Anim.To(id + ".hover", hover && !locked ? 1f : 0f, 18f);
            float press = Anim.To(id + ".press", 0f, 10f);
            Color fill, text;
            switch (b.Kind)
            {
                case ButtonKind.Primary: fill = Theme.Amber; text = Theme.Glass; break;
                case ButtonKind.Danger: fill = Theme.Alarm.WithAlpha(0.16f + 0.2f * hv); text = Theme.Alarm; break;
                default: fill = Theme.Seam; text = Theme.Text; break;
            }
            if (b.Kind != ButtonKind.Danger) fill = Color.Lerp(fill, Color.white, 0.12f * hv);
            fill = Color.Lerp(fill, Color.white, 0.25f * press);

            var prev = GUI.color;
            if (locked) GUI.color = prev.WithAlpha(0.45f);
            var draw = new Rect(r.x, r.y + press * 1.5f, r.width, r.height);
            Theme.Fill(draw, fill, 5);
            Theme.Label(draw, b.Label, 14, text, true, TextAnchor.MiddleCenter);
            GUI.color = prev;
            return clicked;
        }

        // ---- Inputs ------------------------------------------------------------------------

        public string TextField(string id, string label, string value, float fieldWidth = 200)
        {
            if (Hidden(label)) return value;
            const float h = 48;
            var r = new Rect(0, Y, Width, h);
            Y += h;
            if (!Visible(r)) return value;
            RowChrome(id, r, false);
            Theme.Label(new Rect(r.x + 2, r.y, r.width - fieldWidth - 10, h), label, 15, Theme.Text, true);
            var fr = new Rect(r.xMax - fieldWidth, r.y + 8, fieldWidth, h - 16);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = Theme.Seam;
            GUI.SetNextControlName(id);
            value = GUI.TextField(fr, value ?? "", 24, Theme.FieldStyle());
            GUI.backgroundColor = prev;
            return value;
        }

        /// <summary>Label with ‹ value › arrows on the right. Returns the new value, wrapped.</summary>
        public int Stepper(string id, string label, string desc, int value, int min, int max, Func<int, string> format = null)
        {
            if (Hidden(label, desc)) return value;
            float h = desc == null ? 46 : 56;
            var r = new Rect(0, Y, Width, h);
            Y += h;
            var left = new Rect(r.xMax - 172, r.y + h / 2f - 15, 34, 30);
            var right = new Rect(r.xMax - 34, r.y + h / 2f - 15, 34, 30);
            if (Button(id + ".l", left, "‹")) value = value <= min ? max : value - 1;
            if (Button(id + ".r", right, "›")) value = value >= max ? min : value + 1;
            if (!Visible(r)) return value;

            RowChrome(id, r, false);
            Theme.Label(new Rect(r.x + 2, r.y + (desc == null ? 0 : 9), r.width - 190, desc == null ? h : 20), label, 15, Theme.Text, true);
            if (desc != null) Theme.Label(new Rect(r.x + 2, r.y + 30, r.width - 190, 18), desc, 12, Theme.Dim);
            Theme.Label(new Rect(left.xMax, left.y, right.x - left.xMax, left.height),
                format != null ? format(value) : value.ToString(), 16, Theme.Amber, true, TextAnchor.MiddleCenter);
            return value;
        }

        public void Stat(string label, string value, Color? valueColor = null)
        {
            if (Hidden(label)) return;
            const float h = 34;
            var r = new Rect(0, Y, Width, h);
            Y += h;
            if (!Visible(r)) return;
            Theme.Line(new Rect(r.x, r.yMax - 1, r.width, 1), Theme.Seam.WithAlpha(0.6f));
            Theme.Label(new Rect(r.x + 2, r.y, r.width / 2f, h), label, 14, Theme.Dim);
            Theme.Label(new Rect(r.x + r.width / 2f, r.y, r.width / 2f - 2, h), value, 14, valueColor ?? Theme.Text, true, TextAnchor.MiddleRight);
        }

        /// <summary>Shown in place of a list when it has nothing to list.</summary>
        public void Empty(string text)
        {
            if (_filter != null) return;
            const float h = 44;
            var r = new Rect(0, Y, Width, h);
            Y += h + 4;
            if (!Visible(r)) return;
            Theme.Fill(r, Theme.Rail, 6);
            Theme.Label(new Rect(r.x + 14, r.y, r.width - 28, h), text, 13, Theme.Dim);
        }

        /// <summary>
        /// A list row with a title, a caption and right-aligned compact buttons. Buttons from
        /// index <paramref name="lockFrom"/> onwards are host-only and greyed out.
        /// <paramref name="accent"/> colours a thin marker on the left (enemy red, loot amber).
        /// </summary>
        public int ListRow(string id, string title, string caption, int lockFrom, Color? accent, params Btn[] buttons)
        {
            if (Hidden(title, caption)) return -1;
            const float h = 54, minW = 84, gap = 6;
            var r = new Rect(0, Y, Width, h);
            Y += h;
            int clicked = -1;
            // Buttons grow to fit their label, laid out right to left.
            float x = r.xMax;
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                float bw = Mathf.Max(minW, Theme.Measure(buttons[i].Label, 14, true).x + 24);
                x -= bw;
                var br = new Rect(x, r.y + 11, bw, 32);
                if (Button(id + ".b" + i, br, buttons[i], i >= lockFrom)) clicked = i;
                x -= gap;
            }
            if (!Visible(r)) return clicked;
            RowChrome(id, r, Hover(r));
            float tx = r.x + 2;
            if (accent.HasValue)
            {
                Theme.Fill(new Rect(r.x, r.y + 13, 3, h - 26), accent.Value, 1);
                tx += 12;
            }
            float tw = x - tx - 8;
            Theme.Label(new Rect(tx, r.y + 8, tw, 20), title, 15, Theme.Text, true);
            Theme.Label(new Rect(tx, r.y + 29, tw, 18), caption, 12, Theme.Dim);
            return clicked;
        }
    }
}
