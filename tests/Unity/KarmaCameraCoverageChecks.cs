#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;
using KarmaPrototype;
public static class KarmaCameraCoverageChecks
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    public static void CheckView(KarmaGame game, string label)
    {
        var bg = game.World.Find(game.StageIndex == 0 ? "Mountain dojo artwork" : "Trial hall artwork").GetComponent<SpriteRenderer>().bounds;
        var cam = game.GameCamera; var p = cam.transform.position;
        float y = cam.orthographicSize, x = y * cam.aspect;
        if (p.x - x < bg.min.x - 0.01f || p.x + x > bg.max.x + 0.01f || p.y - y < bg.min.y - 0.01f || p.y + y > bg.max.y + 0.01f)
            throw new Exception("Camera leaves painted backdrop: " + label + " camera=" + p + "/" + cam.orthographicSize + " background=" + bg);
    }
    public static string Run()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        float aspect = game.GameCamera.aspect; int checks = 0;
        try
        {
            foreach (int stage in new[] { 0, 1, 3 })
            {
                game.StartRun(); typeof(KarmaGame).GetProperty("StageIndex").SetValue(game, stage);
                typeof(KarmaGame).GetMethod("LoadRoom", F).Invoke(game, new object[] { true });
                var frame = (Bounds)typeof(KarmaGame).GetField("stageBackgroundBounds", F).GetValue(game);
                CheckView(game, "initial frame"); checks++;
                foreach (float ratio in new[] { 4f / 3, 16f / 9, 21f / 9 })
                foreach (float edge in new[] { KarmaStage.ArenaLeft + 1, KarmaStage.ArenaRight - 1 })
                {
                    game.GameCamera.aspect = ratio;
                    game.Player.ResetForRoom(new Vector2(edge, KarmaSpace.PlayerHalfHeight), true);
                    game.Enemies[0].transform.position = new Vector3(edge, KarmaSpace.GuardianGroundY, 0);
                    float combatSize = Mathf.Max(frame.extents.y, 25 / ratio);
                    foreach (bool inherited in new[] { false, true })
                    {
                        typeof(KarmaGame).GetField("guardianInheriting", F).SetValue(game, inherited);
                        typeof(KarmaGame).GetProperty("Phase").SetValue(game, RunPhase.Transition);
                        game.GameCamera.orthographicSize = combatSize;
                        game.GameCamera.transform.position = new Vector3(20, frame.center.y, -10);
                        for (int i = 0; i < 1000; i++)
                        {
                            typeof(KarmaGame).GetMethod("LateUpdate", F).Invoke(game, null);
                            CheckView(game, stage + "/" + ratio + "/edge=" + edge + "/inherit=" + inherited); checks++;
                        }
                        float intended = combatSize * (inherited ? 0.8f : 0.75f);
                        if (Mathf.Abs(game.GameCamera.orthographicSize - intended) > 0.1f) throw new Exception("Camera zoom changed instead of extending scenery");
                    }
                }
            }
            return "PASS: " + checks + " expanded-background edge/zoom coverage assertions, original zoom preserved";
        }
        finally { game.GameCamera.aspect = aspect; game.StartRun(); }
    }
}
#endif
