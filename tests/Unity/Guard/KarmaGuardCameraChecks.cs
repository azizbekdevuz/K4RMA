#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;
public static class KarmaGuardCameraChecks
{
    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>(); game.StartRun();
        foreach (var kind in new[] { Essence.Guard, Essence.SwordWave, Essence.RisingSlash })
        { game.Player.Essences.Preview(kind); game.Player.Essences.Confirm(out _); }
        var f = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, 3);
        typeof(KarmaGame).GetMethod("LoadRoom", f).Invoke(game, new object[] { true });
        var enemy = game.Enemies[0]; var b = typeof(KarmaEnemy).GetField("behavior", f).GetValue(enemy);
        b.GetType().GetField("PracticeHold", f).SetValue(b, true);
        typeof(KarmaEnemy).GetField("engaged", f).SetValue(enemy, true);
        typeof(KarmaEnemy).GetField("selected", f).SetValue(enemy, EnemyAttack.GD05);
        typeof(KarmaEnemy).GetField("running", f).SetValue(enemy, true);
        typeof(KarmaEnemy).GetField("patternStarted", f).SetValue(enemy, enemy.FightSeconds);
        enemy.StartCoroutine((System.Collections.IEnumerator)typeof(KarmaEnemy).GetMethod("RunPattern", f).Invoke(enemy, null));
        double until = EditorApplication.timeSinceStartup + 20;
        while (!enemy.TrialActive) { if (EditorApplication.timeSinceStartup > until) throw new Exception("GD05 camera did not activate"); await Task.Delay(10); }
        float originalAspect = game.GameCamera.aspect; int checks = 0;
        try
        {
            foreach (float aspect in new[] { 4f / 3, 16f / 9, 21f / 9 })
            {
                game.GameCamera.aspect = aspect;
                for (int i = 0; i < 1000; i++)
                {
                    typeof(KarmaGame).GetMethod("LateUpdate", f).Invoke(game, null);
                    KarmaCameraCoverageChecks.CheckView(game, "GD05/" + aspect); checks++;
                }
                var frame = (Bounds)typeof(KarmaGame).GetField("stageBackgroundBounds", f).GetValue(game);
                float intended = Mathf.Max(frame.extents.y, 25 / aspect, 16, 22 / aspect);
                if (Mathf.Abs(game.GameCamera.orthographicSize - intended) > 0.1f) throw new Exception("GD05 zoom was changed");
            }
            return "PASS: " + checks + " GD05 expanded-background camera coverage assertions, original zoom preserved";
        }
        finally { game.GameCamera.aspect = originalAspect; typeof(KarmaEnemy).GetMethod("InterruptPattern", f).Invoke(enemy, null); game.StartRun(); }
    }
}
#endif
