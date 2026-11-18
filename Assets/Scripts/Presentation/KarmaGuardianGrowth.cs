using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // A rehearsal of existing skills inside Transition. This component never
    // selects/executes an attack, changes progression, or creates a damage object.
    public sealed class KarmaGuardianGrowth : MonoBehaviour
    {
        public enum Motion { Sweep, Rise, Surround, Return, Slam, Dash }
        public struct Technique
        {
            public string Prepare, Release, Inspect, Copy;
            public string Realization;
            public Sprite Effect, Trail;
            public Motion Movement;
            public Vector2 Size;
            // Normalized eye position on the primary form's Idle illustration.
            public Vector2 FaceInIdle;
            public Technique(string prepare, string release, string inspect, Sprite effect, Motion movement, Vector2 size, string copy = null, string realization = null, Sprite trail = null)
            { Prepare = prepare; Release = release; Inspect = inspect; Effect = effect; Movement = movement; Size = size; Copy = copy ?? prepare; Realization = realization; Trail = trail; FaceInIdle = new Vector2(0.5f, 0.85f); }
        }
        public enum Beat { Revive, Observe, Imitate, Stagger, Falter, Inspect, Adapt, Combine, Foreshadow, Ready, Remember, Hesitate, TakeStance }
        KarmaGame game;
        KarmaEnemy guardian;
        KarmaCharacterAnimator animator, sourceAnimator;
        Transform sourceRoot;
        SpriteRenderer sourcePicture, original, focus;
        Sprite reflection, hesitantReach;
        SpriteRenderer formPicture, formReveal;
        Material formMaterial, originalFormMaterial;
        SpriteRenderer inheritedHalo;
        Color haloColor;
        Technique ultimate;
        readonly SpriteRenderer[] failedPieces = new SpriteRenderer[3];
        readonly Sprite[] pieceSprites = new Sprite[3];
        readonly SpriteRenderer[] ultimateHints = new SpriteRenderer[3];
        readonly SpriteRenderer[] demonstrations = new SpriteRenderer[3];
        readonly Technique[] techniques = new Technique[3];
        readonly Essence[] order = new Essence[3];
        readonly SpriteRenderer[] masteryMemories = new SpriteRenderer[3];
        float age, facing, memoryX;
        bool recall, restored;
        public int Stage { get; private set; }
        public Beat CurrentBeat { get; private set; }
        public float Duration { get; private set; }
        public float Age => age;
        public int CurrentAttempt { get; private set; }
        const float Revived = 1.2f, Observed = 2.5f;
        const float FirstQuestion = Observed + 1.55f, FirstCopy = FirstQuestion + 1.85f;
        const float Copied = FirstCopy + 1.05f, RevealStart = Copied + 0.6f;
        const float RevealEnd = RevealStart + 1.25f, FirstRemembered = RevealEnd + 1.75f;
        const float AttemptSpan = 1.9f, AttemptReleaseEnd = 1.05f;
        const float ThirdCopyEnds = Observed + AttemptSpan * 2 + AttemptReleaseEnd;
        const float FailureHeld = ThirdCopyEnds + 2.2f, Reflected = FailureHeld + 2.6f, Adaptation = 1.55f;
        const float MasterStance = Revived + 0.35f, FirstStroke = MasterStance + 1.95f, StrokeLength = 1.05f;
        const float ChainEnd = FirstStroke + 3 * StrokeLength, HintStart = ChainEnd + 0.7f;
        const float HintEnd = HintStart + 2.25f, MasteryEnd = HintEnd + 1.05f;
        public string Caption
        {
            get
            {
                if (Stage == 1)
                {
                    if (age < FirstCopy) return "이렇게…?";
                    return Mathf.Floor(age * 24) / 24 < RevealEnd ? "네 움직임…" : "네 움직임… 기억했다.";
                }
                if (Stage == 3) return age < FirstStroke ? "이번엔… 내가 먼저." : "이제 나만의 움직임이다…";
                return techniques[1].Realization;
            }
        }
        public float CaptionAlpha
        {
            get
            {
                if (Stage == 1)
                    return age < FirstCopy ? CaptionWindow(FirstQuestion + 0.1f, FirstCopy - 0.15f)
                        : CaptionWindow(RevealStart, FirstRemembered - 0.22f);
                if (Stage == 3)
                    return age < FirstStroke ? CaptionWindow(MasterStance + 0.1f, FirstStroke - 0.15f)
                        : CaptionWindow(HintStart + 0.7f, MasteryEnd - 0.1f);
                return Smooth(FailureHeld + 1, FailureHeld + 1.25f, age)
                    * (1 - Smooth(Reflected + Adaptation + 0.5f, Duration - 0.1f, age));
            }
        }
        float CaptionWindow(float start, float end) => Smooth(start, start + 0.2f, age) * (1 - Smooth(end - 0.25f, end, age));
        public static float DurationFor(int count) => count == 1 ? FirstRemembered + Adaptation + 1.1f : count == 2 ? Reflected + Adaptation + 1 : MasteryEnd;
        public Vector3 CameraFocus => new Vector3(guardian.Position.x + facing * 1.4f, 3.6f, -10);

        public static KarmaGuardianGrowth Play(KarmaGame game, KarmaEnemy guardian,
            KarmaCharacterAnimator animator, IReadOnlyList<Essence> inherited, Sprite fallen, string previousForm, SpriteRenderer shieldHalo = null)
        {
            var go = new GameObject("Guardian growth rehearsal"); go.transform.SetParent(game.World, false);
            var growth = go.AddComponent<KarmaGuardianGrowth>();
            growth.game = game; growth.guardian = guardian; growth.animator = animator;
            growth.Stage = inherited.Count; growth.Duration = DurationFor(inherited.Count);
            if (inherited.Count == 1)
            {
                growth.hesitantReach = Resources.Load<Sprite>("Neutral/Growth/Hesitate");
                if (shieldHalo != null && shieldHalo.enabled && shieldHalo.gameObject.activeInHierarchy)
                {
                    growth.inheritedHalo = shieldHalo;
                    growth.haloColor = shieldHalo.color;
                }
            }
            if (inherited.Count == 2) growth.reflection = guardian.GrowthReflection;
            growth.facing = guardian.GetComponentInChildren<SpriteRenderer>().transform.lossyScale.x < 0 ? -1 : 1;
            // The memory stands between player and guardian, close enough for the
            // camera to connect the same movement without cutting away to a panel.
            growth.memoryX = guardian.Position.x + growth.facing * 6.5f;
            for (int i = 0; i < inherited.Count; i++)
            {
                growth.order[i] = inherited[i];
                growth.techniques[i] = guardian.GrowthTechnique(inherited[i]);
                growth.demonstrations[i] = growth.Layer("Reinterpreted technique " + inherited[i], growth.techniques[i].Effect, 12);
            }
            growth.sourceRoot = new GameObject("Memory of player's original technique").transform;
            growth.sourceRoot.SetParent(go.transform, false);
            var art = KarmaArtwork.Player(growth.sourceRoot);
            growth.sourceAnimator = art.GetComponent<KarmaCharacterAnimator>();
            growth.sourcePicture = art.GetComponentInChildren<SpriteRenderer>();
            growth.sourcePicture.sortingOrder = 6;
            growth.original = growth.Layer("Original technique rehearsal", OriginalEffect(inherited[inherited.Count - 1]), 11);
            growth.focus = growth.Layer("Power gathered into own weapon", KarmaArtwork.Sprite("PersonalizationVFX", "Core"), 13);
            if (inherited.Count == 2) growth.CreateFailedPieces();
            if (inherited.Count == 3)
            {
                growth.ultimate = guardian.UltimateForeshadow;
                for (int i = 0; i < growth.ultimateHints.Length; i++)
                    growth.ultimateHints[i] = growth.Layer("Unreleased ultimate preparation " + i,
                        i > 0 && growth.ultimate.Trail != null ? growth.ultimate.Trail : growth.ultimate.Effect, 8);
            }
            animator.BeginGrowth(fallen, previousForm);
            growth.Draw();
            return growth;
        }

        // A brief recall of the three learned stances at the REAL ultimate's
        // existing windup. It adds neither a fourth attack nor extra windup time.
        internal static void RecallMastery(KarmaGame game, KarmaEnemy guardian, KarmaCharacterAnimator animator)
        {
            if (game.Player.Essences.Count != 3) return;
            var go = new GameObject("Mastery stance recall"); go.transform.SetParent(guardian.transform, false);
            var growth = go.AddComponent<KarmaGuardianGrowth>();
            growth.game = game; growth.guardian = guardian; growth.animator = animator;
            growth.recall = true; growth.Duration = 0.5f;
            var inherited = game.Player.Essences.PersonalizationOrder;
            for (int i = 0; i < 3; i++)
            {
                growth.order[i] = inherited[i];
                var technique = guardian.GrowthTechnique(inherited[i]);
                growth.masteryMemories[i] = growth.Layer("Learned stance " + inherited[i], animator.GrowthSprite(technique.Release), 4);
            }
            growth.DrawRecall();
        }

        static Sprite OriginalEffect(Essence essence) => KarmaArtwork.Sprite("PlayerVFX",
            essence == Essence.Guard ? "Barrier" : essence == Essence.RisingSlash ? "Rising" : "Wave");
        SpriteRenderer Layer(string name, Sprite sprite, int sorting)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            var picture = child.AddComponent<SpriteRenderer>(); picture.sprite = sprite;
            picture.sortingOrder = sorting; KarmaVisuals.ApplyMaterial(picture); picture.enabled = false;
            return picture;
        }
        static float Smooth(float a, float b, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, t));
        static void Display(SpriteRenderer picture, Vector2 position, Vector2 size, float angle, float flip, float opacity, Color tint)
        {
            if (picture == null || picture.sprite == null) return;
            picture.enabled = opacity > 0.001f;
            picture.transform.position = position;
            picture.transform.rotation = Quaternion.Euler(0, 0, angle);
            float fit = Mathf.Min(size.x / picture.sprite.bounds.size.x, size.y / picture.sprite.bounds.size.y);
            picture.transform.localScale = new Vector3(fit * flip, fit, 1);
            tint.a = opacity; picture.color = tint;
        }
        void LateUpdate()
        {
            if (game == null || guardian == null || !guardian.Alive
                || (recall ? !guardian.PresentationActive || guardian.Stunned : !game.GuardianInheriting))
            { Finish(); return; }
            if (game.Paused || game.GameplayFrozen) return;
            age = Mathf.Min(Duration, age + Time.deltaTime);
            if (age >= Duration) { Finish(); return; }
            if (recall) DrawRecall(); else Draw();
        }
        void DrawRecall()
        {
            float t = age / Duration;
            for (int i = 0; i < 3; i++)
            {
                float side = i - 1;
                Vector2 point = guardian.Position + new Vector2(side * 2.6f * (1 - t), 1.7f + (i == 1 ? 1.8f : 0) * (1 - t));
                Display(masteryMemories[i], point, new Vector2(5, 6), 0, guardian.Facing,
                    Mathf.Sin(t * Mathf.PI) * 0.22f, KarmaPersonalizationVFX.TechniqueColor(game, order[i]));
            }
        }
        void Draw()
        {
            float t = Mathf.Floor(age * 24) / 24;
            foreach (var picture in demonstrations) if (picture != null) picture.enabled = false;
            original.enabled = focus.enabled = sourcePicture.enabled = false;
            foreach (var piece in failedPieces) if (piece != null) piece.enabled = false;
            foreach (var hint in ultimateHints) if (hint != null) hint.enabled = false;
            if (inheritedHalo != null)
            {
                var tint = haloColor;
                tint.a *= Smooth(RevealEnd, RevealEnd + 0.55f, t);
                inheritedHalo.color = tint;
            }
            int latest = Stage - 1;
            if (t < Revived)
            {
                CurrentBeat = Beat.Revive; animator.GrowthRevive(t / Revived); return;
            }
            if (Stage == 3)
            {
                DrawMastery(t);
                return;
            }
            if (t < Observed)
            {
                CurrentBeat = Beat.Observe;
                animator.GrowthPose("Idle", true, Vector2.zero);
                float opacity = Smooth(Revived, Revived + 0.3f, t);
                if (Stage == 1) opacity *= 1 - Smooth(Observed - 0.35f, Observed, t);
                ShowSource(latest, t - Revived, opacity);
            }
            else if (Stage == 1 && t < FirstQuestion)
            {
                CurrentBeat = Beat.Hesitate;
                float elapsed = t - Observed;
                animator.GrowthPose("Idle", true, Vector2.zero);
                if (elapsed >= 0.2f && t < FirstQuestion - 0.28f)
                {
                    float reach = Smooth(0.2f, 0.55f, elapsed) * (1 - Smooth(FirstQuestion - 0.65f, FirstQuestion - 0.28f, t));
                    animator.GrowthKeyPose(hesitantReach, "Growth/Hesitate", 1, -3 * reach);
                }
            }
            else if (Stage == 1 && t < FirstCopy)
            {
                CurrentBeat = Beat.TakeStance;
                animator.GrowthPose("Windup", true, Vector2.zero);
            }
            else if (Stage == 2 && t < ThirdCopyEnds)
            {
                DrawAttempt(t);
            }
            else if (Stage == 1 && t < Copied)
            {
                CurrentAttempt = 1;
                CurrentBeat = Beat.Imitate;
                float action = Mathf.InverseLerp(FirstCopy, Copied, t);
                // Neutral only has its original sword. The copied effect carries the skill identity.
                string pose = order[0] == Essence.Guard || (order[0] == Essence.RisingSlash && action > 0.36f)
                    ? "Windup" : "Slash";
                animator.GrowthPose(action < 0.82f ? pose : "Recovery", true, Vector2.zero);
                DrawOriginal(guardian.Position, order[latest], action, 0.8f);
            }
            else if (Stage == 1)
            {
                // The first successful copy is acknowledged before the guardian
                // demonstrates its existing signature skill. No repeated trial.
                float progress = (t - FirstRemembered) / Adaptation;
                CurrentBeat = progress < 0 ? Beat.Remember : progress < 1 ? Beat.Adapt : Beat.Ready;
                if (t >= RevealEnd) EndFirstForm();
                if (progress < 0)
                {
                    if (t < RevealStart)
                        animator.GrowthPose("Recovery", true, Vector2.zero);
                    else if (t < RevealEnd)
                        DrawFirstForm(Mathf.InverseLerp(RevealStart, RevealEnd, t));
                    else animator.GrowthPose("Idle", false, Vector2.zero);
                }
                else if (progress < 1) Adapt(0, progress);
                else animator.GrowthPose(techniques[0].Prepare, false, Vector2.zero);
            }
            else if (Stage == 2 && t < FailureHeld)
            {
                CurrentBeat = Beat.Falter;
                animator.GrowthFalter(t - ThirdCopyEnds);
                DrawFailedPieces(t - (ThirdCopyEnds - 0.25f));
                // Only the third attempt collapses. Let the fallen pose register
                // before the guardian pushes up and studies its own weapon.
            }
            else if (Stage == 2 && t < Reflected)
            {
                CurrentBeat = Beat.Inspect;
                if (t < FailureHeld + 0.6f)
                {
                    animator.GrowthRevive(Mathf.InverseLerp(FailureHeld, FailureHeld + 0.6f, t) * 0.4f);
                    return;
                }
                var technique = techniques[latest];
                animator.GrowthPose(technique.Inspect, true, Vector2.zero, -3);
                animator.GrowthKeyPose(reflection, "Growth/InspectWeapon", 0.95f);
                Vector2 anchor = guardian.GrowthInspectionAnchor;
                Vector2 hand = guardian.Position + new Vector2(facing * anchor.x, anchor.y);
                float gather = Smooth(FailureHeld + 0.9f, Reflected - 0.15f, t);
                Display(focus, hand, Vector2.one * Mathf.Lerp(1.7f, 0.45f, gather), 0, 1,
                    Mathf.Sin(gather * Mathf.PI) * 0.7f, KarmaPersonalizationVFX.TechniqueColor(game, order[latest]));
            }
            else
            {
                float progress = (t - Reflected) / Adaptation;
                CurrentBeat = progress < 1 ? Beat.Adapt : Beat.Ready;
                if (progress < 1) Adapt(latest, progress);
                else animator.GrowthPose(techniques[latest].Prepare, false, Vector2.zero);
            }
        }
        // Both original sprite meshes share the foot pivot. Keeping their mesh
        // outlines excludes neighbouring drawings that overlap the atlas slice rectangles.
        static Vector2 PoseAnchor(Sprite pose, Vector2 normalized) =>
            (Vector2.Scale(normalized, pose.rect.size) - pose.pivot) / pose.pixelsPerUnit;

        void DrawFirstForm(float progress)
        {
            if (formReveal == null)
            {
                animator.GrowthPose("Recovery", true, Vector2.zero);
                formPicture = animator.GetComponentInChildren<SpriteRenderer>();
                var next = animator.GrowthSprite("Idle");
                Bounds area = formPicture.sprite.bounds;
                float height = area.size.y;
                area.Encapsulate(next.bounds);
                Vector2 grip = PoseAnchor(formPicture.sprite, new Vector2(0.59f, 0.50f));
                Vector2 oldFace = PoseAnchor(formPicture.sprite, new Vector2(0.393f, 0.878f));
                Vector2 newFace = PoseAnchor(next, techniques[0].FaceInIdle);
                Vector2 farthest = Vector2.Max(grip - (Vector2)area.min, (Vector2)area.max - grip);
                formMaterial = new Material(Resources.Load<Shader>("Shared/Rendering/KarmaInheritanceReveal"));
                formMaterial.SetVector("_Grip", new Vector4(grip.x, grip.y, height, farthest.magnitude));
                formMaterial.SetVector("_Faces", new Vector4(oldFace.x, oldFace.y, newFace.x, newFace.y));
                originalFormMaterial = formPicture.sharedMaterial;
                formPicture.sharedMaterial = formMaterial;

                formReveal = new GameObject("First inherited form").AddComponent<SpriteRenderer>();
                formReveal.transform.SetParent(formPicture.transform, false);
                formReveal.sprite = next;
                formReveal.color = formPicture.color;
                formReveal.flipX = formPicture.flipX; formReveal.flipY = formPicture.flipY;
                formReveal.sortingLayerID = formPicture.sortingLayerID;
                formReveal.sortingOrder = formPicture.sortingOrder + 1;
                formReveal.sharedMaterial = formMaterial;
                var layer = new MaterialPropertyBlock();
                layer.SetFloat("_Incoming", 1);
                formReveal.SetPropertyBlock(layer);
            }
            formMaterial.SetFloat("_FormAmount", progress);
        }

        void EndFirstForm()
        {
            if (formMaterial == null) return;
            if (formPicture != null) formPicture.sharedMaterial = originalFormMaterial;
            if (formReveal != null) { formReveal.enabled = false; Destroy(formReveal.gameObject); }
            Destroy(formMaterial);
            formMaterial = null; formReveal = null;
        }

        void DrawAttempt(float t)
        {
            int index = Mathf.Min(2, Mathf.FloorToInt((t - Observed) / AttemptSpan));
            CurrentAttempt = index + 1;
            float local = t - Observed - index * AttemptSpan;
            if (index == 0) ShowSource(1, 0.6f, 1 - Smooth(0.15f, 0.65f, local));
            if (local < AttemptReleaseEnd)
            {
                CurrentBeat = Beat.Imitate;
                animator.GrowthPose(techniques[1].Copy, true, Vector2.zero);
                float action = Mathf.InverseLerp(0.3f, AttemptReleaseEnd, local);
                DrawOriginal(guardian.Position, order[1], action * (0.32f + index * 0.16f),
                    Smooth(0.27f, 0.4f, local) * (1 - Smooth(0.8f, AttemptReleaseEnd, local)) * 0.8f);
                if (index == 2 && local > 0.8f) DrawFailedPieces(local - 0.8f);
            }
            else
            {
                CurrentBeat = Beat.Stagger;
                animator.GrowthStumble(techniques[1].Prepare, CurrentAttempt, local - AttemptReleaseEnd);
            }
        }
        void ShowSource(int index, float seconds, float opacity)
        {
            sourceRoot.position = new Vector3(memoryX, KarmaSpace.PlayerHalfHeight, 0);
            sourceRoot.localScale = new Vector3(-facing, 1, 1);
            sourceAnimator.SampleGrowthSource(order[index], seconds);
            sourcePicture.enabled = opacity > 0.001f;
            sourcePicture.color = new Color(0.75f, 0.88f, 1, opacity * KarmaPersonalizationVFX.MemoryOpacity);
            // The original skill begins at the memory, then the same shape appears
            // at the guardian's hand in the following beat.
            DrawOriginal(sourceRoot.position, order[index], Mathf.Clamp01(seconds / 0.75f), opacity * KarmaPersonalizationVFX.MemoryOpacity, -facing);
        }
        void DrawOriginal(Vector2 origin, Essence essence, float t, float opacity, float direction = 0)
        {
            if (direction == 0) direction = facing;
            Vector2 point = origin + new Vector2(direction * 1.5f, 0.6f);
            Vector2 size = essence == Essence.Guard ? new Vector2(3.3f, 4) : new Vector2(3.5f, 4);
            if (essence == Essence.SwordWave) point.x += direction * t * 3;
            if (essence == Essence.RisingSlash) point.y += t * 1.5f;
            Display(original, point, size, 0, direction, opacity * (1 - Smooth(0.75f, 1, t)), Color.white);
        }
        void Adapt(int index, float progress, string leadPose = null)
        {
            var technique = techniques[index];
            float launch = Smooth(0.18f, 0.75f, progress);
            float opacity = Smooth(0, 0.22f, progress) * (1 - Smooth(0.75f, 1, progress));
            Vector2 offset = Vector2.zero;
            Vector2 point = guardian.Position + new Vector2(facing * 2.2f, 1.1f);
            float angle = 0;
            switch (technique.Movement)
            {
                case Motion.Sweep: point.x += facing * launch * 3.6f; break;
                case Motion.Rise: point += new Vector2(facing * 2.5f, launch * 2 - 2.6f); break;
                case Motion.Surround: point = guardian.Position + Vector2.up * 1.3f; break;
                case Motion.Return: point.x += facing * Mathf.Sin(launch * Mathf.PI) * 4.5f; angle = -facing * launch * 360; break;
                case Motion.Slam: point += new Vector2(facing * launch * 3, -1.8f); break;
                case Motion.Dash:
                    offset = new Vector2(Mathf.Sin(launch * Mathf.PI) * 1.3f, Mathf.Sin(launch * Mathf.PI) * 1.1f);
                    point += new Vector2(facing * offset.x, offset.y); break;
            }
            animator.GrowthPose(progress < 0.25f ? leadPose ?? technique.Prepare : technique.Release, false, offset);
            Display(demonstrations[index], point, technique.Size, angle, facing, opacity * 0.85f, Color.white);

        }
        void CreateFailedPieces()
        {
            var source = original.sprite;
            if (source == null) return;
            Rect rect = source.rect;
            for (int i = 0; i < 3; i++)
            {
                var part = new Rect(rect.x + rect.width * i / 3, rect.y, rect.width / 3, rect.height);
                pieceSprites[i] = Sprite.Create(source.texture, part, Vector2.one * 0.5f, source.pixelsPerUnit);
                pieceSprites[i].name = "Broken copy fragment " + i;
                failedPieces[i] = Layer(pieceSprites[i].name, pieceSprites[i], 11);
            }
        }
        void DrawFailedPieces(float elapsed)
        {
            float fall = Mathf.Clamp01(elapsed / 0.85f);
            for (int i = 0; i < 3; i++)
            {
                float side = i - 1;
                Vector2 point = guardian.Position + new Vector2(facing * (1.8f + side * (0.5f + fall)),
                    0.9f - fall * fall * 2.4f + Mathf.Sin(fall * Mathf.PI) * 0.3f);
                Display(failedPieces[i], point, new Vector2(1.4f, 3.3f) * (1 - fall * 0.4f),
                    side * fall * 60, facing, (1 - fall) * 0.85f, new Color(0.7f, 0.75f, 0.85f));
            }
        }
        void DrawMastery(float t)
        {
            CurrentBeat = Beat.Combine;
            if (t < FirstStroke)
            {
                CurrentBeat = Beat.TakeStance;
                animator.GrowthPose(t < MasterStance ? "Idle" : techniques[0].Copy, false, Vector2.zero);
                return;
            }
            if (t < ChainEnd)
            {
                float stroke = (t - FirstStroke) / StrokeLength;
                int i = Mathf.Min(2, Mathf.FloorToInt(stroke));
                // Each release becomes the windup of the next inherited skill.
                Adapt(i, stroke - i, i == 0 ? techniques[0].Copy : techniques[i - 1].Release);
                return;
            }
            if (t < HintStart)
            {
                animator.GrowthPose(techniques[2].Release, false, Vector2.zero);
                return;
            }
            if (t < HintEnd)
            {
                CurrentBeat = Beat.Foreshadow;
                DrawUltimateHint(Mathf.InverseLerp(HintStart, HintEnd, t));
                return;
            }
            CurrentBeat = Beat.Ready;
            animator.GrowthPose(ultimate.Release, false, Vector2.zero);
        }

        void DrawUltimateHint(float progress)
        {
            animator.GrowthPose(progress < 0.4f ? ultimate.Prepare : ultimate.Release, false, Vector2.zero);
            float fade = 1 - Smooth(0.8f, 1, progress);
            Vector2 center = guardian.Position + Vector2.up * 1.1f;
            if (ultimate.Movement == Motion.Rise)
            {
                for (int i = 0; i < ultimateHints.Length; i++)
                {
                    float form = Smooth(i * 0.12f, i * 0.12f + 0.25f, progress);
                    Vector2 point = center + new Vector2(facing * (i - 1) * 3, 3.5f + form);
                    Display(ultimateHints[i], point, ultimate.Size, 0, facing, form * fade * 0.8f, Color.white);
                }
                return;
            }
            float open = Smooth(0, 0.35f, progress);
            if (ultimate.Movement == Motion.Dash)
            {
                Display(ultimateHints[0], center, ultimate.Size, 0, facing, open * fade * 0.6f, Color.white);
                // Show the shield and two crossing paths, as in the actual dash windup.
                for (int i = 1; i <= 2; i++)
                {
                    float trace = Mathf.InverseLerp(i * 0.17f, i * 0.17f + 0.38f, progress);
                    float slope = i == 1 ? 1 : -1;
                    Vector2 path = center + new Vector2(facing * (1 + 4 * trace), slope * (trace - 0.5f) * 2.5f);
                    Display(ultimateHints[i], path, new Vector2(5, 3), facing * slope * 25, facing,
                        Mathf.Sin(trace * Mathf.PI) * fade * 0.72f, Color.white);
                }
                return;
            }
            center.y = 0.25f;
            Display(ultimateHints[0], center, ultimate.Size * Mathf.Lerp(0.3f, 1, open), 0, facing,
                open * fade * 0.8f, Color.white);
        }
        void OnDestroy()
        {
            foreach (var sprite in pieceSprites) if (sprite != null) Destroy(sprite);
        }
        void Finish()
        {
            if (!restored && !recall && animator != null) animator.EndGrowth();
            restored = true; gameObject.SetActive(false); Destroy(gameObject);
        }
        void OnDisable()
        {
            if (inheritedHalo != null) inheritedHalo.color = haloColor;
            EndFirstForm();
            if (!restored && !recall && animator != null) animator.EndGrowth();
            restored = true;
        }
    }
}
