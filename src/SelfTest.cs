#if SELFTEST
using System;
using System.Collections;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using RepoTrainer.Features;
using RepoTrainer.UI;
using UnityEngine;

namespace RepoTrainer
{
    /// <summary>
    /// Automated in-game check, only compiled with -p:SelfTest=true. Starts a throwaway solo run,
    /// exercises every feature, saves screenshots and a PASS/FAIL report, deletes the save and quits.
    /// </summary>
    internal sealed class SelfTest : MonoBehaviour
    {
        private ManualLogSource _log;
        private string _dir;
        private int _pass, _fail, _errors;
        private string _save;

        public static void Start(GameObject host, ManualLogSource log)
        {
            var t = host.AddComponent<SelfTest>();
            t._log = log;
        }

        // Start, not Awake: Awake runs inside AddComponent, before _log is assigned.
        private void Start()
        {
            _dir = Path.Combine(Paths.BepInExRootPath, "selftest");
            Directory.CreateDirectory(_dir);
            foreach (var f in Directory.GetFiles(_dir)) File.Delete(f);
            Application.logMessageReceived += OnLog;
            Application.runInBackground = true;
            StartCoroutine(Script());
        }

        private void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            if ((stack ?? "").Contains("RepoTrainer") || condition.Contains("RepoTrainer"))
            {
                _errors++;
                _log.LogError("[selftest] trainer error: " + condition + "\n" + stack);
            }
        }

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) _pass++; else _fail++;
            _log.LogInfo($"[selftest] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(0.6f);
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));
            yield return new WaitForSecondsRealtime(0.3f);
        }

        private static IEnumerator Until(Func<bool> cond, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (!cond() && Time.realtimeSinceStartup < end) yield return null;
        }

        private IEnumerator Script()
        {
            _log.LogInfo("[selftest] waiting for the main menu");
            yield return Until(() => RunManager.instance && SemiFunc.IsMainMenu() && MenuManager.instance && StatsManager.instance, 120f);
            yield return new WaitForSecondsRealtime(4f);

            // ---- Menu on the main menu screen ----
            L.Setting.Value = Language.Francais;
            Menu.Open();
            Menu.Select(0, "");
            yield return Shot("00_mainmenu_player");
            Menu.Select(9, "");
            yield return Shot("01_mainmenu_settings");
            Menu.Close();

            // ---- Start a throwaway solo run ----
            _log.LogInfo("[selftest] starting a solo run");
            SemiFunc.MainMenuSetSingleplayer();
            SemiFunc.MenuActionSingleplayerGame();
            _save = StatsManager.instance.saveFileCurrent;
            _log.LogInfo("[selftest] save " + _save);
            yield return Until(() => Game.InGame && GameDirector.instance.currentState == GameDirector.gameState.Main, 180f);
            Check("enter level", Game.InGame, SemiFunc.RunIsLevel() ? "level" : SemiFunc.RunIsShop() ? "shop" : "other");
            if (!Game.InGame) { Finish(); yield break; }
            yield return new WaitForSecondsRealtime(6f);

            var pc = Game.Controller;
            var me = Game.Me;
            var hp = me.playerHealth;

            // ---- Menu tabs ----
            for (int i = 0; i < Menu.TabCount; i++)
            {
                Menu.Open();
                Menu.Select(i, "");
                yield return Shot($"1{i}_tab");
            }
            // Scrolled to the very end: the last row of a long tab must be fully visible.
            Menu.Select(0, "", 99999f);
            yield return Shot("19_player_bottom");
            Menu.Select(0, "argent");
            yield return Shot("20_search_argent");
            Menu.Select(0, "zzzz");
            yield return Shot("21_search_empty");
            L.Setting.Value = Language.English;
            Menu.Select(2, "");
            yield return Shot("22_english_enemies");
            L.Setting.Value = Language.Francais;
            Menu.Select(0, "");
            Menu.Close();
            yield return new WaitForSecondsRealtime(0.5f);
            Check("cursor relocked after close", Cursor.lockState != CursorLockMode.None, Cursor.lockState.ToString());

            // ---- Survival ----
            int before = hp.health;
            Self.God = true;
            yield return null;
            hp.Hurt(30, false);
            Check("god mode blocks damage", hp.health == before, $"{before} -> {hp.health}");
            Self.God = false;
            yield return null;
            hp.Hurt(10, false);
            Check("damage without god", hp.health == before - 10, $"{before} -> {hp.health}");
            Self.HealSelf();
            Check("heal to full", hp.health == hp.maxHealth, $"{hp.health}/{hp.maxHealth}");

            Self.InfiniteStamina = true;
            pc.EnergyCurrent = 3f;
            yield return null;
            yield return null;
            Check("infinite stamina", Mathf.Approximately(pc.EnergyCurrent, pc.EnergyStart), $"{pc.EnergyCurrent}/{pc.EnergyStart}");
            Self.InfiniteStamina = false;
            yield return null;
            Check("stamina flag released", !pc.DebugEnergy);

            Self.NoTumble = true;
            me.tumble.TumbleRequest(_isTumbling: true, _playerInput: false);
            yield return new WaitForSecondsRealtime(0.6f);
            Check("never knocked over", !me.isTumbling);
            Self.NoTumble = false;
            me.tumble.TumbleRequest(_isTumbling: true, _playerInput: false);
            yield return new WaitForSecondsRealtime(0.6f);
            Check("knock-down works when off", me.isTumbling);
            me.tumble.TumbleRequest(_isTumbling: false, _playerInput: true);
            yield return new WaitForSecondsRealtime(1.5f);

            // ---- Movement ----
            float move = pc.playerOriginalMoveSpeed, jump = pc.JumpForce, grav = pc.playerOriginalCustomGravity;
            Self.SpeedMult = 2f; Self.JumpMult = 2f; Self.GravityMult = 0.5f;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Check("speed ×2", Mathf.Approximately(pc.MoveSpeed, move * 2f), $"{pc.MoveSpeed} vs {move}");
            Check("jump ×2", Mathf.Approximately(pc.JumpForce, jump * 2f), $"{pc.JumpForce} vs {jump}");
            Check("gravity ×0.5", Mathf.Approximately(pc.CustomGravity, grav * 0.5f), $"{pc.CustomGravity} vs {grav}");
            Self.SpeedMult = Self.JumpMult = Self.GravityMult = 1f;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Check("movement restored", Mathf.Approximately(pc.MoveSpeed, move) && Mathf.Approximately(pc.JumpForce, jump), $"{pc.MoveSpeed} {pc.JumpForce}");

            Self.AirJump = true;
            yield return null;
            Check("air jump reserve", pc.JumpExtraCurrent >= 1, pc.JumpExtraCurrent.ToString());
            Self.AirJump = false;

            var start = pc.transform.position;
            Self.Noclip = true;
            yield return new WaitForSecondsRealtime(0.3f);
            Check("noclip disables collider", !pc.col.enabled && pc.rb.isKinematic);
            Game.TeleportTo(start + Vector3.up * 6f);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("noclip hovers", pc.transform.position.y > start.y + 5f, $"{pc.transform.position.y - start.y:0.0} m up");
            yield return Shot("30_noclip");
            Self.Noclip = false;
            float yOff = pc.transform.position.y;
            yield return new WaitForSecondsRealtime(1.5f);
            Check("noclip off restores physics", pc.col.enabled && !pc.rb.isKinematic);
            // 6 m above the truck floor can be the truck roof, so only check that gravity acted.
            Check("gravity back after noclip", pc.transform.position.y < yOff - 0.2f || pc.CollisionController.Grounded,
                $"{yOff - pc.transform.position.y:0.0} m drop, grounded {pc.CollisionController.Grounded}");
            Check("no fall damage after noclip", hp.health == hp.maxHealth, $"{hp.health}");

            // ---- Grab and camera ----
            var g = PhysGrabber.instance;
            float range = g.grabRange, str = g.grabStrength;
            Self.GrabRangeMult = 2f; Self.GrabStrengthMult = 3f;
            yield return null;
            Check("grab range ×2", Mathf.Approximately(g.grabRange, range * 2f), $"{g.grabRange} vs {range}");
            Check("grab strength ×3", Mathf.Approximately(g.grabStrength, str * 3f), $"{g.grabStrength} vs {str}");
            Self.GrabRangeMult = Self.GrabStrengthMult = 1f;
            yield return null;
            Check("grab restored", Mathf.Approximately(g.grabRange, range), $"{g.grabRange}");

            float zoomDefault = CameraZoom.Instance.playerZoomDefault;
            Self.FovOverride = true; Self.Fov = 110f;
            yield return new WaitForSecondsRealtime(1f);
            Check("custom FOV", Mathf.Abs(CameraZoom.Instance.zoomCurrent - 110f) < 3f, $"{CameraZoom.Instance.zoomCurrent:0}");
            yield return Shot("31_fov110");
            Self.FovOverride = false;
            yield return new WaitForSecondsRealtime(1f);
            Check("FOV restored", Mathf.Approximately(CameraZoom.Instance.playerZoomDefault, zoomDefault), $"{CameraZoom.Instance.playerZoomDefault}");

            // ---- Teleport ----
            var truck = Teleport.TruckPosition();
            Check("truck found", truck.HasValue);
            Game.TeleportTo(start);
            yield return new WaitForSecondsRealtime(0.3f);
            var camT = Game.Camera.transform;
            camT.rotation = Quaternion.LookRotation(Vector3.down * 0.3f + pc.transform.forward);
            var p0 = pc.transform.position;
            Teleport.ToCrosshair();
            yield return new WaitForSecondsRealtime(0.3f);
            Check("crosshair teleport moved", (pc.transform.position - p0).sqrMagnitude > 0.01f, $"{Vector3.Distance(pc.transform.position, p0):0.0} m");

            // ---- Scan, vision ----
            yield return new WaitForSecondsRealtime(0.5f);
            _log.LogInfo($"[selftest] scan: {Scan.Enemies.Count} enemies, {Scan.Valuables.Count} valuables ({Scan.TotalValue}), {Scan.Items.Count} items, {Scan.Extractions.Count} extractions");
            Check("valuables scanned", Scan.Valuables.Count > 0 || !SemiFunc.RunIsLevel(), Scan.Valuables.Count.ToString());
            Check("extractions scanned", Scan.Extractions.Count > 0 || !SemiFunc.RunIsLevel(), Scan.Extractions.Count.ToString());

            Vision.Enemies = Vision.Valuables = Vision.Items = Vision.Players = Vision.Extractions = Vision.DeadHeads = true;
            Vision.MaxDistance = 400f;
            yield return Shot("40_esp");
            Vision.Fullbright = true; Vision.NoFog = true;
            yield return new WaitForSecondsRealtime(0.5f);
            Check("fullbright", RenderSettings.ambientLight.r > 0.5f, RenderSettings.ambientLight.ToString());
            Check("no fog", !RenderSettings.fog && Game.Camera.farClipPlane >= 599f, $"{RenderSettings.fog} {Game.Camera.farClipPlane}");
            yield return Shot("41_esp_fullbright");
            Vision.Fullbright = false; Vision.NoFog = false;
            yield return new WaitForSecondsRealtime(0.5f);
            Check("fog restored", Game.Camera.farClipPlane < 599f, Game.Camera.farClipPlane.ToString());

            // ---- Loot ----
            if (Scan.Valuables.Count > 0)
            {
                var v = Scan.Valuables[Scan.Valuables.Count - 1];
                float value = v.dollarValueCurrent;
                var menuButtons = Game.InFront();
                v.physGrabObject.Teleport(menuButtons, Quaternion.identity);
                yield return new WaitForSecondsRealtime(1f);
                Check("bring valuable", Vector3.Distance(v.transform.position, me.transform.position) < 6f, $"{Vector3.Distance(v.transform.position, me.transform.position):0.0} m");

                Loot.Indestructible = true;
                yield return new WaitForSecondsRealtime(1f);
                value = v.dollarValueCurrent; // landing after the teleport may already have cost some value
                var impact = v.GetComponent<PhysGrabObjectImpactDetector>();
                impact.BreakHeavy(v.transform.position);
                yield return new WaitForSecondsRealtime(0.5f);
                Check("indestructible keeps value", v && Mathf.Approximately(v.dollarValueCurrent, value), v ? v.dollarValueCurrent.ToString() : "destroyed");
                Loot.Indestructible = false;

                if (v)
                {
                    value = v.dollarValueCurrent;
                    var mult = typeof(Loot).GetMethod("MultiplyValues", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    mult.Invoke(null, new object[] { 2f });
                    yield return new WaitForSecondsRealtime(0.3f);
                    Check("value ×2", Mathf.Approximately(v.dollarValueCurrent, Mathf.Round(value * 2f)), $"{value} -> {v.dollarValueCurrent}");
                }
                var bringAll = typeof(Loot).GetMethod("BringAll", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                bringAll.Invoke(null, null);
                yield return new WaitForSecondsRealtime(1.5f);
                int near = 0;
                foreach (var x in Scan.Valuables) if (x && Vector3.Distance(x.transform.position, me.transform.position) < 12f) near++;
                Check("bring all", near >= Mathf.Min(5, Scan.Valuables.Count), $"{near}/{Scan.Valuables.Count} nearby");
                yield return Shot("50_bring_all");
            }

            Loot.InfiniteBattery = true;
            yield return null;
            Check("infinite battery flag", RoundDirector.instance.debugInfiniteBattery);
            Loot.InfiniteBattery = false;
            yield return null;

            // ---- Run: money, upgrades ----
            Run.SetCurrency(500);
            Check("set money", Run.Currency == 500, Run.Currency.ToString());
            Run.SetCurrency(Run.Currency + 10);
            Check("add money", Run.Currency == 510, Run.Currency.ToString());
            int lvl = Run.Level(me, "Health"), maxHp = hp.maxHealth;
            Run.Give(me, "Health", 1);
            yield return null;
            Check("health upgrade", Run.Level(me, "Health") == lvl + 1 && hp.maxHealth == maxHp + 20, $"lvl {Run.Level(me, "Health")}, max {hp.maxHealth}");
            float energy = pc.EnergyStart;
            Run.GiveAll(me, 1);
            yield return null;
            Check("all upgrades +1", Run.Level(me, "Stamina") >= 1 && pc.EnergyStart > energy, $"stamina lvl {Run.Level(me, "Stamina")}");
            Menu.Open(); Menu.Select(5, "");
            yield return Shot("60_run_after_upgrades");
            Menu.Close();

            // ---- Enemies ----
            Enemies.Invisible = true;
            yield return null;
            Check("invisible to enemies", EnemyDirector.instance.debugNoVision.Contains(me.steamID));
            Enemies.Invisible = false;
            yield return null;
            Check("visible again", !EnemyDirector.instance.debugNoVision.Contains(me.steamID));
            Team.TeamInvisible = true; Enemies.Invisible = true;
            yield return null;
            Enemies.Invisible = false;
            yield return null;
            Check("team invisibility survives own toggle", EnemyDirector.instance.debugNoVision.Contains(me.steamID));
            Team.TeamInvisible = false;
            yield return null;
            Check("team invisibility off", !EnemyDirector.instance.debugNoVision.Contains(me.steamID));

            _log.LogInfo("[selftest] waiting up to 60 s for an enemy to spawn");
            yield return Until(() => Scan.Enemies.Count > 0, 60f);
            if (Scan.Enemies.Count > 0)
            {
                var e = Scan.Enemies[0];
                Check("enemy listed", e, Scan.EnemyName(e));
                Enemies.FreezeAll = true;
                yield return new WaitForSecondsRealtime(0.8f);
                Check("freeze all", e.Enemy.FreezeTimer > 0f, e.Enemy.FreezeTimer.ToString("0.0"));
                Enemies.FreezeAll = false;
                Vision.Enemies = true;
                Game.TeleportTo(Scan.EnemyPos(e) + Vector3.up * 2.5f - Game.Camera.transform.forward * 4f);
                yield return Shot("70_enemy_esp");
                Enemies.Kill(e);
                yield return new WaitForSecondsRealtime(2f);
                Check("kill enemy", !e || !e.Enemy.HasHealth || e.Enemy.Health.dead || e.Enemy.Health.healthCurrent <= 0 || !e.Spawned, Scan.EnemyName(e));
            }
            else Check("enemy spawned within 60 s", false, "(none, not a trainer failure)");

            // ---- Spawner ----
            Game.TeleportTo(start);
            yield return new WaitForSecondsRealtime(0.5f);
            Spawner.Build();
            _log.LogInfo($"[selftest] catalogue: {Spawner.Items.Count} items, {Spawner.Valuables.Count} valuables, {Spawner.Enemies.Count} enemies");
            Check("catalogue built", Spawner.Items.Count > 10 && Spawner.Valuables.Count > 20 && Spawner.Enemies.Count > 5);
            int itemsBefore = ItemManager.instance.spawnedItems.Count, valBefore = ValuableDirector.instance.valuableList.Count;
            Spawner.SpawnObject(Spawner.Items[0]);
            Spawner.SpawnObject(Spawner.Valuables[0]);
            yield return new WaitForSecondsRealtime(1.5f);
            int valAfter = ValuableDirector.instance.valuableList.Count;
            Check("spawn valuable", valAfter > valBefore, $"{valBefore} -> {valAfter} ({Spawner.Valuables[0].Name})");
            var spawned = ValuableDirector.instance.valuableList[valAfter - 1];
            yield return new WaitForSecondsRealtime(1f);
            Check("spawned valuable has a price", spawned && spawned.dollarValueCurrent > 0f, spawned ? spawned.dollarValueCurrent.ToString() : "gone");
            int nearItems = 0;
            foreach (var it in UnityEngine.Object.FindObjectsByType<ItemAttributes>(FindObjectsSortMode.None))
                if (it && Vector3.Distance(it.transform.position, me.transform.position) < 5f) nearItems++;
            Check("spawn item", nearItems > 0, $"{Spawner.Items[0].Name}, {nearItems} item(s) nearby, list {itemsBefore}");
            yield return Shot("75_spawned");
            int enemiesBefore = EnemyDirector.instance.enemiesSpawned.Count;
            Spawner.SpawnEnemy(Spawner.Enemies[0]);
            yield return new WaitForSecondsRealtime(4f);
            Check("spawn enemy", EnemyDirector.instance.enemiesSpawned.Count > enemiesBefore || UnityEngine.Object.FindObjectsByType<EnemyParent>(FindObjectsSortMode.None).Length > enemiesBefore,
                $"{Spawner.Enemies[0].Name}: {enemiesBefore} -> {EnemyDirector.instance.enemiesSpawned.Count}");
            Menu.Open(); Menu.Select(4, "");
            yield return Shot("76_spawn_tab");
            Menu.Close();

            // ---- Gather (solo: only the local robot) ----
            var truckPos = Teleport.TruckPosition();
            Team.Move(me, truckPos.Value + Vector3.up * 0.5f);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("move player to truck", Vector3.Distance(me.transform.position, truckPos.Value) < 3f, $"{Vector3.Distance(me.transform.position, truckPos.Value):0.0} m");
            me.Spawn(start + Vector3.up * 0.3f, me.transform.rotation);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("host spawn call moves player", Vector3.Distance(me.transform.position, start) < 3f, $"{Vector3.Distance(me.transform.position, start):0.0} m");
            Menu.Open(); Menu.Select(6, "");
            yield return Shot("77_team_tab");
            Menu.Close();

            // ---- Extras: overcharge, map, doors, crown, cosmetics ----
            Self.NoOvercharge = true;
            yield return null;
            Check("no overcharge flag", pc.DebugDisableOvercharge);
            Self.NoOvercharge = false;
            yield return null;
            Check("overcharge flag released", !pc.DebugDisableOvercharge);

            int rooms = Vision.RevealMap();
            Check("reveal map", rooms > 0, rooms + " rooms");
            Vision.MapValuables();
            int hidden = 0;
            foreach (var v in Scan.Valuables) if (v && !v.discovered) hidden++;
            Check("valuables on map", hidden == 0, hidden + " still hidden");

            var hinges = UnityEngine.Object.FindObjectsByType<PhysGrabHinge>(FindObjectsSortMode.None);
            int alive = 0;
            foreach (var h in hinges) if (h && !h.dead) { h.DestroyHinge(); alive++; }
            yield return new WaitForSecondsRealtime(1f);
            int broken = 0;
            foreach (var h in hinges) if (!h || h.dead) broken++;
            Check("break doors", alive == 0 || broken >= alive, $"{broken}/{hinges.Length}");

            string crownBefore = SessionManager.instance.crownedPlayerSteamID;
            PunManager.instance.CrownPlayerSync(me.steamID);
            yield return new WaitForSecondsRealtime(1f);
            Check("crown me", SessionManager.instance.CrownedPlayerGet() == me);
            yield return Shot("78_crown");
            PunManager.instance.CrownPlayerSync(crownBefore ?? "");

            // Tokens are added then the list is put back exactly as it was, so the real save is untouched.
            var meta = MetaManager.instance;
            var tokensBefore = new System.Collections.Generic.List<int>(meta.cosmeticTokens);
            int commons = Cosmetics.TokenCount(SemiFunc.Rarity.Common);
            Cosmetics.AddTokens(SemiFunc.Rarity.Common, 2);
            Check("add cosmetic tokens", Cosmetics.TokenCount(SemiFunc.Rarity.Common) == commons + 2);
            Menu.Open(); Menu.Select(8, "");
            yield return Shot("79_cosmetics_tab");
            Menu.Close();
            meta.cosmeticTokens.Clear();
            meta.cosmeticTokens.AddRange(tokensBefore);
            meta.Save();
            Check("cosmetic tokens restored", meta.cosmeticTokens.Count == tokensBefore.Count);

            // ---- Team regeneration ----
            Self.God = false;
            yield return null;
            hp.Hurt(40, false);
            int hurt = hp.health;
            Team.Regen = true; Team.RegenAmount = 10f;
            yield return new WaitForSecondsRealtime(2.5f);
            Check("team regeneration", hp.health > hurt, $"{hurt} -> {hp.health}");
            Team.Regen = false;
            Self.HealSelf();

            // ---- Enemy speed ----
            EnemyParent walker = null;
            foreach (var en in Scan.Enemies)
                if (en && en.Enemy && en.Enemy.HasNavMeshAgent && en.Enemy.NavMeshAgent.Agent && en.Enemy.NavMeshAgent.Agent.enabled) { walker = en; break; }
            Check("walking enemy available for speed test", walker, walker ? Scan.EnemyName(walker) : $"{Scan.Enemies.Count} enemies, none on the nav mesh");
            if (walker)
            {
                var agent = walker.Enemy.NavMeshAgent.Agent;
                float speedBefore = agent.speed;
                Enemies.SpeedMult = 0.5f;
                yield return new WaitForSecondsRealtime(0.3f);
                float slowed = agent.speed;
                Check("enemy speed ×0.5", slowed < speedBefore * 0.75f || slowed < 0.01f, $"{speedBefore:0.00} -> {slowed:0.00}");
                Enemies.SpeedMult = 1f;
                yield return new WaitForSecondsRealtime(0.3f);
                Check("enemy speed restored", agent.speed > slowed || Mathf.Approximately(agent.speed, speedBefore), $"{agent.speed:0.00}");
            }

            // ---- Extraction: send everything, then complete ----
            // One cheap valuable only, so the goal is not reached on its own and the button has to do it.
            var epx = Teleport.FindExtraction();
            // The game switches the first point on by itself once someone leaves the truck.
            if (epx && Scan.Valuables.Count > 0 && (epx.StateIs(ExtractionPoint.State.Idle) || epx.StateIs(ExtractionPoint.State.Active)))
            {
                if (epx.StateIs(ExtractionPoint.State.Idle)) epx.OnClick();
                yield return new WaitForSecondsRealtime(1.5f);
                Check("extraction activated", epx.StateIs(ExtractionPoint.State.Active), epx.currentState.ToString());
                var cheap = Scan.Valuables[Scan.Valuables.Count - 1];
                cheap.physGrabObject.Teleport(epx.transform.position + Vector3.up * 1.2f, Quaternion.identity);
                yield return new WaitForSecondsRealtime(2.5f);
                Check("one valuable counted, goal not reached", epx.haulCurrent > 0 && epx.haulCurrent < epx.haulGoal,
                    "$" + epx.haulCurrent + " / $" + epx.haulGoal);
                Game.TeleportTo(Teleport.ExtractionPosition().Value);
                yield return Shot("74_extraction_partial");
                bool done = Run.CompleteExtraction();
                yield return new WaitForSecondsRealtime(4f);
                Check("complete extraction button", done && !epx.StateIs(ExtractionPoint.State.Active), epx.currentState.ToString());
                yield return Shot("74b_extraction_done");

                Loot.SendAllToExtraction();
                yield return new WaitForSecondsRealtime(2f);
                int nearEp = 0, total = 0;
                foreach (var v in Scan.Valuables)
                    if (v) { total++; if (Vector3.Distance(v.transform.position, epx.transform.position) < 4f) nearEp++; }
                Check("send all to extraction", nearEp >= Mathf.Min(5, total), $"{nearEp}/{total} at the point");
            }
            else Check("idle extraction point for test", false, epx ? epx.currentState.ToString() : "none");

            // ---- HUD and toasts ----
            Self.God = true; Self.InfiniteStamina = true; Vision.Valuables = true;
            Toasts.Show("Test de notification du trainer.");
            yield return Shot("80_hud_toast");

            // ---- Reset ----
            Plugin.ResetAll();
            yield return null;
            Check("reset all", !Self.God && !Vision.AnyEsp && !hp.godMode && !pc.DebugEnergy);

            // ---- Level change ----
            var level = LevelGenerator.Instance;
            RunManager.instance.ChangeLevel(_completedLevel: true, _levelFailed: false);
            yield return Until(() => LevelGenerator.Instance != level && Game.InGame && GameDirector.instance.currentState == GameDirector.gameState.Main, 120f);
            Check("skip to next level", Game.InGame && LevelGenerator.Instance != level, SemiFunc.RunIsShop() ? "shop" : "level");
            yield return new WaitForSecondsRealtime(3f);
            Menu.Open(); Menu.Select(0, "");
            yield return Shot("90_after_level_change");
            Menu.Close();

            Finish();
        }

        private void Finish()
        {
            _log.LogInfo($"[selftest] DONE: {_pass} passed, {_fail} failed, {_errors} trainer errors");
            File.WriteAllText(Path.Combine(_dir, "result.txt"), $"{_pass} passed, {_fail} failed, {_errors} errors");
            if (!string.IsNullOrEmpty(_save) && StatsManager.instance)
            {
                StatsManager.instance.SaveFileDelete(_save);
                _log.LogInfo("[selftest] deleted save " + _save);
            }
            Application.Quit();
        }
    }
}
#endif
