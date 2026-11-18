using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace KarmaPrototype.EditorTools
{
    public static class KarmaMotionRouteChecks
    {
        const BindingFlags Binding = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static void Call(KarmaCharacterAnimator animator, string method, params object[] args) =>
            typeof(KarmaCharacterAnimator).GetMethod(method, Binding).Invoke(animator, args);

        public static void Run()
        {
            var checks = new List<string> { "form,event,expected,actual" };
            var catalog = Resources.Load<TextAsset>("Motions12/Catalog").text.Split('\n')
                .Where(s => s.Length != 0).Select(s => s.Split('\t')).ToLookup(f => f[0], f => f[1]);
            var stage = new GameObject("Character motion route checks");
            void Check(SpriteRenderer renderer, string form, string action, string motion)
            {
                string actual = AssetDatabase.GetAssetPath(renderer.sprite.texture);
                string expected = "Assets/Arts/Resources/" + (motion == "Idle" ? KarmaArtwork.StageArt + form + "_Idle" : "Motions12/" + form + "/" + motion) + ".png";
                checks.Add(string.Join(",", form, action, motion, actual));
                if (actual != expected) throw new InvalidOperationException(form + "/" + action + " expected " + motion + ", got " + actual);
            }
            try
            {
                var player = KarmaArtwork.Player(stage.transform);
                var animator = player.GetComponent<KarmaCharacterAnimator>();
                var renderer = player.GetComponentInChildren<SpriteRenderer>();
                foreach (var name in KarmaPlayerTimeline.Motions.Select(m => m.Name).Concat(new[] {"Dash", "Transition", "AirSlash"}))
                {
                    animator.PlayPose(name, 1);
                    Check(renderer, "Player", name, name == "Riposte" ? "ParrySuccess" : KarmaPlayerTimeline.Find(name).Name);
                }
                Call(animator, "BeginEndingPoses", (object)new[] {"Riposte", "Walk", "Salute"});
                foreach (var pose in new[] {"Draw", "Sheathe", "Riposte", "Walk", "Salute"})
                {
                    Call(animator, "PresentEndingPose", pose, 0.5f, false, 0f);
                    Check(renderer, "Player", "ending/" + pose, pose);
                }
                Call(animator, "EndEndingPoses");
                foreach (var form in catalog.Select(g => g.Key).Where(f => f != "Master" && f != "Player" && f != "WardFinal_NoShield"))
                {
                    var order = new List<Essence>();
                    string rest = form.Replace("Neutral", "");
                    while (rest.Length > 0)
                    {
                        if (rest == "Final") { order.AddRange(Enum.GetValues(typeof(Essence)).Cast<Essence>().Except(order).ToArray()); break; }
                        string key = new[] {"Wave", "Rise", "Ward"}.First(k => rest.StartsWith(k, StringComparison.Ordinal));
                        order.Add(key == "Wave" ? Essence.SwordWave : key == "Rise" ? Essence.RisingSlash : Essence.Guard);
                        rest = rest.Substring(key.Length);
                    }
                    var art = KarmaArtwork.Guardian(stage.transform, order, order.Count == 3);
                    animator = art.GetComponent<KarmaCharacterAnimator>();
                    renderer = art.GetComponentInChildren<SpriteRenderer>();
                    Check(renderer, form, "spawn", "Idle");
                    bool sword = form.StartsWith("Wave"), rise = form.StartsWith("Rise"), guard = form.StartsWith("Ward");
                    if (sword) Call(animator, "ConfigureSwordPresentation");
                    if (rise) Call(animator, "ConfigureRisingPresentation", form);
                    if (guard) animator.ConfigureHeavyStrike(order);
                    var basic = sword ? EnemyAttack.SWBasic : EnemyAttack.GroundStrike;
                    animator.GuardianPose("Telegraph", basic, false, false);
                    Check(renderer, form, "telegraph", "Windup");
                    art.position += Vector3.right;
                    animator.GuardianPose("Approach", basic, false, false);
                    Check(renderer, form, "approach movement", rise ? "Dash" : "Move");
                    if (guard)
                    {
                        Call(animator, "ShowAutoShield", 0.25f);
                        animator.GuardianPose("Approach", EnemyAttack.GD01, false, false);
                        Check(renderer, form, "automatic guard", "GD01");
                        Call(animator, "ShowAutoShield", 0f);
                    }
                    foreach (var motion in catalog[form])
                    {
                        string id = motion == "UPBasic" || motion == "GDBasic" ? "GroundStrike" : motion;
                        if (!Enum.TryParse(id, out EnemyAttack attack)) continue;
                        animator.GuardianPose("Attack", attack, false, false);
                        Check(renderer, form, "Attack/" + id, motion);
                    }
                    animator.GuardianPose("Recover", EnemyAttack.GroundStrike, false, false);
                    Check(renderer, form, "Recover", sword ? "FollowThrough" : rise ? "Landing" : guard ? "Recover" : "Recovery");
                    animator.GuardianPose("Stunned", EnemyAttack.GroundStrike, false, false);
                    Check(renderer, form, "Stunned", guard ? "Stagger" : "Stun");
                    if (guard)
                    {
                        typeof(KarmaCharacterAnimator).GetField("guardStunBegan", Binding).SetValue(animator, Time.time - 0.5f);
                        animator.GuardianPose("Stunned", basic, false, false);
                        Check(renderer, form, "sustained stun", "Stun");
                    }
                    if (sword || guard)
                    {
                        animator.GuardianPose("Approach", basic, false, true);
                        Check(renderer, form, "armored stance", "Guard");
                    }
                    if (sword)
                    {
                        typeof(KarmaCharacterAnimator).GetProperty("RangedBasicBody", Binding).SetValue(animator, true);
                        animator.GuardianPose("Attack", EnemyAttack.SWBasic, false, false);
                        Check(renderer, form, "ranged basic", "SWBasicRanged");
                        Call(animator,"ShowSwordRetreat");
                        Check(renderer, form, "retreat", "Backstep");
                    }
                    if (form == "RiseFinal")
                    {
                        typeof(KarmaCharacterAnimator).GetProperty("RisingBarrierVisible", Binding).SetValue(animator, true);
                        animator.GuardianPose("Attack", EnemyAttack.UP05, true, false);
                        Check(renderer, form, "external barrier", "UP05_NoFX");
                    }
                    if (form == "WardFinal")
                    {
                        var shield = new GameObject("Thrown shield").AddComponent<SpriteRenderer>();
                        shield.transform.SetParent(art, false);
                        animator.SetGuardTrialShield(shield);
                        animator.GuardianPose("Approach", EnemyAttack.GD05, false, false);
                        Check(renderer, "WardFinal_NoShield", "shield away", "Idle");
                        animator.GuardianPose("Attack", EnemyAttack.GD03, false, false);
                        Check(renderer, "WardFinal_NoShield", "shield release", "GD03Release");
                        art.position += Vector3.right;
                        animator.GuardianPose("Approach", EnemyAttack.GD05, false, false);
                        Check(renderer, "WardFinal_NoShield", "shieldless movement", "Move");
                        animator.GuardianPose("Stunned", EnemyAttack.GD05, false, false);
                        Check(renderer, "WardFinal_NoShield", "shieldless stun", "Stagger");
                        animator.Hurt();
                        animator.GuardianPose("Approach", EnemyAttack.GD05, false, false);
                        Check(renderer, "WardFinal_NoShield", "shieldless hurt", "Hurt");
                        typeof(KarmaCharacterAnimator).GetField("hurtUntil", Binding).SetValue(animator, 0f);
                        animator.PrepareHeavyStrike();
                        Check(renderer, "WardFinal_NoShield", "prepare heavy", "GD05Heavy");
                        animator.EndHeavyStrike();
                        shield.gameObject.SetActive(false);
                        animator.GuardianPose("Approach", EnemyAttack.GD05, false, false);
                        Check(renderer, form, "shield restored", "Idle");
                    }
                    if (rise)
                    {
                        Call(animator, "StartRisingDescent", 0.5f);
                        Call(animator, "LandRisingDescent");
                        animator.GuardianPose("Attack", EnemyAttack.UP01, true, false);
                        Check(renderer, form, "land descent", "UP01Landing");
                    }
                    if (form == "WardWave" || form == "WardFinal")
                    {
                        Call(animator, "CatchGuardShield");
                        animator.GuardianPose("Recover", EnemyAttack.GD03, false, false);
                        Check(renderer, form, "recover thrown shield", "GD03Catch");
                    }
                    if (form == "WardWave")
                    {
                        var thrown = new GameObject("GD03 shield").AddComponent<SpriteRenderer>();
                        thrown.transform.SetParent(art, false);
                        animator.SetGuardThrownShield(thrown);
                        animator.Hurt();
                        animator.GuardianPose("Approach", EnemyAttack.GD03, false, false);
                        Check(renderer, form, "shield away hurt", "GD03ShieldAwayHurt");
                        animator.SetGuardThrownShield(null);
                        typeof(KarmaCharacterAnimator).GetField("hurtUntil", Binding).SetValue(animator, 0f);
                    }
                    Call(animator, "PresentVictory", "Idle", 1f);
                    Check(renderer, form, "victory", "Victory");
                    Call(animator, "BeginGrowth", renderer.sprite, form);
                    foreach (var motion in catalog[form].Where(m => m.StartsWith("Growth", StringComparison.Ordinal)))
                    {
                        if (motion == "GrowthFalter") Call(animator, "GrowthFalter", 0.4f);
                        else if (motion == "GrowthStumble") Call(animator, "GrowthStumble", "Idle", 1, 0.2f);
                        else Call(animator, "ShowGrowthMotion", motion,
                            motion != "GrowthMastery" && motion != "GrowthTechnique", 0.2f);
                        Check(renderer, form, "inheritance/" + motion, motion);
                    }
                    Call(animator, "EndGrowth");
                    if (form.EndsWith("Final"))
                    {
                        Call(animator, "PresentEndingDefeat", 1f);
                        Check(renderer, form, "ending defeat", "FinalDefeat");
                    }
                    else
                    {
                        Call(animator, "BeginGrowth", renderer.sprite, form);
                        string[] revive = {"Death", "RevivePush", "ReviveKneel", "ReviveRise", "ReviveStand", "Idle"};
                        for (int i = 0; i < revive.Length; i++)
                        {
                            Call(animator, "GrowthRevive", i / 5f);
                            Check(renderer, form, "revive/" + i, revive[i]);
                        }
                        Call(animator, "EndGrowth");
                    }
                    animator.Hurt();
                    animator.GuardianPose("Approach", EnemyAttack.GroundStrike, false, false);
                    Check(renderer, form, "interrupt", "Hurt");
                    animator.Die();
                    Check(renderer, form, "death", "Death");
                    UnityEngine.Object.DestroyImmediate(art.gameObject);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stage);
                Directory.CreateDirectory("output/validation/motions12");
                File.WriteAllLines("output/validation/motions12/routing.csv", checks);
            }
            Debug.Log("Verified " + (checks.Count-1) + " character animation routes.");
        }
    }
}
