using System.Collections.Generic;
using UnityEngine;

namespace RepoTrainer.UI
{
    /// <summary>
    /// Frame-rate independent exponential smoothing keyed by id. Values only advance on
    /// Repaint so the several OnGUI passes per frame don't speed animations up.
    /// </summary>
    internal static class Anim
    {
        private static readonly Dictionary<string, float> Values = new Dictionary<string, float>();

        public static float To(string id, float target, float speed = 14f)
        {
            if (!Values.TryGetValue(id, out var cur))
            {
                Values[id] = target;
                return target;
            }
            if (cur == target) return cur;
            var e = Event.current;
            if (e == null || e.type == EventType.Repaint)
            {
                cur = Mathf.Lerp(cur, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
                if (Mathf.Abs(cur - target) < 0.0005f) cur = target;
                Values[id] = cur;
            }
            return cur;
        }

        public static void Snap(string id, float value) => Values[id] = value;

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t -= 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }
    }
}
