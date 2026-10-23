using System.Collections.Generic;
using UnityEngine;

namespace RepoTrainer.Features
{
    /// <summary>
    /// Snapshot of the level's enemies, valuables, items, extraction points and death heads,
    /// refreshed a few times a second from the game's own registries instead of scene searches.
    /// The ESP and the lists read from here every frame.
    /// </summary>
    internal static class Scan
    {
        public static readonly List<EnemyParent> Enemies = new List<EnemyParent>();
        public static readonly List<ValuableObject> Valuables = new List<ValuableObject>();
        public static readonly List<ItemAttributes> Items = new List<ItemAttributes>();
        public static readonly List<ExtractionPoint> Extractions = new List<ExtractionPoint>();
        public static readonly List<PlayerDeathHead> DeadHeads = new List<PlayerDeathHead>();

        public static float TotalValue { get; private set; }

        private const float Interval = 0.25f;
        private static float _next;
        private static LevelGenerator _level;

        public static void Tick()
        {
            if (!Game.InGame) { Clear(); return; }
            if (_level != LevelGenerator.Instance) { _level = LevelGenerator.Instance; Clear(); _next = 0f; }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Interval;
            Refresh();
        }

        public static void Clear()
        {
            Enemies.Clear();
            Valuables.Clear();
            Items.Clear();
            Extractions.Clear();
            DeadHeads.Clear();
            TotalValue = 0f;
        }

        private static void Refresh()
        {
            Enemies.Clear();
            if (EnemyDirector.instance && EnemyDirector.instance.enemiesSpawned != null)
                foreach (var e in EnemyDirector.instance.enemiesSpawned)
                    if (e && e.Spawned && e.Enemy && !(e.Enemy.HasHealth && (e.Enemy.Health.dead || e.Enemy.Health.healthCurrent <= 0)))
                        Enemies.Add(e);

            Valuables.Clear();
            TotalValue = 0f;
            if (ValuableDirector.instance)
                foreach (var v in ValuableDirector.instance.valuableList)
                    if (v && v.gameObject.activeInHierarchy)
                    {
                        Valuables.Add(v);
                        TotalValue += v.dollarValueCurrent;
                    }
            Valuables.Sort((a, b) => b.dollarValueCurrent.CompareTo(a.dollarValueCurrent));

            Items.Clear();
            if (ItemManager.instance)
                foreach (var it in ItemManager.instance.spawnedItems)
                    if (it && it.gameObject.activeInHierarchy) Items.Add(it);

            Extractions.Clear();
            if (RoundDirector.instance)
                foreach (var go in RoundDirector.instance.extractionPointList)
                    if (go && go.TryGetComponent<ExtractionPoint>(out var ep)) Extractions.Add(ep);

            DeadHeads.Clear();
            if (GameDirector.instance)
                foreach (var p in GameDirector.instance.PlayerList)
                    if (p && p.deadSet && p.playerDeathHead && p.playerDeathHead.isActiveAndEnabled)
                        DeadHeads.Add(p.playerDeathHead);
        }

        public static string EnemyName(EnemyParent e) => string.IsNullOrEmpty(e.enemyName) ? "?" : e.enemyName;

        public static Vector3 EnemyPos(EnemyParent e) =>
            e.Enemy && e.Enemy.CenterTransform ? e.Enemy.CenterTransform.position : e.transform.position;

        public static string ItemName(ItemAttributes it) =>
            !string.IsNullOrEmpty(it.itemName) ? it.itemName : it.item ? it.item.itemName : it.name;
    }
}
