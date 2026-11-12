#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

public static partial class KarmaEndingMoonChecks
{
    public static void RunAlternativesBatch()
    { SessionState.SetBool(Key + ".Alternatives", true); RunEndingBatch(); }

    static async Task CheckAlternatives()
    {
        float preference = KarmaPreferences.MusicVolume;
        foreach (var version in new[] { KarmaEndingVersion.Legacy, KarmaEndingVersion.Cycle, KarmaEndingVersion.Lesson })
        {
            game.PreviewEnding(version);
            Check(game.Phase == RunPhase.Ending && game.IsEndingReview, "Review enters complete ending " + version);
            float health = game.Player.Health, damage = game.DamageTaken;
            int captured = 0;
            float[] times = version == KarmaEndingVersion.Legacy ? new[] { 6.35f, 9f, 11.6f, 18.5f, 23.5f }
                : version == KarmaEndingVersion.Cycle ? new[] { 6f, 12f, 15f, 18f, 22f }
                : new[] { 6.7f, 10.5f, 12f, 15f, 20.2f };
            while (!game.EndingCompleted)
            {
                if (captured < times.Length && game.EndingTime >= times[captured])
                {
                    Render(version + "-" + captured);
                    ScreenCapture.CaptureScreenshot(Output + "/" + version + "-" + captured + "-game.png");
                    captured++;
                }
                await Task.Delay(10);
                if (runtimeError != null) throw new Exception(runtimeError);
            }
            Check(captured == times.Length, "All distinct stages exercised " + version);
            Check(game.Phase == RunPhase.Victory && game.EndingTime == game.ActiveEndingDuration, "Correct completion duration " + version);
            Check(game.Player.Health == health && game.DamageTaken == damage, "Presentation does not damage player " + version);
            Check(game.Projectiles.Count == 0, "No gameplay projectile created " + version);
            await Task.Delay(100);
            Check(game.World.GetComponentsInChildren<KarmaEndingPresentation>().Length == 0, "Presentation cleanup " + version);
            Check(KarmaPreferences.MusicVolume == preference, "Music preference unchanged " + version);
            game.PreviewEnding(version);
            await Until(() => game.EndingTime >= 1);
            game.SkipEndingReview();
            await Until(() => game.EndingCompleted);
            Check(game.Phase == RunPhase.Victory, "Skip/replay " + version);
        }
        game.PreviewEnding(KarmaEndingVersion.Legacy);
        await Until(() => game.EndingTime >= 6.2f);
        var animator = game.Player.GetComponentInChildren<KarmaCharacterAnimator>();
        var picture = (SpriteRenderer)Get(animator, "picture");
        Call(animator, "PresentEndingReviewPose", "SwordWave", .101f, false);
        var first = picture.sprite;
        Call(animator, "PresentEndingReviewPose", "SwordWave", .12f, false);
        Check(first == picture.sprite, "Native24 holds within one sample");
        Call(animator, "PresentEndingReviewPose", "SwordWave", .14f, false);
        Check(first != picture.sprite, "Native24 advances at next 1/24-second sample");
        game.TogglePause(); float held = game.EndingTime;
        await Task.Delay(200); Check(game.EndingTime == held, "Ending pause freezes timeline");
        game.TogglePause();
        game.PreviewEnding(KarmaEndingVersion.Cycle);
        await Until(() => game.EndingTime >= 1);
        Check(game.World.GetComponentsInChildren<KarmaEndingPresentation>().Length == 1, "Switching leaves one presentation");
        game.ReturnToTitle(); await Task.Delay(100);
        Check(game.Phase == RunPhase.Title && !game.IsEndingReview && !game.EndingCompleted, "Title clears preview state");
        Reset(Essence.SwordWave);
        enemy.TakeDamage(enemy.MaxHealth * 100);
        Check(game.Phase == RunPhase.Ending && game.EndingVersion == KarmaEndingVersion.Campaign
            && game.ActiveEndingDuration == 40, "Real victory keeps original campaign ending");
        game.ReturnToTitle();
    }
}
#endif
