using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        void LateUpdate()
        {
            if (Player == null) return;
            float halfWidth = GameCamera.orthographicSize * GameCamera.aspect;
            float min = Mathf.Min(20, halfWidth - 1), max = Mathf.Max(20, 41 - halfWidth);
            float x = Mathf.Clamp(Player.Position.x + Player.Facing * 2, min, max);
            var target = new Vector3(x, 4.4f, -10);
            GameCamera.transform.position = Vector3.Lerp(GameCamera.transform.position, target,
                1 - Mathf.Exp(-6 * Time.unscaledDeltaTime));
        }
    }
}
