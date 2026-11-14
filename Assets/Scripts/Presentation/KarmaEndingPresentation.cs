using UnityEngine;

namespace KarmaPrototype
{
    // This presentation owns only ending visuals. Combat and progression stay with KarmaGame.
    public sealed class KarmaEndingPresentation : MonoBehaviour
    {
        KarmaGame game;
        Transform fallen;
        KarmaCharacterAnimator fallenAnimator;
        SpriteRenderer[] fallenPictures;
        readonly SpriteRenderer[] symbols = new SpriteRenderer[3];
        readonly SpriteRenderer[] particles = new SpriteRenderer[48];
        readonly Vector3[] particleOrigins = new Vector3[48];
        const float MasterHeight = 4.8f;
        const int MasterColumns = 24, MasterRows = 28;
        static readonly Color MasterMoonlight = new Color(0.84f, 0.9f, 1f);
        static readonly Color MasterWarmLight = new Color(1f, 0.94f, 0.78f);
        static readonly string[] masterPoseNames = { "MasterLift", "MasterTeach" };
        static readonly float[] masterFootPixels = { 16, 9 };
        readonly SpriteRenderer[] memories = new SpriteRenderer[KarmaGame.EndingMemoryCount];
        readonly Vector3[] symbolSizes = new Vector3[3];
        const float EmblemRadius = 3.2f;
        readonly SpriteRenderer[] mist = new SpriteRenderer[2];
        readonly Sprite[] masterPoses = new Sprite[masterPoseNames.Length];
        SpriteRenderer master, aura, backdrop, emblemCore;
        float coreScale;
        Sprite masterIdle;
        Mesh masterMesh;
        MeshRenderer masterMotion;
        MaterialPropertyBlock masterProperties;
        Vector3[] masterVertices;
        float lastMasterFrame = -1;
        readonly SpriteRenderer[] driftingLights = new SpriteRenderer[20];
        Color backdropColor;
        bool originalBackdrop;
        readonly Vector3[] techniqueSizes = new Vector3[3];
        readonly SpriteRenderer[] techniques = new SpriteRenderer[3];
        SpriteRenderer counterSpark;
        const int RibbonPoints = 28;
        readonly LineRenderer[] oathRibbons = new LineRenderer[3];
        readonly Vector3[] ribbonPoints = new Vector3[RibbonPoints];
        static readonly Color OathLight = new Color(1f, 0.85f, 0.57f);
        MaterialPropertyBlock dissolveBlock;
        bool seeded, guardianFinished;

        public static KarmaEndingPresentation Create(KarmaGame game, Transform world, Transform fallen, Vector3 origin,
            Sprite[] defeated)
        {
            var root = new GameObject("Ending presentation"); root.transform.SetParent(world, false);
            var ending = root.AddComponent<KarmaEndingPresentation>();
            ending.game = game; ending.fallen = fallen;
            Sprite finalDefeat = null;
            if (fallen != null)
            {
                fallen.position = origin;
                ending.fallenAnimator = fallen.GetComponent<KarmaCharacterAnimator>();
                ending.fallenPictures = fallen.GetComponentsInChildren<SpriteRenderer>();
                if (ending.fallenPictures.Length > 0) finalDefeat = ending.fallenPictures[0].sprite;
                ending.fallenAnimator?.PresentEndingDefeat(0);
                var material = Resources.Load<Material>("Shared/Rendering/KarmaEndingDissolve");
                if (material != null) foreach (var picture in ending.fallenPictures) picture.sharedMaterial = material;
                ending.dissolveBlock = new MaterialPropertyBlock();
            }
            var mistSprite = Resources.Load<Sprite>("Ending/MoonMist");
            for (int i = 0; i < ending.symbols.Length; i++)
            {
                ending.symbols[i] = ending.Layer("Personalized technique " + (Essence)i,
                    KarmaArtwork.SkillIcon((Essence)i), 1.75f, 18);
                ending.symbolSizes[i] = ending.symbols[i].transform.localScale;
            }
            for (int i = 0; i < ending.particles.Length; i++)
                ending.particles[i] = ending.Layer("Guardian light " + i,
                    KarmaArtwork.Sprite("PersonalizationVFX", "Core"), 0.16f + i % 3 * 0.04f, 19);
            ending.aura = ending.Layer("Completed aura", KarmaArtwork.Sprite("PlayerVFX", "Barrier"), 5.5f, 4);
            ending.emblemCore = ending.Layer("Merged emblem light", KarmaArtwork.Sprite("PersonalizationVFX", "Core"), 1.45f, 19);
            ending.coreScale = ending.emblemCore.transform.localScale.x;
            for (int i = 0; i < ending.oathRibbons.Length; i++)
                ending.oathRibbons[i] = ending.CreateOathRibbon((Essence)i);
            ending.counterSpark = ending.Layer("Counter contact spark", KarmaArtwork.Sprite("HitVFX", "Burst"), 1.35f, 17);
            ending.techniques[(int)Essence.Guard] = ending.Layer("Counter demonstration",
                KarmaArtwork.Sprite("PersonalizationVFX", "Counter"), 3.1f, 12);
            ending.techniques[(int)Essence.RisingSlash] = ending.Layer("Aerial slash demonstration",
                KarmaArtwork.Sprite("PlayerVFX", "AirSlash"), 3.6f, 13);
            ending.techniques[(int)Essence.SwordWave] = ending.Layer("Pierce trail demonstration",
                KarmaArtwork.Sprite("PlayerVFX", "PierceTrail"), 5.4f, 14);
            for (int i = 0; i < ending.techniques.Length; i++) ending.techniqueSizes[i] = ending.techniques[i].transform.localScale;
            var acquired = game.Player.Essences.PersonalizationOrder;
            ending.memories[0] = ending.Layer("Memory of the first guardian", KarmaArtwork.Sprite("Neutral", "Death") ?? defeated[0], 5f, 1);
            if (acquired.Count >= 1)
            {
                var baseForm = KarmaArtwork.GuardianFormName(new[] { acquired[0] });
                ending.memories[1] = ending.Layer("Memory of the second guardian", KarmaArtwork.Sprite(baseForm, "Death") ?? defeated[1], 5f, 1);
            }
            if (acquired.Count >= 2)
            {
                var inheritedForm = KarmaArtwork.GuardianFormName(new[] { acquired[0], acquired[1] });
                ending.memories[2] = ending.Layer("Memory of the third guardian",
                    FindSprite(KarmaArtwork.InheritedPoses(inheritedForm), "Death") ?? defeated[2], 5f, 1);
            }
            string finalForm = KarmaArtwork.GuardianFormName(acquired);
            ending.memories[3] = ending.Layer("Memory of the final guardian",
                FindSprite(KarmaArtwork.InheritedPoses(finalForm), "Death") ?? finalDefeat, 5f, 1);
            for (int i = 0; i < ending.mist.Length; i++)
                ending.mist[i] = ending.Layer("Ending ground mist " + i, mistSprite, i == 0 ? 42f : 30f, -7 + i);
            var background = world.Find("Trial hall artwork") ?? world.Find("Mountain dojo artwork");
            if (background != null && background.TryGetComponent(out ending.backdrop))
            {
                ending.backdropColor = ending.backdrop.color;
                ending.originalBackdrop = true;
            }
            ending.master = KarmaTutorialStage.Picture(root.transform, "Master",
                new Vector2(KarmaGame.EndingMasterX, KarmaTutorialStage.MasterGroundCenter(MasterHeight)),
                new Vector2(4.39f, MasterHeight), 10);
            ending.masterIdle = ending.master.sprite;
            for (int i = 0; i < ending.masterPoses.Length; i++)
                ending.masterPoses[i] = Resources.Load<Sprite>("Ending/" + masterPoseNames[i]);
            ending.master.enabled = false;
            ending.CreateMasterMotion();
            for (int i = 0; i < ending.driftingLights.Length; i++)
                ending.driftingLights[i] = ending.Layer("Ending air light " + i,
                    KarmaArtwork.Sprite("PersonalizationVFX", "Core"), 0.08f + 0.03f * (i % 4), 2 + i % 2);
            return ending;
        }

        static Sprite FindSprite(Sprite[] sprites, string name)
        {
            foreach (var sprite in sprites)
                if (sprite.name == name) return sprite;
            return null;
        }

        void OnDisable()
        {
            if (originalBackdrop && backdrop != null) backdrop.color = backdropColor;
        }

        void OnDestroy()
        {
            if (masterMesh != null) Destroy(masterMesh);
        }

        void CreateMasterMotion()
        {
            var layer = new GameObject("Master performance");
            layer.transform.SetParent(transform, false);
            layer.transform.localPosition = new Vector3(KarmaGame.EndingMasterX, 0, 0);
            masterMotion = layer.AddComponent<MeshRenderer>();
            masterMotion.sharedMaterial = KarmaVisuals.DefaultMaterial;
            masterMotion.sortingOrder = master.sortingOrder;
            masterProperties = new MaterialPropertyBlock();
            masterMesh = new Mesh { name = "Ending master performance" };
            masterMesh.MarkDynamic();
            masterVertices = new Vector3[(MasterColumns + 1) * (MasterRows + 1)];
            var uv = new Vector2[masterVertices.Length];
            var indices = new int[MasterColumns * MasterRows * 6];
            var colors = new Color32[masterVertices.Length];
            int index = 0;
            for (int y = 0; y <= MasterRows; y++)
            for (int x = 0; x <= MasterColumns; x++)
            {
                int point = y * (MasterColumns + 1) + x;
                uv[point] = new Vector2((float)x / MasterColumns, (float)y / MasterRows);
                colors[point] = new Color32(255, 255, 255, 255);
                if (x == MasterColumns || y == MasterRows) continue;
                int top = point + MasterColumns + 1;
                indices[index++] = point;
                indices[index++] = top;
                indices[index++] = point + 1;
                indices[index++] = point + 1;
                indices[index++] = top;
                indices[index++] = top + 1;
            }
            masterMesh.vertices = masterVertices;
            masterMesh.uv = uv;
            masterMesh.colors32 = colors;
            masterMesh.triangles = indices;
            masterMesh.bounds = new Bounds(new Vector3(0, 2.4f, 0), new Vector3(7, 7, 1));
            layer.AddComponent<MeshFilter>().sharedMesh = masterMesh;
        }

        SpriteRenderer Layer(string name, Sprite sprite, float size, int order)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
            renderer.sortingOrder = order; KarmaVisuals.ApplyMaterial(renderer);
            if (sprite != null) child.transform.localScale = Vector3.one * (size / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
            renderer.color = Color.clear;
            return renderer;
        }
        LineRenderer CreateOathRibbon(Essence essence)
        {
            var child = new GameObject("Oath ribbon " + essence);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = KarmaCombatVFX.GlowMaterial ?? KarmaVisuals.DefaultMaterial;
            line.positionCount = RibbonPoints;
            line.sortingOrder = 4;
            line.widthCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.7f, 0.65f), new Keyframe(1, 0.02f));
            line.enabled = false;
            return line;
        }

        static float Ease(float start, float end, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, t));
        void LateUpdate()
        {
            if (game == null || game.Player == null || game.Phase != RunPhase.Ending) return;
            float t = game.EndingTime;
            float poseTime = Mathf.Floor(t * 24f + 0.001f) / 24f;
            game.Player.PresentEnding(t);
            DrawGuardian(t);
            DrawAtmosphere(t);
            DrawMemories(game.EndingMemoryTime);
            DrawMaster(poseTime);
            DrawAir(t);

            var center = game.Player.Position + Vector2.up * 1.05f;
            if (t < KarmaGame.EndingGatherAt)
            {
                ShowTechniqueSymbol(Essence.SwordWave, center, t, KarmaGame.EndingPierceAt - 0.08f, KarmaGame.EndingAirJumpAt - 0.04f);
                ShowTechniqueSymbol(Essence.RisingSlash, center + Vector2.up * game.Player.EndingAirHeight, t,
                    KarmaGame.EndingAirJumpAt, KarmaGame.EndingCounterAt - 0.18f);
                ShowTechniqueSymbol(Essence.Guard, center, t, KarmaGame.EndingCounterAt - 0.18f, KarmaGame.EndingGatherAt);
            }
            else DrawEmblemAbsorption(center, t);

            float counterAge = t - (KarmaGame.EndingCounterAt + KarmaGame.EndingRelease);
            counterSpark.transform.position = center + new Vector2(Mathf.Lerp(3.9f, 1.2f, Ease(-0.26f, 0, counterAge)), 0.6f);
            counterSpark.color = new Color(1, 0.84f, 0.62f, Ease(-0.26f, 0, counterAge) * (1 - Ease(0, 0.13f, counterAge)));
            ShowTechnique((int)Essence.Guard, center + new Vector2(1.35f, 0.55f), counterAge, 0.5f, 0);
            ShowTechnique((int)Essence.RisingSlash, center + new Vector2(1.18f, game.Player.EndingAirHeight + 0.7f),
                t - (KarmaGame.EndingAirSlashAt + KarmaGame.EndingRelease), 0.39f, 24f);
            float pierceAge = t - KarmaGame.EndingPierceReleaseAt;
            var dashPath = new Vector2((game.Player.EndingPierceStartX + center.x) * 0.5f, center.y + 0.35f);
            ShowTechnique((int)Essence.SwordWave, dashPath, pierceAge, 0.32f, 0);

            float absorbed = Ease(KarmaGame.EndingAbsorbAt - 0.06f, KarmaGame.EndingAbsorbAt + 0.10f, t);
            aura.transform.position = center;
            aura.color = new Color(1, 0.91f, 0.57f, game.EndingAbsorbLight * absorbed * 0.27f);
        }

        void DrawAtmosphere(float t)
        {
            float mood = Ease(0, 5.5f, t);
            float release = Ease(KarmaGame.EndingDepartAt, KarmaGame.EndingDuration - 0.2f, t);
            float gather = Ease(KarmaGame.EndingGatherAt, KarmaGame.EndingAbsorbAt - 0.15f, t)
                * (1 - Ease(KarmaGame.EndingAbsorbAt, KarmaGame.EndingAbsorbAt + 0.4f, t));
            var moonlit = Color.Lerp(new Color(0.73f, 0.8f, 0.99f), new Color(0.55f, 0.64f, 0.85f), gather);
            var departure = new Color(0.95f, 0.88f, 0.92f);
            var stageLight = Color.Lerp(Color.white, Color.Lerp(moonlit, departure, release), mood);
            if (originalBackdrop && backdrop != null)
                backdrop.color = backdropColor * Color.Lerp(stageLight, new Color(1f, 0.88f, 0.65f),
                    game.EndingAbsorbLight * 0.55f);
            for (int i = 0; i < mist.Length; i++)
            {
                float drift = Mathf.Sin(t * (i == 0 ? 0.15f : 0.21f) + i * 1.7f) * 3f;
                mist[i].transform.position = new Vector3(game.GameCamera.transform.position.x + drift,
                    (i == 0 ? 8.9f : 6.35f) + Mathf.Sin(t * 0.27f + i) * 0.25f, 0);
                mist[i].flipX = i == 1;
                mist[i].color = new Color(0.68f, 0.77f, 1f, mood * (i == 0 ? 0.34f : 0.18f) * (1 - 0.35f * release));
            }
        }

        void DrawMemories(float time)
        {
            for (int i = 0; i < memories.Length; i++)
            {
                var image = memories[i];
                if (image == null || image.sprite == null) continue;
                float age = time - (KarmaGame.EndingMemoryDelay + i * KarmaGame.EndingMemoryBeat);
                float presence = Ease(0, 0.33f, age) * (1 - Ease(0.7f, KarmaGame.EndingMemoryBeat, age));
                float x = KarmaGame.EndingMasterX - 0.7f + i * 0.42f;
                float floor = 0.3f + Ease(0, 1.4f, age) * 0.45f;
                image.transform.position = new Vector3(x, floor - image.sprite.bounds.min.y * image.transform.localScale.y, 0);
                image.color = new Color(0.72f, 0.80f, 1f, presence * 0.58f);
            }
        }

        void DrawMaster(float t)
        {
            if (t == lastMasterFrame) return;
            lastMasterFrame = t;
            float presence = Ease(KarmaGame.EndingMasterAt, 11.8f, t) * (1 - Ease(KarmaGame.EndingDepartAt + 0.1f,
                KarmaGame.EndingDepartAt + 2.7f, t));
            masterMotion.enabled = presence > 0;
            if (!masterMotion.enabled) return;

            // The farewell uses the original idle pose throughout both lines.
            float lesson = Gesture(11.3f, 12.0f, 12.38f, 13.55f, t) * 9f
                + Gesture(14.95f, 15.64f, 16.08f, 17.3f, t) * 8f
                + Gesture(18.38f, 18.96f, 19.38f, 20.35f, t) * 6f;
            float reply = Gesture(27.24f, 27.78f, 28.06f, 28.85f, t) * 10f;
            float angle = (lesson + reply) * Mathf.Deg2Rad;
            float breath = t < KarmaGame.EndingFarewellAt ? Mathf.Sin(t * 2.15f) * 0.022f : 0;
            int pose = -1;
            if (t >= 11.55f && t < 12f || t >= 15.9f && t < 16.35f) pose = 0;
            else if (t >= 12f && t < 15.9f) pose = 1;
            var sprite = pose >= 0 ? masterPoses[pose] : null;
            float foot = sprite != null ? masterFootPixels[pose] : 17;
            sprite ??= masterIdle;

            // Keep both sandals and the cane tip planted. Bend around the waist,
            // letting the head and shoulders travel farther than the hands.
            float sine = Mathf.Sin(angle);
            float cosine = Mathf.Cos(angle);
            for (int y = 0; y <= MasterRows; y++)
            {
                float height = (float)y / MasterRows;
                float weight = Ease(0.44f, 0.78f, height);
                for (int x = 0; x <= MasterColumns; x++)
                {
                    float horizontal = (float)x / MasterColumns;
                    float px = (horizontal - 0.5f) * 4.39f;
                    float py = (height - foot / 1312f) * MasterHeight;
                    var rest = new Vector2(px, py - 2.45f);
                    var bent = new Vector2(rest.x * cosine + rest.y * sine,
                        rest.y * cosine - rest.x * sine + 2.45f + breath);
                    masterVertices[y * (MasterColumns + 1) + x] = new Vector3(
                        Mathf.Lerp(px, bent.x, weight), Mathf.Lerp(py, bent.y, weight), 0);
                }
            }
            masterMesh.SetVertices(masterVertices, 0, masterVertices.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            var color = Color.Lerp(MasterMoonlight, MasterWarmLight, game.EndingAbsorbLight);
            color.a = presence * 0.98f;
            masterProperties.SetTexture("_MainTex", sprite.texture);
            masterProperties.SetColor("_Color", color);
            masterMotion.SetPropertyBlock(masterProperties);
        }

        static float Gesture(float start, float peak, float hold, float finish, float t)
            => Ease(start, peak, t) * (1 - Ease(hold, finish, t));

        void DrawAir(float t)
        {
            float ambient = Ease(4.7f, 9f, t)
                * (1 - Ease(KarmaGame.EndingDuration - 1.8f, KarmaGame.EndingDuration - 0.3f, t));
            for (int i = 0; i < driftingLights.Length; i++)
            {
                float seed = Mathf.Repeat(i * 0.618034f, 1f);
                float phase = Mathf.Repeat(seed + t * (0.065f + 0.005f * (i % 4)), 1f);
                float x = KarmaGame.EndingMasterX + (phase - 0.45f) * 40f;
                float y = 0.65f + Mathf.Repeat(i * 0.37f, 1f) * 4.5f + 0.27f * Mathf.Sin(t + i);
                driftingLights[i].transform.position = new Vector3(x, y, 0);
                driftingLights[i].color = new Color(0.59f, 0.75f, 1f,
                    Mathf.Sin(Mathf.PI * phase) * ambient * (0.15f + seed * 0.16f));
            }
        }

        static Vector2 EmblemOrbitPosition(int index, float time)
        {
            float arrive = KarmaGame.EndingAbsorbAt + 0.28f;
            float spin = 185f * Ease(KarmaGame.EndingGatherAt + 0.54f, arrive, time);
            float pull = Ease(KarmaGame.EndingGatherAt + 0.61f, arrive, time);
            float angle = (index * 120f + spin) * Mathf.Deg2Rad;
            // Center the formation above the chest, then bring all three emblems into it.
            return Vector2.up * (1.48f * (1 - pull))
                + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (EmblemRadius * (1 - pull));
        }

        void DrawEmblemAbsorption(Vector2 center, float t)
        {
            float merge = Ease(KarmaGame.EndingAbsorbAt + 1.15f, KarmaGame.EndingAbsorbAt + 1.65f, t);
            float absorb = Ease(KarmaGame.EndingAbsorbAt + 1.65f, KarmaGame.EndingAbsorbAt + 2.0f, t);
            for (int i = 0; i < symbols.Length; i++)
            {
                var symbol = symbols[i];
                float start = KarmaGame.EndingGatherAt + i * 0.17f;
                float appear = Ease(start, start + 0.19f, t);
                float alpha = appear * (1 - merge);
                symbol.transform.position = center + EmblemOrbitPosition(i, t);
                symbol.transform.localScale = symbolSizes[i] * Mathf.Lerp(1.05f, 0.44f, merge);
                symbol.color = new Color(1, 1, 1, alpha);

                var ribbon = oathRibbons[i];
                float trailAlpha = alpha * Ease(KarmaGame.EndingGatherAt + 0.54f, KarmaGame.EndingGatherAt + 0.79f, t);
                ribbon.enabled = trailAlpha > 0.001f;
                if (!ribbon.enabled) continue;
                var tint = Color.Lerp(game.Config.ColorOf((Essence)i), OathLight, 0.35f);
                tint.a = trailAlpha * 0.75f;
                ribbon.startColor = new Color(tint.r, tint.g, tint.b, 0);
                ribbon.endColor = tint;
                ribbon.widthMultiplier = 0.12f;
                for (int point = 0; point < RibbonPoints; point++)
                {
                    float u = (float)point / (RibbonPoints - 1);
                    ribbonPoints[point] = center + EmblemOrbitPosition(i, t - 0.24f * (1 - u));
                }
                ribbon.SetPositions(ribbonPoints);
            }

            emblemCore.transform.position = center;
            emblemCore.transform.localScale = Vector3.one * Mathf.Lerp(coreScale, 0, absorb);
            emblemCore.color = new Color(OathLight.r, OathLight.g, OathLight.b, merge * (1 - absorb));
        }

        void ShowTechniqueSymbol(Essence essence, Vector2 center, float t, float start, float end)
        {
            var symbol = symbols[(int)essence];
            float enter = Ease(start, start + 0.15f, t);
            float alpha = enter * (1 - Ease(end - 0.18f, end, t));
            symbol.transform.position = center + new Vector2(-1.65f, 2.6f + 0.38f * enter);
            symbol.color = new Color(1, 1, 1, alpha * 0.88f);
        }

        void ShowTechnique(int index, Vector2 position, float age, float duration, float sweep)
        {
            var renderer = techniques[index];
            float growth = Ease(0, duration * 0.73f, age);
            renderer.transform.position = position;
            renderer.transform.localScale = techniqueSizes[index] * Mathf.Lerp(0.68f, 1.14f, growth);
            renderer.transform.localRotation = Quaternion.Euler(0, 0, sweep * (1 - growth));
            float alpha = Ease(0, 0.105f, age) * (1 - Ease(duration * 0.45f, duration, age));
            renderer.color = new Color(1, 1, 1, alpha * 0.94f);
        }
        void DrawGuardian(float t)
        {
            if (t >= 6)
            {
                if (!guardianFinished)
                {
                    guardianFinished = true;
                    foreach (var particle in particles) particle.gameObject.SetActive(false);
                    if (fallen != null) { fallen.gameObject.SetActive(false); Destroy(fallen.gameObject); fallen = null; }
                }
                return;
            }
            if (fallen != null && t < 6)
            {
                fallenAnimator?.PresentEndingDefeat(t);
                if (!seeded && t >= 1)
                {
                    var bounds = fallenPictures != null && fallenPictures.Length > 0 ? fallenPictures[0].bounds
                        : new Bounds(fallen.position, new Vector3(5, 4, 1));
                    for (int i = 0; i < particles.Length; i++)
                    {
                        float x = Mathf.Repeat(i * 0.618034f, 1);
                        float y = Mathf.Repeat(i * 0.414214f, 1);
                        particleOrigins[i] = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, x),
                            Mathf.Lerp(bounds.min.y, bounds.max.y, y), 0);
                    }
                    seeded = true;
                }
                if (fallenPictures != null)
                    foreach (var picture in fallenPictures)
                    {
                        picture.GetPropertyBlock(dissolveBlock);
                        dissolveBlock.SetFloat("_Progress", Ease(1.1f, 5.2f, t));
                        picture.SetPropertyBlock(dissolveBlock);
                    }
            }
            if (fallen != null && t >= 5.5f) { fallen.gameObject.SetActive(false); Destroy(fallen.gameObject); fallen = null; }
            for (int i = 0; i < particles.Length; i++)
            {
                float age = t - 1.2f - i * 0.055f;
                float visible = age >= 0 && age < 1.65f ? Mathf.Sin(Mathf.Clamp01(age / 1.65f) * Mathf.PI) : 0;
                particles[i].transform.position = particleOrigins[i]
                    + new Vector3(Mathf.Sin(i * 2.4f) * age * 0.65f, age * (1.4f + i % 4 * 0.25f), 0);
                particles[i].color = new Color(1, 0.91f, 0.63f, visible * 0.8f);
            }
        }
    }
}
