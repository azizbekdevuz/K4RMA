#if UNITY_EDITOR
using System;
using System.Reflection;
using KarmaPrototype;
using UnityEngine;

public static class KarmaGuardIntegrationChecks
{
    public static void Run(KarmaGame game, Action<bool, string> check)
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var player = game.Player;
        var body = player.GetComponent<Rigidbody2D>();
        for (int i = 0; i < 3; i++)
        {
            player.ResetForRoom(new Vector2(32, KarmaSpace.PlayerHalfHeight + 0.05f), true);
            player.Stun(0.8f); body.linearVelocity = Vector2.up * 24;
            KarmaGuardAirborneHit.Play(game, player);
            var effect = player.GetComponent<KarmaGuardAirborneHit>();
            effect.SendMessage("LateUpdate");
            check(effect.enabled && body.linearVelocity.y == 24, "GD04 feedback preserves launch velocity");
            check(player.GetComponents<KarmaGuardAirborneHit>().Length == 1, "GD04 reuses one presentation component");
            if (i == 0) player.ClearStatus();
            else if (i == 1) player.ResetForRoom(new Vector2(20, KarmaSpace.PlayerHalfHeight), true);
            else
            {
                typeof(KarmaPlayer).GetField("invulnerableUntil", hidden).SetValue(player, 0f);
                check(player.ApplyShock(0.75f), "Sword shock may supersede GD04 presentation");
            }
            effect.SendMessage("LateUpdate");
            check(!effect.enabled, "Status reset, room change and shock discard obsolete GD04 feedback");
        }
        player.ClearStatus();
    }
}
#endif
