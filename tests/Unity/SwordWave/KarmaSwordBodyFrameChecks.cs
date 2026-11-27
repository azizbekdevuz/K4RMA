using System;
using System.Linq;
using System.Reflection;
using KarmaPrototype;
using UnityEditor;
using UnityEngine;

public static class KarmaSwordBodyFrameChecks
{
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static object Call(KarmaCharacterAnimator actor, string method, params object[] args)
        => typeof(KarmaCharacterAnimator).GetMethod(method, Instance).Invoke(actor, args);

    [MenuItem("K4RMA/검기형/스킬 갑옷 색상 검사")]
    public static void CheckArmourPalette()
    {
        int checkedFrames = 0, maxError = 0;
        foreach (string form in new[] { "Wave", "WaveRise", "WaveWard", "WaveFinal" })
        {
            var reference = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Arts/Reference/" + form + "_Idle.png");
            var target = ArmourMedian(ReadPixels(reference.texture), reference);
            string folder = "Assets/Arts/SwordWave/" + form + "/";
            foreach (string motion in new[] { "Idle", "Move", "Backstep", "Guard", "Windup", "FollowThrough",
                "BasicDowncut", "CastWindup", "CastDowncut", "GroundLift", "GroundReverse", "ShieldRaise",
                "SWBasic", "SWBasicRanged", "SW01", "SW02", "SW03", "SW04", "SW05" })
            {
                string path = folder + motion + ".png";
                if (!System.IO.File.Exists(path)) continue;
                var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Where(s => s.name != "BasicBlade").ToArray();
                if (frames.Length == 0) throw new InvalidOperationException(path + " has no body sprites");
                var pixels = ReadPixels(frames[0].texture);
                foreach (var frame in frames)
                {
                    var actual = ArmourMedian(pixels, frame);
                    int error = Mathf.Max(Math.Abs(actual.r - target.r), Math.Abs(actual.g - target.g), Math.Abs(actual.b - target.b));
                    if (error > 15) throw new InvalidOperationException(path + "/" + frame.name + " armour " + actual + ", idle " + target);
                    maxError = Math.Max(maxError, error);
                    checkedFrames++;
                }
            }
        }
        Debug.Log($"Sword armour palette passed: {checkedFrames} imported body drawings, maximum RGB channel difference {maxError}/255.");
    }

    static Color32[] ReadPixels(Texture2D texture)
    {
        var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        bool writeSRGB = GL.sRGBWrite;
        try
        {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(texture, target);
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            return image.GetPixels32();
        }
        finally
        {
            GL.sRGBWrite = writeSRGB;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            RenderTexture.ReleaseTemporary(target);
        }
    }

    static Color32 ArmourMedian(Color32[] pixels, Sprite frame)
    {
        Rect rect = frame.rect;
        int[,] bins = new int[3, 256];
        int count = 0;
        for (int y = (int)rect.yMin; y < (int)rect.yMax; y += 3)
        for (int x = (int)rect.xMin; x < (int)rect.xMax; x += 3)
        {
            var p = pixels[y * frame.texture.width + x];
            if (p.a < 224 || p.r - p.g < 12 || p.g - p.b < 12) continue;
            bins[0, p.r]++; bins[1, p.g]++; bins[2, p.b]++;
            count++;
        }
        if (count < 32) throw new InvalidOperationException("Missing armour on " + frame.name);
        byte Median(int channel)
        {
            int sum = 0;
            for (int i = 0; i < 256; i++)
            {
                sum += bins[channel, i];
                if (2 * sum >= count) return (byte)i;
            }
            return 255;
        }
        return new Color32(Median(0), Median(1), Median(2), 255);
    }

    [MenuItem("K4RMA/검기형/실제 부활 연결 검사")]
    public static void CheckLiveRevival()
    {
        var game = UnityEngine.Object.FindAnyObjectByType<KarmaGame>();
        if (!EditorApplication.isPlaying || game == null || !game.IsCombat || game.Enemies.Count != 1)
            throw new InvalidOperationException("Start a SwordWave practice encounter before checking revival.");
        var enemy = game.Enemies[0];
        var form = enemy.GetComponentInChildren<KarmaCharacterAnimator>();
        string previous = (string)typeof(KarmaCharacterAnimator).GetField("motionForm", Instance).GetValue(form);
        if (previous != "Wave" && previous != "WaveWard" && previous != "WaveRise")
            throw new InvalidOperationException("Revival requires a non-final SwordWave guardian.");
        game.StartCoroutine(ObserveRevival(game, enemy, previous));
    }

    static System.Collections.IEnumerator ObserveRevival(KarmaGame game, KarmaEnemy enemy, string previous)
    {
        float deadline = Time.realtimeSinceStartup + 75;
        enemy.TakeDamage(enemy.MaxHealth * 100);
        bool chose = false, rested = false, pushed = false, stood = false;
        var rendered = new WaitForEndOfFrame();
        while (!stood && Time.realtimeSinceStartup < deadline)
        {
            yield return rendered;
            if (!chose && game.Phase == RunPhase.Choosing)
            {
                game.Choose(previous == "WaveWard" ? Essence.RisingSlash : Essence.Guard);
                game.ConfirmChoice();
                chose = true;
            }
            if (!game.GuardianInheriting || game.Enemies.Count == 0) continue;
            var actor = game.Enemies[0].GetComponentInChildren<KarmaCharacterAnimator>();
            var sprite = actor.GetComponentInChildren<SpriteRenderer>().sprite;
            if (actor.CurrentPose == "Growth/Revive/0")
            {
                if (sprite != KarmaArtResource.Frames("Motions12/" + previous + "/Death").Last())
                    throw new InvalidOperationException(previous + " stood up before the first push-up");
                rested = true;
            }
            if (actor.CurrentPose == "Growth/Revive/1") pushed |= sprite.name.StartsWith("RevivePush_");
            if (actor.CurrentPose == "Growth/Revive/4") stood = sprite.name.StartsWith("ReviveStand_");
        }
        if (!rested || !pushed || !stood) throw new InvalidOperationException("Incomplete live revival: " + previous);
        Debug.Log("Sword live revival passed: " + previous + " collapse, push-up and standing poses.");
    }

    [MenuItem("K4RMA/검기형/몸 동작 프레임 검사")]
    public static void Run()
    {
        var stage = new GameObject("Sword body frame checks");
        stage.transform.position = Vector3.up * 300;
        int total = 0;
        try
        {
            var rows = Resources.Load<TextAsset>("Motions12/Catalog").text.Split('\n')
                .Where(line => line.StartsWith("Wave", StringComparison.Ordinal)).Select(line => line.Split('\t'));
            foreach (var form in rows.GroupBy(row => row[0]))
            {
                var order = new System.Collections.Generic.List<Essence> { Essence.SwordWave };
                if (form.Key == "WaveRise" || form.Key == "WaveFinal") order.Add(Essence.RisingSlash);
                if (form.Key == "WaveWard" || form.Key == "WaveFinal") order.Add(Essence.Guard);
                var art = KarmaArtwork.Guardian(stage.transform, order, order.Count == 3);
                var actor = art.GetComponent<KarmaCharacterAnimator>();
                var picture = art.GetComponentInChildren<SpriteRenderer>();
                Call(actor, "ConfigureSwordPresentation");
                void Expect(string motion, int frame)
                {
                    var sprite = picture.sprite;
                    string path = motion == "Idle" ? "Assets/Arts/Reference/" + form.Key + "_Idle.png"
                        : "Assets/Arts/SwordWave/" + form.Key + "/" + motion + ".png";
                    if (sprite == null || AssetDatabase.GetAssetPath(sprite.texture) != path ||
                        motion != "Idle" && sprite.name != motion + "_" + frame.ToString("00"))
                        throw new InvalidOperationException(form.Key + "/" + motion + " frame " + frame + ", got " + sprite);
                }
                foreach (var row in form)
                {
                    string motion = row[1];
                    for (int i = 0; i < int.Parse(row[4]); i++)
                    {
                        Call(actor, "DrawMotion", form.Key, motion, (i + 0.5f) / 12, false);
                        Expect(motion, i);
                        total++;
                    }
                }
                Call(actor, "DrawMotion", form.Key, "Move", (6 + 0.5f) / 12, true);
                Expect("Move", 0);
                actor.GuardianPose("Approach", EnemyAttack.SWBasic, false, false);
                art.position += Vector3.right;
                actor.GuardianPose("Approach", EnemyAttack.SWBasic, false, false);
                Expect("Move", 0);
                float stride = (float)typeof(KarmaCharacterAnimator).GetField("swordStride", Instance).GetValue(actor)
                    * Mathf.Abs(art.lossyScale.x);
                for (int i = 1; i <= 6; i++)
                {
                    Call(actor, "SampleSwordLocomotion", stride / 6, KarmaBootstrap.Step, 1, true);
                    var planted = picture.sprite;
                    Call(actor, "SampleSwordLocomotion", 0f, 1f / 144, 1, true);
                    if (picture.sprite != planted)
                        throw new InvalidOperationException(form.Key + " changed support foot between physics ticks");
                }
                Call(actor, "ShowSwordRetreat");
                Expect("Backstep", 0);
                Call(actor, "BeginSW02Presentation", 1f);
                Call(actor, "LockSW02Presentation", 0.5f);
                Call(actor, "ReleaseSW02Presentation");
                if (form.Key == "Wave")
                {
                    if (picture.sprite != Resources.Load<KarmaSwordBasicArt>("SwordWave/Cast/Wave").poses[11])
                        throw new InvalidOperationException("Basic SW02 did not release on its downcut");
                }
                else Expect("CastDowncut", 4);
                Call(actor, "CancelSwordBodyPresentation");
                if (order.Contains(Essence.Guard))
                {
                    foreach (var cast in new[] { EnemyAttack.SW01, EnemyAttack.SW02 })
                    {
                        actor.GuardianPose("Attack", cast, false, false);
                        Expect("Guard", 0);
                    }
                    Call(actor, "PrepareSwordShieldRaise", 0.7f);
                    actor.GuardianPose("Telegraph", EnemyAttack.SW04, false, false);
                    void ExpectShieldRaise(int index)
                    {
                        var shared = KarmaArtResource.Frames("SwordWave/WaveWardShieldRaise");
                        if (picture.sprite != shared[index])
                            throw new InvalidOperationException(form.Key + " missed shield raise frame " + index);
                        var plate = picture.transform.Find("Shield mantle plate");
                        if ((plate != null && plate.GetComponent<SpriteRenderer>().enabled) != (form.Key == "WaveFinal"))
                            throw new InvalidOperationException(form.Key + " has the wrong shield raise equipment");
                    }
                    ExpectShieldRaise(0);
                    Call(actor, "ActivateSwordShieldRaise");
                    ExpectShieldRaise(7);
                    actor.GuardianPose("Recover", EnemyAttack.SW04, false, true);
                    ExpectShieldRaise(7);
                    Call(actor, "PrepareSwordBody", "Counter", 1f, 0);
                    actor.GuardianPose("Telegraph", EnemyAttack.SW04, false, true);
                    Expect("SW04", 0);
                    Call(actor, "ReleaseSwordBody", "Counter", 0);
                    actor.GuardianPose("Approach", EnemyAttack.SW04, false, true);
                    Expect("SW04", 2);
                }
                if (order.Contains(Essence.RisingSlash))
                {
                    Call(actor, "BeginSwordGroundStrike", 1f);
                    Call(actor, "SampleSwordGroundStrike", 0.95f);
                    actor.GuardianPose("Telegraph", EnemyAttack.SW03, false, false);
                    var plunge = Resources.Load<KarmaSwordBasicArt>("SwordWave/Ground/" + form.Key);
                    if (picture.sprite != plunge.poses[3])
                        throw new InvalidOperationException(form.Key + " missed the SW03 descent");
                    Call(actor, "ReleaseSwordGroundStrike");
                    actor.GuardianPose("Attack", EnemyAttack.SW03, false, false);
                    if (picture.sprite != plunge.poses[4])
                        throw new InvalidOperationException(form.Key + " missed the SW03 plant");
                }
                if (order.Count == 3)
                {
                    var cast = Resources.Load<KarmaSwordBasicArt>("SwordWave/Cast/WaveFinal");
                    void ExpectCast(int frame)
                    {
                        var source = cast.poses[frame];
                        if (picture.sprite.name != source.name || picture.sprite.texture != source.texture)
                            throw new InvalidOperationException("SW05 expected SW02 pose " + frame + ", got " + picture.sprite);
                    }
                    Call(actor, "PrepareSwordBody", "UltimateHold", 1f, 0);
                    actor.GuardianPose("Attack", EnemyAttack.SW05, true, true);
                    Expect("SW05", 0);
                    for (int group = 0; group < 3; group++)
                    {
                        if (group > 0)
                            typeof(KarmaCharacterAnimator).GetField("swordRainCutAt", Instance).SetValue(actor, Time.time - 1f);
                        Call(actor, "PrepareSwordBody", "UltimateWindup", 1f, group);
                        typeof(KarmaCharacterAnimator).GetField("swordBodyAt", Instance).SetValue(actor, Time.time - 0.5f);
                        Call(actor, "SampleSwordGroundStrike", 0.5f);
                        actor.GuardianPose("Attack", EnemyAttack.SW05, true, true);
                        ExpectCast(2);
                        Call(actor, "SampleSwordGroundStrike", 0.96f);
                        actor.GuardianPose("Attack", EnemyAttack.SW05, true, true);
                        ExpectCast(3);
                        Call(actor, "ReleaseSwordBody", "UltimateCommand", group);
                        actor.GuardianPose("Attack", EnemyAttack.SW05, true, true);
                        ExpectCast(4);
                    }
                    Call(actor, "PrepareSwordBody", "UltimateCharge", 0.6f, 0);
                    actor.GuardianPose("Attack", EnemyAttack.SW05, true, true);
                    ExpectCast(4);
                    typeof(KarmaCharacterAnimator).GetField("swordRainCutAt", Instance).SetValue(actor, Time.time - 1f);
                    Call(actor, "ReleaseSwordBody", "UltimateFinish", 0);
                    actor.GuardianPose("Attack", EnemyAttack.SW05, true, true);
                    if (picture.sprite != cast.idle) throw new InvalidOperationException("SW05 detonation repeated a downcut");
                    Call(actor, "PrepareSwordBody", "UltimateRecover", 0.7f, 0);
                    actor.GuardianPose("Attack", EnemyAttack.SW05, true, false);
                    Expect("FollowThrough", 0);
                }
                Call(actor, "PresentVictory", "Recovery", 0.25f);
                Expect("Victory", 2);
                actor.GuardianPose("Stunned", EnemyAttack.SWBasic, false, false);
                Expect("Stun", 0);
                actor.Hurt();
                actor.GuardianPose("Approach", EnemyAttack.SWBasic, false, false);
                Expect("Hurt", 0);
                actor.Die();
                Expect("Death", 0);
                if (order.Count < 3)
                {
                    Call(actor, "BeginGrowth", picture.sprite, form.Key);
                    string[] revive = { "Death", "RevivePush", "ReviveKneel", "ReviveRise", "ReviveStand", "Idle" };
                    for (int i = 0; i < revive.Length; i++)
                    {
                        Call(actor, "GrowthRevive", i / 5f);
                        Call(actor, "PresentSwordCeremony");
                        Expect(revive[i], i == 0 ? 3 : 0);
                    }
                    Call(actor, "EndGrowth");
                    actor.Empower(Essence.SwordWave, 1f, previousForm: form.Key);
                    Call(actor, "PresentSwordCeremony");
                    Expect("Death", 3);
                }
                else
                {
                    Call(actor, "PresentEndingDefeat", 1f);
                    Expect("FinalDefeat", 3);
                }
                UnityEngine.Object.DestroyImmediate(art.gameObject);
            }
            Debug.Log("Sword body frame checks passed: " + total + " drawings, four forms and combat transitions.");
        }
        finally { UnityEngine.Object.DestroyImmediate(stage); }
    }
}
