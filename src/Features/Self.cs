using System.Collections.Generic;
using HarmonyLib;
using RepoTrainer.UI;
using UnityEngine;

namespace RepoTrainer.Features
{
    /// <summary>The local robot: survival, movement, noclip, grabbing and camera.</summary>
    internal static class Self
    {
        public static bool God, InfiniteStamina, Noclip, AirJump, NoTumble, FovOverride, NoOvercharge;
        public static float SpeedMult = 1f, JumpMult = 1f, GravityMult = 1f, NoclipSpeed = 8f, Fov = 85f;
        public static float GrabRangeMult = 1f, GrabStrengthMult = 1f, ThrowMult = 1f;

        private static PlayerController _pc;
        private static PlayerHealth _hp;
        private static bool _godApplied, _energyApplied, _overchargeApplied;
        private static float _nextStand;
        private static float _baseJump;
        private static bool _movementDirty, _noclipWasOn, _fovApplied;
        private static float _fovDefault;

        // Grab values are additive upgrades on top of a base; we track the base so the game's
        // own upgrades still land while a multiplier is applied.
        private static PhysGrabber _grabber;
        private static float _baseRange, _baseStrength, _baseThrow, _appliedRange, _appliedStrength, _appliedThrow;

        public static void Reset()
        {
            God = InfiniteStamina = Noclip = AirJump = NoTumble = FovOverride = NoOvercharge = false;
            SpeedMult = JumpMult = GravityMult = 1f;
            GrabRangeMult = GrabStrengthMult = ThrowMult = 1f;
            _movementDirty = true;
            Tick();
            ApplyGrab(true);
            ApplyFov();
        }

        public static void Tick()
        {
            var pc = Game.Controller;
            if (!Game.InGame || !pc) { _pc = null; _grabber = null; return; }
            if (_pc != pc)
            {
                _pc = pc;
                _baseJump = pc.JumpForce;
                _noclipWasOn = false;
                _energyApplied = _overchargeApplied = false;
            }

            // These flags are also set by the game itself (the vehicle arena turns god mode on every
            // frame), so they are written only while our option is on and cleared once when it goes off.
            var me = Game.Me;
            var hp = me ? me.playerHealth : null;
            if (hp && hp != _hp) { _hp = hp; _godApplied = false; }
            if (hp && God) { hp.godMode = true; _godApplied = true; }
            else if (hp && _godApplied) { hp.godMode = DebugGod; _godApplied = false; }

            if (InfiniteStamina) { pc.DebugEnergy = true; pc.EnergyCurrent = pc.EnergyStart; _energyApplied = true; }
            else if (_energyApplied) { pc.DebugEnergy = DebugEnergy; _energyApplied = false; }

            if (NoOvercharge) { pc.DebugDisableOvercharge = true; _overchargeApplied = true; }
            else if (_overchargeApplied) { pc.DebugDisableOvercharge = false; _overchargeApplied = false; }

            // As a guest, enemy knock-downs are decided by the host; stand straight back up instead.
            if (NoTumble && me && me.isTumbling && me.tumble && !me.tumble.isPlayerInputTriggered && Time.unscaledTime >= _nextStand)
            {
                _nextStand = Time.unscaledTime + 0.3f;
                me.tumble.TumbleRequest(_isTumbling: false, _playerInput: true);
            }

            if (_noclipWasOn && !Noclip) EndNoclip(pc);
            _noclipWasOn = Noclip;

            ApplyGrab(false);
        }

        public static void LateTick() => ApplyFov();

        private static bool DebugGod => DebugCommandHandler.instance && DebugCommandHandler.instance.godMode;
        private static bool DebugEnergy => DebugCommandHandler.instance && DebugCommandHandler.instance.infiniteEnergy;

        /// <summary>Runs before PlayerController.FixedUpdate: speed, jump and gravity multipliers.</summary>
        private static void ApplyMovement(PlayerController pc)
        {
            bool custom = !Mathf.Approximately(SpeedMult, 1f) || !Mathf.Approximately(JumpMult, 1f) || !Mathf.Approximately(GravityMult, 1f);
            if (!custom && !_movementDirty) return;
            _movementDirty = custom;
            if (pc.playerOriginalMoveSpeed <= 0f) return; // upgrades not loaded yet

            // The game's own slow-downs (overrides) win while they run.
            if (pc.overrideSpeedTimer <= 0f && pc.overrideTimeScaleTimer <= 0f)
            {
                pc.MoveSpeed = pc.playerOriginalMoveSpeed * SpeedMult;
                pc.SprintSpeed = pc.playerOriginalSprintSpeed * SpeedMult;
                pc.CrouchSpeed = pc.playerOriginalCrouchSpeed * SpeedMult;
                pc.CustomGravity = pc.playerOriginalCustomGravity * GravityMult;
            }
            if (_baseJump > 0f) pc.JumpForce = _baseJump * JumpMult;
        }

        // ---- Noclip ------------------------------------------------------------------------

        private static void NoclipStep(PlayerController pc)
        {
            pc.rb.isKinematic = true;
            pc.col.enabled = false;
            pc.JumpImpulse = false;
            pc.VelocityRelativeNew = Vector3.zero;
            pc.CollisionController.ResetFalling();
            if (Menu.IsOpen) return;

            var cam = Game.Camera;
            if (!cam) return;
            var input = new Vector3(SemiFunc.InputMovementX(), 0f, SemiFunc.InputMovementY());
            var dir = cam.transform.right * input.x + cam.transform.forward * input.z;
            if (SemiFunc.InputHold(InputKey.Jump)) dir += Vector3.up;
            if (SemiFunc.InputHold(InputKey.Crouch)) dir += Vector3.down;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            float speed = NoclipSpeed * (SemiFunc.InputHold(InputKey.Sprint) ? 2.5f : 1f);
            var pos = pc.rb.position + dir * speed * Time.fixedDeltaTime;
            pc.rb.position = pos;
            pc.transform.position = pos;
            pc.transform.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
        }

        private static void EndNoclip(PlayerController pc)
        {
            if (!pc) return;
            pc.col.enabled = true;
            pc.rb.isKinematic = false;
            pc.rb.velocity = Vector3.zero;
            pc.CollisionController.ResetFalling();
        }

        [HarmonyPatch(typeof(PlayerController), "FixedUpdate")]
        private static class ControllerFixedPatch
        {
            private static bool Prefix(PlayerController __instance)
            {
                if (__instance != PlayerController.instance || !GameDirector.instance) return true;
                if (GameDirector.instance.currentState != GameDirector.gameState.Main) return true;
                ApplyMovement(__instance);
                if (!Noclip) return true;
                NoclipStep(__instance);
                return false;
            }
        }

        [HarmonyPatch(typeof(PlayerController), "Update")]
        private static class ControllerUpdatePatch
        {
            private static void Prefix(PlayerController __instance)
            {
                // Keep one extra jump in reserve at all times.
                if (AirJump && __instance == PlayerController.instance && __instance.JumpExtraCurrent < 1)
                    __instance.JumpExtraCurrent = 1;
            }

            private static void Postfix(PlayerController __instance)
            {
                if (__instance != PlayerController.instance) return;
                // Landing refills the reserve to the upgrade count, which may be zero.
                if (AirJump && __instance.JumpExtraCurrent < 1) __instance.JumpExtraCurrent = 1;
                // Update re-enables the collider and physics every frame; noclip wins.
                if (!Noclip) return;
                __instance.col.enabled = false;
                __instance.rb.isKinematic = true;
            }
        }

        /// <summary>
        /// Knock-downs not asked for by the player are dropped for our own robot only. The game's
        /// DebugNoTumble flag would instead protect every player on the machine that decides.
        /// </summary>
        [HarmonyPatch(typeof(PlayerTumble), nameof(PlayerTumble.TumbleRequest))]
        private static class TumblePatch
        {
            private static bool Prefix(PlayerTumble __instance, bool _isTumbling, bool _playerInput) =>
                !(NoTumble && _isTumbling && !_playerInput && __instance.playerAvatar && __instance.playerAvatar.isLocal);
        }

        /// <summary>Some hazards kill outright instead of dealing damage.</summary>
        [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.PlayerDeath))]
        private static class DeathPatch
        {
            // The game-over arena eliminates players on purpose; blocking that would stall the round.
            private static bool Prefix(PlayerAvatar __instance) => !(God && __instance.isLocal && !SemiFunc.RunIsArena());
        }

        // ---- Grab --------------------------------------------------------------------------

        private static void ApplyGrab(bool restore)
        {
            var g = PhysGrabber.instance;
            if (!g || !Game.InGame) { _grabber = null; return; }
            if (_grabber != g)
            {
                _grabber = g;
                _baseRange = _appliedRange = g.grabRange;
                _baseStrength = _appliedStrength = g.grabStrength;
                _baseThrow = _appliedThrow = g.throwStrength;
            }
            // Fold any upgrade the game applied since last frame into the base.
            _baseRange += g.grabRange - _appliedRange;
            _baseStrength += g.grabStrength - _appliedStrength;
            _baseThrow += g.throwStrength - _appliedThrow;

            bool hostOnly = restore || !Game.IsHost;
            float rm = restore ? 1f : GrabRangeMult, sm = hostOnly ? 1f : GrabStrengthMult, tm = hostOnly ? 1f : ThrowMult;
            g.grabRange = _appliedRange = _baseRange * rm;
            g.grabStrength = _appliedStrength = _baseStrength * sm;
            // Throw strength starts at zero without upgrades, so it scales additively:
            // each step of ×1 is worth five throw upgrades (0.3 each).
            g.throwStrength = _appliedThrow = _baseThrow + 1.5f * (tm - 1f);
        }

        // ---- Camera ------------------------------------------------------------------------

        private static void ApplyFov()
        {
            var z = CameraZoom.Instance;
            if (!z || !Game.InGame) { _fovApplied = false; return; }
            if (FovOverride)
            {
                if (!_fovApplied) { _fovDefault = z.playerZoomDefault; _fovApplied = true; }
                z.playerZoomDefault = Fov;
                if (!z.OverrideActive) { z.zoomPrev = Fov; z.zoomNew = Fov; }
            }
            else if (_fovApplied)
            {
                _fovApplied = false;
                z.playerZoomDefault = _fovDefault;
                if (!z.OverrideActive) { z.zoomPrev = _fovDefault; z.zoomNew = _fovDefault; }
            }
        }

        // ---- Actions -----------------------------------------------------------------------

        public static void HealSelf()
        {
            var me = Game.Me;
            if (!me || !me.playerHealth) return;
            me.playerHealth.Heal(me.playerHealth.maxHealth - me.playerHealth.health);
            Toasts.Show(L.T("Santé au maximum.", "Health full."));
        }

        public static void CollectActive(List<string> list)
        {
            if (God) list.Add(L.T("invincible", "god mode"));
            if (InfiniteStamina) list.Add(L.T("endurance infinie", "infinite stamina"));
            if (Noclip) list.Add("noclip");
            if (AirJump) list.Add(L.T("saut infini", "infinite jump"));
            if (NoTumble) list.Add(L.T("jamais renversé", "never knocked over"));
            if (NoOvercharge) list.Add(L.T("sans surcharge", "no overcharge"));
            if (!Mathf.Approximately(SpeedMult, 1f)) list.Add(L.T("vitesse ×", "speed ×") + SpeedMult.ToString("0.0"));
            if (!Mathf.Approximately(JumpMult, 1f)) list.Add(L.T("saut ×", "jump ×") + JumpMult.ToString("0.0"));
            if (!Mathf.Approximately(GravityMult, 1f)) list.Add(L.T("gravité ×", "gravity ×") + GravityMult.ToString("0.00"));
            if (!Mathf.Approximately(GrabRangeMult, 1f) || !Mathf.Approximately(GrabStrengthMult, 1f) || !Mathf.Approximately(ThrowMult, 1f))
                list.Add(L.T("prise renforcée", "stronger grab"));
            if (FovOverride) list.Add(L.T("champ de vision ", "FOV ") + Fov.ToString("0") + "°");
        }

        // ---- Tab ---------------------------------------------------------------------------

        private static string X(float v) => "×" + v.ToString(v < 1f ? "0.00" : "0.0");

        public static void Draw(Ui ui)
        {
            ui.Section(L.T("Survie", "Survival"));
            God = ui.Toggle("self.god", L.T("Invincible", "God mode"),
                L.T("Aucun dégât, aucune chute, aucune mort.", "No damage, no falls, no death."), God);
            InfiniteStamina = ui.Toggle("self.stamina", L.T("Endurance infinie", "Infinite stamina"),
                L.T("Sprint et glissades sans jamais s'essouffler.", "Sprint and slide without ever running out."), InfiniteStamina);
            NoTumble = ui.Toggle("self.tumble", L.T("Jamais renversé", "Never knocked over"),
                L.T("Les coups ne te font plus tomber, ou tu te relèves aussitôt chez un autre hôte.",
                    "Hits no longer knock you down, or you stand right back up in someone else's game."), NoTumble);
            if (ui.Buttons("self.heal", false, new Btn(L.T("Soigner complètement", "Heal to full"), ButtonKind.Primary)) == 0) HealSelf();

            ui.Section(L.T("Déplacement", "Movement"));
            Noclip = ui.Toggle("self.noclip", "Noclip",
                L.T($"Vole à travers les murs. Saut pour monter, accroupi pour descendre. Touche : {Settings.NoclipKey.Value}",
                    $"Fly through walls. Jump to rise, crouch to sink. Key: {Settings.NoclipKey.Value}"), Noclip);
            NoclipSpeed = ui.Slider("self.ncspeed", L.T("Vitesse du noclip", "Noclip speed"), NoclipSpeed, 2f, 30f,
                v => v.ToString("0") + " m/s", step: 1f, desc: L.T("Sprint pour aller 2,5 fois plus vite.", "Sprint to go 2.5 times faster."));
            SpeedMult = ui.Slider("self.speed", L.T("Vitesse de marche et de course", "Walk and sprint speed"), SpeedMult, 0.5f, 4f, X, step: 0.1f);
            JumpMult = ui.Slider("self.jump", L.T("Hauteur de saut", "Jump height"), JumpMult, 0.5f, 4f, X, step: 0.1f);
            GravityMult = ui.Slider("self.gravity", L.T("Gravité", "Gravity"), GravityMult, 0.1f, 2f, X, step: 0.05f);
            AirJump = ui.Toggle("self.airjump", L.T("Saut infini", "Infinite jump"),
                L.T("Saute autant de fois que tu veux en l'air.", "Jump as many times as you like in mid-air."), AirJump);
            if (ui.Buttons("self.resetmove", false, L.T("Vitesse, saut et gravité normaux", "Normal speed, jump and gravity")) == 0)
            {
                SpeedMult = JumpMult = GravityMult = 1f;
                Toasts.Show(L.T("Déplacements remis à la normale.", "Movement back to normal."));
            }

            ui.Section(L.T("Saisie", "Grabbing"));
            GrabRangeMult = ui.Slider("self.grabrange", L.T("Portée du rayon", "Beam reach"), GrabRangeMult, 1f, 5f, X, step: 0.1f);
            // Carried objects are simulated on the host, using the host's copy of each grabber.
            bool host = Game.IsHost;
            GrabStrengthMult = ui.Slider("self.grabstr", L.T("Force de portée", "Carry strength"), GrabStrengthMult, 1f, 6f, X, !host, 0.1f,
                L.T("Soulève les objets lourds et les ennemis plus facilement.", "Lift heavy objects and enemies more easily."));
            ThrowMult = ui.Slider("self.throw", L.T("Force de lancer", "Throw strength"), ThrowMult, 1f, 6f, X, !host, 0.1f);
            NoOvercharge = ui.Toggle("self.overcharge", L.T("Pas de surcharge", "No overcharge"),
                L.T("Tenir un ennemi ne remplit plus la jauge qui finit par exploser.", "Holding an enemy no longer fills the gauge that ends in an explosion."), NoOvercharge);
            ui.Note(L.T("La portée marche partout. La force et le lancer passent par l'hôte : ils ne marchent que si c'est ta partie.",
                "Reach works anywhere. Strength and throwing go through the host, so they only work in your own game."));

            ui.Section(L.T("Caméra", "Camera"));
            FovOverride = ui.Toggle("self.fovon", L.T("Champ de vision personnalisé", "Custom field of view"), null, FovOverride);
            float fov = ui.Slider("self.fov", L.T("Champ de vision", "Field of view"), Fov, 50f, 130f, v => v.ToString("0") + "°", step: 1f);
            if (!Mathf.Approximately(fov, Fov)) { Fov = fov; FovOverride = true; }
        }
    }
}
