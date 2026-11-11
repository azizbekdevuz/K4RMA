using UnityEngine;
namespace KarmaPrototype
{
    public enum KarmaTutorialStep { Travel, Lesson, Practice, Complete, FinalDialogue, Gate, Fade }
    public sealed class KarmaTutorial : MonoBehaviour
    {
        public KarmaGame Game { get; private set; }
        public KarmaTutorialStep Step { get; private set; }
        public int Lesson { get; private set; } = -1;
        public int Successes { get; private set; }
        public bool WaitingForGuard { get; private set; }
        public bool Frozen => WaitingForGuard || Step == KarmaTutorialStep.Lesson || Step == KarmaTutorialStep.FinalDialogue || Step == KarmaTutorialStep.Fade;
        bool preparingGuard, inputReleaseRequired;
        int inputFrame = -1, freezeFrame, dialogue, strikeId, contactId, countedStrike;
        bool released, strikePaused;
        float completeUntil, attackAt, attackProgress, fade, blockedUntil;
        Vector2 strikeStart, strikeEnd;
        SpriteRenderer strike, strikeCharge;
        Vector3 chargeScale;
        float strikeFlightTime;
        bool strikeLaunched;
        GameObject[] barriers;
        KarmaTrainingTarget[] targets;
        public bool CanAct => preparingGuard || !Frozen && !inputReleaseRequired && inputFrame != Time.frameCount && Step != KarmaTutorialStep.Fade;
        public float Fade => fade;
        public static readonly float[] Stations = { 8, 8, 8, 8 };
        public string BlockedMessage => Time.unscaledTime < blockedUntil ? "먼저 훈련을 마쳐라. 허수아비 앞에서 수련을 끝내고 이동하거라." : "";
        public void WarnUnfinished()
        {
            if (Step == KarmaTutorialStep.Practice) blockedUntil = Time.unscaledTime + 2;
        }
        static readonly string[] Lessons = { "먼저 검을 휘둘러 보아라. 나무의 중심을 정확히 노려라.", "검에 담은 기운을 앞으로 보내 보아라.", "아래에서 위로. 검을 올리는 힘에 몸을 실어라.", "이번에는 막아 보아라. 공격이 닿기 전에 보호의 힘을 펼쳐라." };
        static readonly string[] Ending = { "내가 가르친 기술은 모두 익혔구나.", "하지만 배운 것을 그대로 쓰는 것만으로는 수련이 끝나지 않는다.", "이제 그 기술들을 너만의 것으로 만들어라." };
        public string Speech => Step == KarmaTutorialStep.FinalDialogue ? Ending[dialogue] : Step == KarmaTutorialStep.Lesson ? Lessons[Lesson] : "";
        public string SpeechTitle => "스승";
        public void Initialize(KarmaGame game)
        {
            Game = game; Step = KarmaTutorialStep.Travel;
            KarmaTutorialStage.Build(this, out targets, out barriers);
        }
        internal void Skip()
        {
            if (Step == KarmaTutorialStep.Fade) return;
            WaitingForGuard = preparingGuard = false;
            inputReleaseRequired = true;
            Step = KarmaTutorialStep.Fade; fade = 0;
            Game.Player.ClearStatus();
            Game.Player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            Time.timeScale = 0;
        }
        public bool Learned(Essence e) => Lesson >= (int)e + 1;
        public bool Accepts(KarmaTrainingAttack kind) => Step == KarmaTutorialStep.Practice && !Game.GameplayFrozen && Lesson == (int)kind;
        public void HitArea(KarmaTrainingAttack kind, int id, Vector2 center, Vector2 size)
        {
            foreach (var target in targets)
                if (target.Kind == kind && target.HitBounds.Intersects(new Bounds(center, size))) target.Hit(kind, id);
        }
        public bool HitProjectile(int id, Vector2 from, Vector2 to, float radius)
        {
            var target = targets[1];
            var bounds = target.HitBounds; bounds.Expand(radius * 2);
            Vector2 delta = to - from;
            bool contact = bounds.Contains(from) || delta.sqrMagnitude > 0 && bounds.IntersectRay(new Ray(from, delta.normalized), out float distance) && distance <= delta.magnitude;
            if (!contact) return false;
            // Count any real swept hit, including point-blank shots and shots
            // fired back toward the target from its far side.
            target.Hit(KarmaTrainingAttack.Wave, id);
            return true;
        }
        public void Success()
        {
            if (Successes >= 3) return;
            Successes++;
            if (Successes != 3) return;
            Step = KarmaTutorialStep.Complete; completeUntil = Time.time + 1;
            blockedUntil = 0;
            barriers[Lesson].GetComponent<KarmaTutorialGate>().Open();
            KarmaCombatVFX.SpawnSprite(transform, Game.Player.Position, "PlayerVFX", "Barrier", Vector2.one * 3, 0.3f);
        }
        public void GuardBlocked()
        {
            if (Lesson != 3 || Step != KarmaTutorialStep.Practice || contactId == 0 || contactId == countedStrike) return;
            countedStrike = contactId; Success();
        }
        public void Tick()
        {
            if (Game.SettingsOpen || Game.Paused || KarmaInput.RebindingThisFrame) return;
            if (Step == KarmaTutorialStep.Lesson || Step == KarmaTutorialStep.FinalDialogue)
            {
                if (!KarmaInput.Pressed(KarmaAction.DialogueConfirm)) return;
                inputFrame = Time.frameCount; inputReleaseRequired = true;
                if (Step == KarmaTutorialStep.FinalDialogue) { if (++dialogue == Ending.Length) Step = KarmaTutorialStep.Gate; }
                else
                {
                    Step = KarmaTutorialStep.Practice;
                    if (Lesson == 3) attackAt = Time.time + Game.Player.Essences.Cooldown(Essence.Guard) + 0.5f;
                    KarmaCombatVFX.SpawnSprite(transform, Game.Player.Position, "PlayerVFX", "Barrier", Vector2.one * 3, 0.35f);
                }
                Time.timeScale = Frozen ? 0 : 1;
                return;
            }
            if (Step == KarmaTutorialStep.Fade)
            { fade += Time.unscaledDeltaTime * 1.5f; if (fade >= 1) Game.FinishOpening(); return; }
            if (WaitingForGuard)
            {
                if (!KarmaInput.Held(KarmaAction.Guard)) released = true;
                if (Time.frameCount <= freezeFrame || !released || !KarmaInput.Pressed(KarmaAction.Guard)) return;
                preparingGuard = true;
                Game.Player.Essences.ResetTemporaryState();
                Game.Player.Essences.TryUse(Essence.Guard);
                preparingGuard = false;
                if (!Game.Player.Shielded) return;
                WaitingForGuard = false; inputFrame = Time.frameCount;
                Time.timeScale = 1;
                return;
            }
            if (inputReleaseRequired)
            {
                foreach (var action in KarmaInput.ConfigurableActions) if (KarmaInput.Held(action)) return;
                inputReleaseRequired = false;
            }
            if (Step == KarmaTutorialStep.Travel)
            {
                int next = Lesson + 1;
                if (next < 4 && Game.Player.Position.x >= Stations[next] - (next == 0 ? 10 : 14))
                {
                    Lesson = next; Successes = 0; Step = KarmaTutorialStep.Lesson;
                    for (int i = 0; i < targets.Length; i++) targets[i].enabled = i == next;
                    Game.Player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    Game.Player.Essences.ResetTemporaryState();
                    attackAt = Time.time + 1.4f; Time.timeScale = Frozen ? 0 : 1;
                }
            }
            if (Step == KarmaTutorialStep.Complete && Time.time >= completeUntil)
            {
                Step = Lesson == 3 ? KarmaTutorialStep.FinalDialogue : KarmaTutorialStep.Travel;
                if (Lesson == 3) { dialogue = 0; Game.Player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero; Time.timeScale = 0; }
            }
            if (Step == KarmaTutorialStep.Practice && Lesson == 3) TickTrainingStrike();
            if (Step == KarmaTutorialStep.Gate && Mathf.Abs(Game.Player.Position.x - KarmaTutorialStage.FinalGate) <= 5 && KarmaInput.Pressed(KarmaAction.Interact))
            { Step = KarmaTutorialStep.Fade; inputFrame = Time.frameCount; Time.timeScale = 0; }
        }
        void TickTrainingStrike()
        {
            if (strike == null)
            {
                float cooldown = Game.Player.Essences.Cooldown(Essence.Guard);
                if (cooldown > 0) attackAt = Mathf.Max(attackAt, Time.time + cooldown + 0.5f);
                if (Time.time < attackAt) return;
                strikeId++;
                Vector2 dummy = targets[3].HitBounds.center;
                float side = Game.Player.Position.x < dummy.x ? -1 : 1;
                strikeStart = dummy + Vector2.right * side * 0.8f;
                strikeEnd = Game.Player.Position;
                strike = KarmaVisuals.Box(transform, "허수아비 검기", strikeStart, Vector2.one, Color.white, 14);
                KarmaRemnantVFX.ProjectileLook(strike, strikeEnd - strikeStart);
                strike.enabled = false;
                strikeCharge = KarmaVisuals.Box(transform, "허수아비 검기 충전", strikeStart, Vector2.one, new Color(0.35f, 0.75f, 1, 0.3f), 15);
                KarmaCombatVFX.ApplySprite(strikeCharge, "PlayerVFX", "Barrier", Vector2.one * 2.4f, true);
                if (KarmaCombatVFX.GlowMaterial != null) strikeCharge.sharedMaterial = KarmaCombatVFX.GlowMaterial;
                chargeScale = strikeCharge.transform.localScale;
                attackProgress = -0.85f; strikePaused = false; strikeLaunched = false;
            }
            attackProgress += Time.deltaTime;
            if (attackProgress < 0)
            {
                float gathered = Mathf.Clamp01(1 + attackProgress / 0.85f);
                strikeCharge.transform.localScale = chargeScale * Mathf.Lerp(1, 0.25f, gathered);
                strikeCharge.color = new Color(0.35f, 0.75f, 1, Mathf.Lerp(0.25f, 1, gathered));
                return;
            }
            if (!strikeLaunched)
            {
                strikeLaunched = true;
                strikeCharge.enabled = false; Destroy(strikeCharge.gameObject); strikeCharge = null;
                strikeEnd = Game.Player.Position;
                strikeFlightTime = Mathf.Max(0.45f, Vector2.Distance(strikeStart, strikeEnd) / 12);
                Vector2 velocity = (strikeEnd - strikeStart) / strikeFlightTime;
                KarmaRemnantVFX.ProjectileLook(strike, velocity);
                KarmaCombatVFX.AttachAttack(strike, velocity, new Color(0.35f, 0.75f, 1));
                strike.enabled = true;
            }
            Vector2 position = Vector2.Lerp(strikeStart, strikeEnd, Mathf.Clamp01(attackProgress / strikeFlightTime));
            var playerBounds = Game.Player.GetComponent<BoxCollider2D>().bounds;
            var warningBounds = playerBounds; warningBounds.Expand(new Vector3(3, 2, 0));
            if (!strikePaused && warningBounds.Contains(position))
            {
                strikePaused = true;
                WaitingForGuard = true; freezeFrame = Time.frameCount;
                released = !KarmaInput.Held(KarmaAction.Guard);
                Time.timeScale = 0; return;
            }
            Vector2 from = strike.transform.position;
            Vector2 travel = position - from;
            playerBounds.Expand(new Vector3(0.7f, 2, 0));
            bool contact = playerBounds.Contains(from) || playerBounds.Contains(position)
                || travel.sqrMagnitude > 0 && playerBounds.IntersectRay(new Ray(from, travel.normalized), out float distance) && distance <= travel.magnitude;
            strike.transform.position = position;
            if (!contact && attackProgress < strikeFlightTime) return;
            contactId = strikeId;
            if (contact) Game.Player.TakeDamage(1);
            contactId = 0;
            Destroy(strike.gameObject); strike = null; attackAt = Time.time + Game.Player.Essences.Cooldown(Essence.Guard) + 0.5f;
        }
        public void FrameCamera()
        {
            var camera = Game.GameCamera;
            // Each supplied 16:9 image is a complete screen. Change screens only
            // when the player walks out of a courtyard; never crop or pan the art.
            float centerX = Game.Player.Position.x < KarmaTutorialStage.RoomWidth / 2 ? 0
                : Mathf.Clamp(Game.Player.Position.x, KarmaTutorialStage.RoomWidth,
                    KarmaTutorialStage.Right - KarmaTutorialStage.RoomWidth / 2);
            camera.orthographicSize = KarmaTutorialStage.RoomWidth * 9 / 32;
            camera.transform.position = new Vector3(centerX, 9, -10);
        }
    }
}
