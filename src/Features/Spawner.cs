using System.Collections.Generic;
using Photon.Pun;
using RepoTrainer.UI;
using UnityEngine;

namespace RepoTrainer.Features
{
    /// <summary>
    /// Spawns shop items, valuables and enemies with the same calls the game uses during level
    /// generation. Network objects can only be created by the host.
    /// </summary>
    internal static class Spawner
    {
        internal sealed class Entry
        {
            public string Name;
            public PrefabRef Prefab;
            public EnemySetup Enemy;
            public int Size;
            public Item Item;
            public int Tier;
        }

        private enum Kind { Items, Valuables, Enemies }

        internal static readonly List<Entry> Items = new List<Entry>();
        internal static readonly List<Entry> Valuables = new List<Entry>();
        internal static readonly List<Entry> Enemies = new List<Entry>();
        private static bool _built;
        private static Kind _kind;
        private static int _amount = 1;

        /// <summary>Reads the catalogues once; the game's ScriptableObjects don't change during a session.</summary>
        internal static void Build()
        {
            if (_built || !StatsManager.instance || !RunManager.instance || !EnemyDirector.instance) return;
            _built = true;

            foreach (var item in StatsManager.instance.itemDictionary.Values)
                if (item && !item.disabled && item.prefab != null && !string.IsNullOrEmpty(item.prefab.ResourcePath))
                    Items.Add(new Entry { Name = item.itemName, Item = item, Prefab = item.prefab });
            Items.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            var seen = new HashSet<string>();
            foreach (var level in RunManager.instance.levels)
            {
                if (!level || level.ValuablePresets == null) continue;
                foreach (var preset in level.ValuablePresets)
                {
                    if (!preset) continue;
                    AddValuables(preset.tiny, 0, seen);
                    AddValuables(preset.small, 1, seen);
                    AddValuables(preset.medium, 2, seen);
                    AddValuables(preset.big, 3, seen);
                    AddValuables(preset.wide, 4, seen);
                    AddValuables(preset.tall, 5, seen);
                    AddValuables(preset.veryTall, 6, seen);
                }
            }
            Valuables.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            var enemies = new HashSet<EnemySetup>();
            foreach (var list in new[] { EnemyDirector.instance.enemiesDifficulty1, EnemyDirector.instance.enemiesDifficulty2, EnemyDirector.instance.enemiesDifficulty3 })
                if (list != null)
                    foreach (var e in list)
                        if (e && e.spawnObjects != null && e.spawnObjects.Count > 0 && enemies.Add(e))
                            Enemies.Add(new Entry { Name = Clean(e.name, "Enemy - ", "Enemy "), Enemy = e, Tier = Tier(e) });
            Enemies.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        }

        private static readonly string[] SizeFr = { "Minuscule", "Petit", "Moyen", "Grand", "Large", "Haut", "Très haut" };
        private static readonly string[] SizeEn = { "Tiny", "Small", "Medium", "Big", "Wide", "Tall", "Very tall" };

        private static void AddValuables(List<PrefabRef> refs, int size, HashSet<string> seen)
        {
            if (refs == null) return;
            foreach (var r in refs)
            {
                if (r == null || string.IsNullOrEmpty(r.ResourcePath) || !seen.Add(r.ResourcePath)) continue;
                Valuables.Add(new Entry { Name = Clean(r.PrefabName, "Valuable "), Prefab = r, Size = size });
            }
        }

        private static string Clean(string s, params string[] prefixes)
        {
            if (string.IsNullOrEmpty(s)) return "?";
            foreach (var p in prefixes)
                if (s.StartsWith(p)) return s.Substring(p.Length);
            return s;
        }

        private static string ItemType(Item item)
        {
            switch (item.itemType)
            {
                case SemiFunc.itemType.drone: return L.T("Drone", "Drone");
                case SemiFunc.itemType.orb: return L.T("Orbe", "Orb");
                case SemiFunc.itemType.cart: return L.T("Chariot", "Cart");
                case SemiFunc.itemType.pocket_cart: return L.T("Chariot de poche", "Pocket cart");
                case SemiFunc.itemType.item_upgrade: return L.T("Amélioration d'objet", "Item upgrade");
                case SemiFunc.itemType.player_upgrade: return L.T("Amélioration du joueur", "Player upgrade");
                case SemiFunc.itemType.power_crystal: return L.T("Cristal d'énergie", "Power crystal");
                case SemiFunc.itemType.grenade: return L.T("Grenade", "Grenade");
                case SemiFunc.itemType.melee: return L.T("Arme de mêlée", "Melee weapon");
                case SemiFunc.itemType.healthPack: return L.T("Soin", "Health pack");
                case SemiFunc.itemType.gun: return L.T("Arme à feu", "Gun");
                case SemiFunc.itemType.tracker: return L.T("Traceur", "Tracker");
                case SemiFunc.itemType.mine: return L.T("Mine", "Mine");
                case SemiFunc.itemType.tool: return L.T("Outil", "Tool");
                case SemiFunc.itemType.vehicle: return L.T("Véhicule", "Vehicle");
                case SemiFunc.itemType.launcher: return L.T("Lanceur", "Launcher");
                default: return item.itemType.ToString();
            }
        }

        private static int Tier(EnemySetup e)
        {
            var d = EnemyDirector.instance;
            int tier = d.enemiesDifficulty1.Contains(e) ? 1 : d.enemiesDifficulty2.Contains(e) ? 2 : 3;
            return tier;
        }

        // ---- Spawning ----------------------------------------------------------------------

        private static GameObject Instantiate(PrefabRef prefab, Vector3 pos, Quaternion rot) =>
            Game.Multiplayer
                ? PhotonNetwork.InstantiateRoomObject(prefab.ResourcePath, pos, rot, 0)
                : Object.Instantiate(prefab.Prefab, pos, rot);

        /// <summary>Spreads several copies side by side in front of the player.</summary>
        private static Vector3 Slot(int i, int count)
        {
            var cam = Game.Camera;
            var fwd = cam.transform.forward; fwd.y = 0; fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            float offset = (i - (count - 1) / 2f) * 1.1f;
            return Game.Me.transform.position + fwd * 2.2f + right * offset + Vector3.up * 0.8f;
        }

        internal static void SpawnObject(Entry e)
        {
            int ok = 0;
            for (int i = 0; i < _amount; i++)
                if (Instantiate(e.Prefab, Slot(i, _amount), Quaternion.LookRotation(-Game.Camera.transform.forward.WithY0()))) ok++;
            Toasts.Show(ok > 0 ? $"{e.Name} ×{ok}" + L.T(" devant toi.", " in front of you.") : L.T("Échec de l'apparition.", "Spawn failed."));
        }

        internal static void SpawnEnemy(Entry e)
        {
            var me = Game.Me.transform.position;
            int ok = 0;
            for (int i = 0; i < _amount; i++)
            {
                // A waypoint off the truck, close but not on top of you; the AI needs the nav mesh.
                var point = SemiFunc.LevelPointGet(me, 8f, 35f) ?? SemiFunc.LevelPointGet(me, 0f, 1000f);
                if (!point) break;
                LevelGenerator.Instance.EnemySpawn(e.Enemy, point.transform.position);
                ok++;
            }
            Toasts.Show(ok > 0 ? $"{e.Name} ×{ok}" + L.T(" apparu(s) à proximité.", " spawned nearby.")
                : L.T("Aucun point d'apparition dans ce niveau.", "No spawn point in this level."));
        }

        private static Vector3 WithY0(this Vector3 v)
        {
            v.y = 0;
            return v.sqrMagnitude > 0.001f ? v.normalized : Vector3.forward;
        }

        // ---- Tab ---------------------------------------------------------------------------

        public static void Draw(Ui ui)
        {
            bool host = Game.IsHost, inGame = Game.InGame;
            if (inGame) Build();
            int lockFrom = host && inGame ? Ui.Open : 0;

            ui.Section(L.T("Réglages", "Options"));
            _amount = ui.Stepper("spawn.amount", L.T("Quantité", "Amount"), L.T("Combien d'exemplaires à chaque clic.", "How many copies per click."),
                _amount, 1, 10);
            if (!ui.Searching)
            {
                int k = ui.Buttons("spawn.kind", false,
                    new Btn(L.T("Objets et armes", "Items and weapons"), _kind == Kind.Items ? ButtonKind.Primary : ButtonKind.Secondary),
                    new Btn(L.T("Objets de valeur", "Valuables"), _kind == Kind.Valuables ? ButtonKind.Primary : ButtonKind.Secondary),
                    new Btn(L.T("Ennemis", "Enemies"), _kind == Kind.Enemies ? ButtonKind.Primary : ButtonKind.Secondary));
                if (k >= 0) _kind = (Kind)k;
            }
            if (!host) ui.Note(L.T("Seul l'hôte peut créer des objets et des ennemis.", "Only the host can create objects and enemies."), Theme.Alarm);
            if (!inGame) { ui.Empty(L.T("Lance une partie pour voir les listes.", "Start a game to see the lists.")); return; }

            // While searching every list is shown so a name can be found from any category.
            if (ui.Searching || _kind == Kind.Items)
            {
                ui.Section(L.T("Objets et armes", "Items and weapons") + "  ·  " + Items.Count);
                foreach (var e in Items)
                    if (ui.ListRow("spawn.i." + e.Name, e.Name, ItemType(e.Item), lockFrom, Theme.Signal, new Btn(L.T("Créer", "Spawn"), ButtonKind.Primary)) == 0)
                        SpawnObject(e);
            }
            if (ui.Searching || _kind == Kind.Valuables)
            {
                ui.Section(L.T("Objets de valeur", "Valuables") + "  ·  " + Valuables.Count);
                if (ui.Buttons("spawn.v.random", !host, L.T("Un au hasard", "A random one")) == 0 && Valuables.Count > 0)
                    SpawnObject(Valuables[Random.Range(0, Valuables.Count)]);
                foreach (var e in Valuables)
                {
                    string caption = L.T("Taille : ", "Size: ") + (L.French ? SizeFr[e.Size] : SizeEn[e.Size]);
                    if (ui.ListRow("spawn.v." + e.Name, e.Name, caption, lockFrom, Theme.Amber, new Btn(L.T("Créer", "Spawn"), ButtonKind.Primary)) == 0)
                        SpawnObject(e);
                }
            }
            if (ui.Searching || _kind == Kind.Enemies)
            {
                ui.Section(L.T("Ennemis", "Enemies") + "  ·  " + Enemies.Count);
                ui.Note(L.T("Ils apparaissent sur un point de passage à quelques dizaines de mètres, jamais dans le camion.",
                    "They appear on a waypoint a few dozen metres away, never in the truck."));
                foreach (var e in Enemies)
                    if (ui.ListRow("spawn.e." + e.Name, e.Name, L.T("Difficulté ", "Difficulty ") + e.Tier, lockFrom, Theme.Alarm, new Btn(L.T("Créer", "Spawn"), ButtonKind.Danger)) == 0)
                        SpawnEnemy(e);
            }
        }

        public static void CollectActive(List<string> list) { }
    }
}
