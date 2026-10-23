using System.Globalization;
using Photon.Pun;
using UnityEngine;

namespace RepoTrainer
{
    /// <summary>Null-safe accessors for the game's singletons and session state.</summary>
    internal static class Game
    {
        /// <summary>A level is loaded and the local robot exists (not the main menu or lobby screens).</summary>
        public static bool InGame
        {
            get
            {
                if (!RunManager.instance || !LevelGenerator.Instance || !LevelGenerator.Instance.Generated) return false;
                if (!PlayerController.instance || !PlayerAvatar.instance || !GameDirector.instance) return false;
                return !SemiFunc.MenuLevel();
            }
        }

        public static bool Multiplayer => GameManager.instance && GameManager.instance.gameMode == 1;

        /// <summary>Host of the room, or playing solo: the machine that runs enemies and physics.</summary>
        public static bool IsHost => !Multiplayer || PhotonNetwork.IsMasterClient;

        public static PlayerController Controller => PlayerController.instance;
        public static PlayerAvatar Me => PlayerAvatar.instance;

        public static Camera Camera
        {
            get
            {
                var c = GameDirector.instance ? GameDirector.instance.MainCamera : null;
                return c ? c : Camera.main;
            }
        }

        public static bool Typing => !SemiFunc.NoTextInputsActive();

        public static string SessionLabel => !InGame ? L.T("Hors partie", "Not in a game")
            : !Multiplayer ? L.T("Solo", "Solo")
            : PhotonNetwork.IsMasterClient ? L.T("Hôte", "Host")
            : L.T("Invité", "Guest");

        public static int PlayerCount => GameDirector.instance ? GameDirector.instance.PlayerList.Count : 0;

        /// <summary>Moves the local robot, cancelling its momentum and any fall damage on the way.</summary>
        public static void TeleportTo(Vector3 pos)
        {
            var pc = Controller;
            if (!pc) return;
            if (pc.playerAvatarScript && pc.playerAvatarScript.isTumbling)
                pc.playerAvatarScript.tumble.TumbleRequest(_isTumbling: false, _playerInput: true);
            if (!pc.rb.isKinematic) pc.rb.velocity = Vector3.zero;
            pc.transform.position = pos;
            pc.rb.position = pos;
            pc.VelocityRelativeNew = Vector3.zero;
            pc.VelocityImpulse = Vector3.zero;
            if (pc.CollisionController) pc.CollisionController.ResetFalling();
            Physics.SyncTransforms();
        }

        /// <summary>A point just in front of the camera, at chest height, for bringing objects over.</summary>
        public static Vector3 InFront(float distance = 1.6f)
        {
            var cam = Camera;
            if (!cam) return Me ? Me.transform.position : Vector3.zero;
            var fwd = cam.transform.forward;
            fwd.y = 0;
            if (fwd.sqrMagnitude < 0.01f) fwd = cam.transform.forward;
            return cam.transform.position + fwd.normalized * distance - Vector3.up * 0.4f;
        }

        public static float Distance(Vector3 p) => Me ? Vector3.Distance(Me.transform.position, p) : 0f;

        /// <summary>The game counts money in thousands: 12 means $12K.</summary>
        public static string Money(int thousands) =>
            "$" + thousands.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", " ") + "K";

        public static string Dollars(float value)
        {
            if (value >= 1000f) return "$" + (value / 1000f).ToString(value >= 10000f ? "0" : "0.0", CultureInfo.InvariantCulture) + "K";
            return "$" + value.ToString("0", CultureInfo.InvariantCulture);
        }

        public static string Meters(float d) => d.ToString("0", CultureInfo.InvariantCulture) + " m";
    }
}
