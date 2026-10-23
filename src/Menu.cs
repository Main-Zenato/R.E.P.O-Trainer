using System;
using System.Collections.Generic;
using RepoTrainer.Features;
using RepoTrainer.UI;
using UnityEngine;

namespace RepoTrainer
{
    /// <summary>
    /// The trainer window, styled as the truck's amber terminal. It powers on like a CRT,
    /// keeps live vitals and the "by Zenato" credit in the header, and has a search box that
    /// filters options across every tab at once.
    /// </summary>
    internal static class Menu
    {
        private sealed class Tab
        {
            public string TitleFr, TitleEn, SubtitleFr, SubtitleEn;
            public Action<Ui> Draw;
            public Action<List<string>> Active;
            public float ScrollTarget;
            public int ActiveCount;
            public string Title => L.T(TitleFr, TitleEn);
            public string Subtitle => L.T(SubtitleFr, SubtitleEn);
        }

        private const float W = 920, H = 600, Header = 78, Side = 196, TabH = 40;
        private const string SearchControl = "repo.search";

        public static bool IsOpen { get; private set; }

        private static readonly List<Tab> Tabs = new List<Tab>();
        private static readonly Ui Ui = new Ui(Toasts.Show);
        private static readonly List<string> Scratch = new List<string>();
        private static readonly List<InputKey> NoKeys = new List<InputKey>();
        private static int _tab;
        private static string _search = "";
        private static float _searchScroll;
        private static Vector2 _pos = new Vector2(-1, -1);
        private static bool _dragging;
        private static Vector2 _dragOffset;
        private static CursorLockMode _prevLock;
        private static bool _prevVisible;
        private static float _nextCount;

        public static void Init()
        {
            Add("Joueur", "Player", "Survie, déplacements, noclip, saisie et caméra. Marche dans toutes les parties.",
                "Survival, movement, noclip, grabbing and camera. Works in every game.", Self.Draw, Self.CollectActive);
            Add("Vision", "Vision", "Voir à travers les murs, pleine lumière et brouillard. Visible par toi seul.",
                "See through walls, fullbright and fog. Only you can see it.", Vision.Draw, Vision.CollectActive);
            Add("Ennemis", "Enemies", "Les monstres du niveau. L'hôte décide de leur sort.",
                "The level's monsters. The host decides their fate.", Enemies.Draw, Enemies.CollectActive);
            Add("Objets", "Loot", "Objets de valeur, extraction et batteries.",
                "Valuables, extraction and batteries.", Loot.Draw, Loot.CollectActive);
            Add("Apparition", "Spawn", "Fais apparaître objets, armes, objets de valeur et ennemis devant toi.",
                "Spawn items, weapons, valuables and enemies in front of you.", Spawner.Draw, Spawner.CollectActive);
            Add("Partie", "Run", "Argent, améliorations et passage au niveau suivant.",
                "Money, upgrades and moving on to the next level.", Run.Draw, Run.CollectActive);
            Add("Équipe", "Team", "Les joueurs présents : rejoindre, soigner, amener, ranimer, expulser.",
                "The players in the room: reach, heal, bring over, revive, kick.", Team.Draw, Team.CollectActive);
            Add("Téléportation", "Teleport", "Viseur, camion, extraction et emplacements enregistrés.",
                "Crosshair, truck, extraction and saved spots.", Teleport.Draw, Teleport.CollectActive);
            Add("Cosmétiques", "Cosmetics", "Jetons et déblocages, enregistrés dans ta sauvegarde de cosmétiques.",
                "Tokens and unlocks, saved in your cosmetics save.", Cosmetics.Draw, Cosmetics.CollectActive);
            Add("Réglages", "Settings", "Langue, touches, taille de l'interface et notifications.",
                "Language, keys, interface size and notifications.", Settings.Draw, null);
        }

        private static void Add(string titleFr, string titleEn, string subFr, string subEn, Action<Ui> draw, Action<List<string>> active) =>
            Tabs.Add(new Tab { TitleFr = titleFr, TitleEn = titleEn, SubtitleFr = subFr, SubtitleEn = subEn, Draw = draw, Active = active });

        public static int TabCount => Tabs.Count;

        /// <summary>Selects a tab, search text and scroll position programmatically (used by the self-test build).</summary>
        internal static void Select(int tab, string search, float scroll = 0f)
        {
            _tab = Mathf.Clamp(tab, 0, Tabs.Count - 1);
            _search = search ?? "";
            Tabs[_tab].ScrollTarget = scroll;
            Anim.Snap("menu.scroll" + _tab, scroll);
        }

        public static void CollectAllActive(List<string> list)
        {
            foreach (var t in Tabs) t.Active?.Invoke(list);
        }

        // ---- Open / close ------------------------------------------------------------------

        public static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public static void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            _prevLock = Cursor.lockState;
            _prevVisible = Cursor.visible;
        }

        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _dragging = false;
            GUIUtility.keyboardControl = 0;
            Settings.CancelRebind();
            Cursor.lockState = _prevLock;
            Cursor.visible = _prevVisible;
        }

        /// <summary>Called every LateUpdate: freezes the robot and frees the cursor while open.</summary>
        public static void Enforce()
        {
            if (!IsOpen) return;
            var input = InputManager.instance;
            if (input)
            {
                input.DisableMovement(0.2f);
                input.DisableAiming(0.2f);
                input.DisableControlsExcept(0.2f, NoKeys);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static float Scale => Mathf.Clamp(Settings.UiScale.Value * Screen.height / 1080f, 0.6f, 2.5f);

        // ---- Frame -------------------------------------------------------------------------

        public static void OnGUI()
        {
            Theme.Ensure();
            float scale = Scale;
            float sw = Screen.width / scale, sh = Screen.height / scale;
            var baseMatrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            GUI.matrix = baseMatrix;

            Vision.DrawEsp(sw, sh);
            if (!IsOpen) Hud.Draw();
            HandleKeys();

            float open = Anim.To("menu.open", IsOpen ? 1f : 0f, IsOpen ? 11f : 20f);
            if (open > 0.004f)
            {
                Enforce();
                if (_pos.x < 0) _pos = new Vector2((sw - W) / 2f, (sh - H) / 2f);
                _pos.x = Mathf.Clamp(_pos.x, 60 - W, sw - 60);
                _pos.y = Mathf.Clamp(_pos.y, 0, sh - 60);
                var win = new Rect(_pos.x, _pos.y, W, H);

                // CRT power-on: a bright line that opens vertically into the full screen.
                float v = Mathf.Clamp01(open * 1.35f);
                float sy = Mathf.Lerp(0.015f, 1f, 1f - Mathf.Pow(1f - v, 3f));
                float sx = Mathf.Lerp(0.92f, 1f, open);
                var c = new Vector3(win.center.x, win.center.y, 0);
                GUI.matrix = baseMatrix * Matrix4x4.TRS(c, Quaternion.identity, new Vector3(sx, sy, 1)) * Matrix4x4.TRS(-c, Quaternion.identity, Vector3.one);
                GUI.color = new Color(1, 1, 1, Mathf.Clamp01(open * 3f));
                DrawWindow(win);
                float flash = Mathf.Clamp01(1f - Mathf.Abs(open - 0.35f) / 0.3f) * (IsOpen ? 1f : 0.5f);
                if (flash > 0.01f) Theme.Fill(win, Theme.Amber.WithAlpha(0.22f * flash), 8);
                GUI.color = Color.white;
            }

            GUI.matrix = baseMatrix;
            Toasts.Draw(sw, sh);
            GUI.matrix = Matrix4x4.identity;
        }

        private static void HandleKeys()
        {
            var e = Event.current;
            if (!IsOpen || Settings.Rebinding || e.type != EventType.KeyDown || e.keyCode != KeyCode.Escape) return;
            if (_search.Length > 0) { _search = ""; GUIUtility.keyboardControl = 0; }
            else Close();
            e.Use();
        }

        private static void DrawWindow(Rect win)
        {
            var e = Event.current;

            for (int i = 3; i >= 1; i--)
                Theme.Fill(new Rect(win.x - i * 5, win.y - i * 3, win.width + i * 10, win.height + i * 10), new Color(0, 0, 0, 0.12f), 12 + i * 2);
            Theme.Fill(win, Theme.Glass, 9);

            GUI.BeginGroup(win);
            var local = new Rect(0, 0, win.width, win.height);
            if (IsOpen && e.type == EventType.MouseDown && local.Contains(e.mousePosition) && e.button != 0) e.Use();

            CountActive();
            DrawSidebar(e);
            DrawContent(e);
            DrawHeader(e);

            Theme.Scanlines(local, 0.08f);
            Theme.Line(new Rect(0, 0, W, 1), Theme.Amber.WithAlpha(0.35f));

            // Swallow clicks on empty window space so they never reach the game.
            if (IsOpen && (e.type == EventType.MouseDown || e.type == EventType.ScrollWheel) && local.Contains(e.mousePosition)) e.Use();
            GUI.EndGroup();
        }

        private static void CountActive()
        {
            if (Time.unscaledTime < _nextCount) return;
            _nextCount = Time.unscaledTime + 0.25f;
            foreach (var t in Tabs)
            {
                Scratch.Clear();
                t.Active?.Invoke(Scratch);
                t.ActiveCount = Scratch.Count;
            }
        }

        // ---- Header ------------------------------------------------------------------------

        private static void DrawHeader(Event e)
        {
            var band = new Rect(0, 0, W, Header);
            Theme.Fill(band, Theme.Rail, 9);
            Theme.Fill(new Rect(0, Header - 10, W, 10), Theme.Rail);
            Theme.GlowDown(new Rect(0, 0, W, Header), Theme.Amber.WithAlpha(0.07f));
            Theme.Line(new Rect(0, Header - 1, W, 1), Theme.Seam);

            Theme.Label(new Rect(24, 13, 260, 30), "R.E.P.O Trainer", 24, Theme.Text, true);
            Theme.Label(new Rect(25, 44, 200, 18), "by Zenato", 13, Theme.Amber, true);

            if (Game.InGame) DrawVitals(new Rect(262, 14, 380, 50));

            var field = new Rect(W - 262, 22, 238, 34);
            DrawSearch(field, e);

            var drag = new Rect(0, 0, field.x - 8, Header);
            if (IsOpen && e.type == EventType.MouseDown && e.button == 0 && drag.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragOffset = e.mousePosition;
                GUIUtility.keyboardControl = 0;
                e.Use();
            }
            if (_dragging && e.type == EventType.MouseDrag)
            {
                _pos += e.mousePosition - _dragOffset;
                e.Use();
            }
            if (_dragging && e.rawType == EventType.MouseUp) _dragging = false;
        }

        /// <summary>Health, stamina and money, the three numbers the game's own HUD shows.</summary>
        private static void DrawVitals(Rect r)
        {
            var me = Game.Me;
            var pc = Game.Controller;
            float colW = r.width / 3f;

            var hp = me ? me.playerHealth : null;
            if (hp) Vital(new Rect(r.x, r.y, colW - 18, r.height), L.T("Santé", "Health"), hp.health + "/" + hp.maxHealth,
                hp.maxHealth > 0 ? hp.health / (float)hp.maxHealth : 0f, Theme.Haul);
            if (pc) Vital(new Rect(r.x + colW, r.y, colW - 18, r.height), L.T("Endurance", "Stamina"), pc.EnergyCurrent.ToString("0"),
                pc.EnergyStart > 0 ? pc.EnergyCurrent / pc.EnergyStart : 0f, Theme.Amber);
            Vital(new Rect(r.x + colW * 2, r.y, colW - 18, r.height), L.T("Caisse", "Bank"), Game.Money(Run.Currency), -1f, Theme.Amber);
        }

        private static void Vital(Rect r, string label, string value, float fill, Color color)
        {
            Theme.Label(new Rect(r.x, r.y, r.width, 16), label, 11, Theme.Dim);
            Theme.Label(new Rect(r.x, r.y + 15, r.width, 22), value, 17, color, true);
            if (fill >= 0f) Theme.Segments(new Rect(r.x, r.y + 40, r.width, 5), Anim.To("vital." + label, fill, 10f), 10, color, Theme.Seam);
        }

        private static void DrawSearch(Rect field, Event e)
        {
            bool focused = GUI.GetNameOfFocusedControl() == SearchControl;
            Theme.Fill(field, focused ? Theme.Seam : Theme.Glass, 6);
            if (focused) Theme.Fill(new Rect(field.x + 8, field.yMax - 2, field.width - 16, 2), Theme.Amber, 1);

            var style = Theme.FieldStyle();
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = Color.clear;
            GUI.SetNextControlName(SearchControl);
            string next = IsOpen ? GUI.TextField(field, _search, 32, style) : _search;
            GUI.backgroundColor = prevBg;
            if (next != _search) { _search = next; _searchScroll = 0f; Anim.Snap("menu.scroll.search", 0f); }

            if (_search.Length == 0 && !focused)
                Theme.Label(new Rect(field.x + 12, field.y, field.width - 24, field.height),
                    L.T("Rechercher une option…", "Search an option…"), 14, Theme.Dim);
            if (_search.Length > 0)
            {
                var clear = new Rect(field.xMax - 30, field.y + 5, 24, 24);
                bool hover = IsOpen && clear.Contains(e.mousePosition);
                Theme.Label(clear, "×", 18, hover ? Theme.Amber : Theme.Dim, true, TextAnchor.MiddleCenter);
                if (IsOpen && e.type == EventType.MouseDown && e.button == 0 && hover)
                {
                    _search = "";
                    GUIUtility.keyboardControl = 0;
                    e.Use();
                }
            }
        }

        // ---- Sidebar -----------------------------------------------------------------------

        private static void DrawSidebar(Event e)
        {
            var rail = new Rect(0, Header, Side, H - Header);
            Theme.Fill(rail, Theme.Rail, 9);
            Theme.Fill(new Rect(0, Header, Side, 12), Theme.Rail);
            Theme.Fill(new Rect(Side - 12, Header, 12, H - Header), Theme.Rail);
            Theme.Line(new Rect(Side - 1, Header, 1, H - Header), Theme.Seam);

            bool searching = _search.Length > 0;
            float top = Header + 14;
            float markY = Anim.To("menu.mark", top + _tab * TabH, 16f);
            if (!searching)
            {
                Theme.Fill(new Rect(10, markY + 2, Side - 22, TabH - 4), Theme.Glass.WithAlpha(0.7f), 5);
                Theme.Fill(new Rect(10, markY + 9, 3, TabH - 18), Theme.Amber, 1);
            }

            for (int i = 0; i < Tabs.Count; i++)
            {
                var r = new Rect(10, top + i * TabH, Side - 22, TabH);
                bool hover = IsOpen && r.Contains(e.mousePosition);
                if (IsOpen && e.type == EventType.MouseDown && e.button == 0 && hover)
                {
                    if (_tab != i || searching) { _tab = i; Anim.Snap("menu.fade", 0f); }
                    if (searching) { _search = ""; GUIUtility.keyboardControl = 0; }
                    e.Use();
                }
                bool sel = i == _tab && !searching;
                float hv = Anim.To("menu.tabhover" + i, hover && !sel ? 1f : 0f, 18f);
                var col = sel ? Theme.Text : Color.Lerp(Theme.Dim, Theme.Text, hv);
                Theme.Label(new Rect(r.x + 18 + hv * 3, r.y, r.width - 56, r.height), Tabs[i].Title, 15, col, sel);

                int n = Tabs[i].ActiveCount;
                if (n > 0)
                {
                    string s = n.ToString();
                    float bw = Mathf.Max(20, Theme.Measure(s, 11, true).x + 10);
                    var badge = new Rect(r.xMax - bw - 8, r.center.y - 9, bw, 18);
                    Theme.Fill(badge, Theme.Amber.WithAlpha(sel ? 1f : 0.85f), 4);
                    Theme.Label(badge, s, 11, Theme.Glass, true, TextAnchor.MiddleCenter);
                }
            }

            // Session status at the foot of the rail.
            bool inGame = Game.InGame, host = Game.IsHost;
            var dot = !inGame ? Theme.Dim : host ? Theme.Haul : Theme.Amber;
            float fy = H - 92;
            Theme.Line(new Rect(22, fy, Side - 44, 1), Theme.Seam);
            Theme.Fill(new Rect(22, fy + 21, 8, 8), dot, 2);
            string players = inGame && Game.Multiplayer ? "  ·  " + Game.PlayerCount + L.T(" joueurs", " players") : "";
            Theme.Label(new Rect(38, fy + 14, Side - 50, 22), Game.SessionLabel + players, 14, Theme.Text, true);
            Theme.Label(new Rect(22, fy + 36, Side - 34, 18),
                !inGame ? L.T("Lance une partie", "Start a game")
                : host ? L.T("Toutes les options", "Every option available")
                : L.T("Options d'hôte grisées", "Host options greyed out"), 12, Theme.Dim);
            Theme.Label(new Rect(22, fy + 58, Side - 34, 18), Settings.MenuKey.Value + L.T(" ou Échap pour fermer", " or Esc to close"), 11, Theme.Dim);
        }

        // ---- Content -----------------------------------------------------------------------

        private static void DrawContent(Event e)
        {
            bool searching = _search.Length > 0;
            var tab = Tabs[_tab];
            float x = Side + 30;

            string title = searching ? L.T("Recherche", "Search") : tab.Title;
            string subtitle = searching ? L.T($"Options qui contiennent « {_search.Trim()} », dans tous les onglets.",
                $"Options containing \"{_search.Trim()}\", across every tab.") : tab.Subtitle;
            Theme.Label(new Rect(x, Header + 18, W - x - 30, 30), title, 22, Theme.Text, true);
            Theme.Label(new Rect(x, Header + 48, W - x - 30, 18), subtitle, 13, Theme.Dim);

            var area = new Rect(x, Header + 78, W - x - 20, H - Header - 78 - 14);
            Theme.Line(new Rect(x, area.y - 8, area.width - 10, 1), Theme.Seam.WithAlpha(0.6f));

            float fade = Anim.To("menu.fade", 1f, 12f);
            string scrollId = searching ? "menu.scroll.search" : "menu.scroll" + _tab;
            float target = searching ? _searchScroll : tab.ScrollTarget;
            float scroll = Anim.To(scrollId, target, 18f);

            bool wheel = IsOpen && e.type == EventType.ScrollWheel && area.Contains(e.mousePosition);
            float wheelDelta = wheel ? e.delta.y : 0f;
            if (wheel) e.Use();

            GUI.BeginGroup(area);
            var prev = GUI.color;
            GUI.color = prev.WithAlpha(fade);
            Ui.Width = area.width - 16;
            Ui.Clip = new Rect(0, 0, area.width, area.height);
            Ui.Interactive = IsOpen;
            Ui.SetFilter(searching ? _search : null);
            float start = -scroll + (1f - fade) * 10f;
            Ui.Y = Ui.Top = start;
            if (searching)
            {
                foreach (var t in Tabs)
                {
                    Ui.BeginTab(t.Title);
                    t.Draw(Ui);
                }
                if (Ui.Y == start)
                    Theme.Label(new Rect(0, 4, area.width, 40), L.T("Aucune option ne correspond. Essaie « argent », « noclip » ou « ennemi ».",
                        "No option matches. Try \"money\", \"noclip\" or \"enemy\"."), 14, Theme.Dim);
            }
            else
            {
                Ui.BeginTab(null);
                tab.Draw(Ui);
            }
            float contentH = Ui.Y - start + 12;
            GUI.color = prev;

            float max = Mathf.Max(0, contentH - area.height);
            target = Mathf.Clamp(target + wheelDelta * 26f, 0, max);
            if (searching) _searchScroll = target;
            else tab.ScrollTarget = target;

            if (max > 0)
            {
                var track = new Rect(area.width - 4, 0, 3, area.height);
                float thumbH = Mathf.Max(30, area.height * area.height / contentH);
                float t = Mathf.Clamp01(scroll / max);
                Theme.Fill(track, Theme.Seam.WithAlpha(0.5f), 1);
                Theme.Fill(new Rect(track.x, t * (area.height - thumbH), 3, thumbH), Theme.Amber.WithAlpha(0.8f), 1);
            }
            GUI.EndGroup();
        }
    }
}
