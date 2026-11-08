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
        float completeUntil, attackAt, attackProgress, fade;
        Vector2 strikeStart, strikeEnd;
        SpriteRenderer strike;
        GameObject[] barriers;
        KarmaTrainingTarget[] targets;
        public bool CanAct => preparingGuard || !Frozen && !inputReleaseRequired && inputFrame != Time.frameCount && Step != KarmaTutorialStep.Fade;
        public float Fade => fade;
        public static readonly float[] Stations = { 14, 42, 70, 98 };
        static readonly string[] Lessons = { "먼저 검을 휘둘러 보아라. 나무의 중심을 정확히 노려라.", "검에 담은 기운을 앞으로 보내 보아라.", "아래에서 위로. 검을 올리는 힘에 몸을 실어라.", "이번에는 막아 보아라. 공격이 닿기 전에 보호의 힘을 펼쳐라." };
        static readonly string[] Ending = { "내가 가르친 기술은 모두 익혔구나.", "하지만 배운 것을 그대로 쓰는 것만으로는 수련이 끝나지 않는다.", "이제 그 기술들을 너만의 것으로 만들어라.", "문 너머의 수호자들이 네 답을 기다리고 있다." };
        public string Speech => Step == KarmaTutorialStep.FinalDialogue ? Ending[dialogue] : Step == KarmaTutorialStep.Lesson ? Lessons[Lesson] : Step == KarmaTutorialStep.Complete ? (Lesson == 0 ? "기본 공격 수련 완료 · 이제 스승의 가르침을 받으러 가세요." : "좋다. 힘과 타이밍을 기억하거라. 다음 마당으로 가자.") : "";
        public void Initialize(KarmaGame game)
        {
            Game = game; Step = KarmaTutorialStep.Travel;
            KarmaTutorialStage.Build(this, out targets, out barriers);
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
            if (Mathf.Abs(Game.Player.Position.x - target.transform.position.x) >= 7) target.Hit(KarmaTrainingAttack.Wave, id);
            return true;
        }
        public void Success()
        {
            if (Successes >= 3) return;
            Successes++;
            if (Successes != 3) return;
            Step = KarmaTutorialStep.Complete; completeUntil = Time.time + 1;
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
                if (next < 4 && Game.Player.Position.x >= Stations[next] - 14)
                {
                    Lesson = next; Successes = 0; Step = next == 0 ? KarmaTutorialStep.Practice : KarmaTutorialStep.Lesson;
                    Game.Player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    Game.Player.Essences.ResetTemporaryState();
                    attackAt = Time.time + 1.4f; Time.timeScale = Frozen ? 0 : 1;
                }
                else if (next == 4 && Game.Player.Position.x > KarmaTutorialStage.FinalGate - 6) { Step = KarmaTutorialStep.FinalDialogue; Time.timeScale = 0; }
            }
            if (Step == KarmaTutorialStep.Complete && Time.time >= completeUntil)
            { Step = KarmaTutorialStep.Travel; }
            if (Step == KarmaTutorialStep.Practice && Lesson == 3) TickTrainingStrike();
            if (Step == KarmaTutorialStep.Gate && Game.Player.Position.x > KarmaTutorialStage.FinalGate - 1 && KarmaInput.Pressed(KarmaAction.Interact))
            { Step = KarmaTutorialStep.Fade; inputFrame = Time.frameCount; Time.timeScale = 0; }
        }
        void TickTrainingStrike()
        {
            if (strike == null)
            {
                if (Time.time < attackAt) return;
                strikeId++;
                strikeStart = new Vector2(Game.Player.Position.x + 10, Game.Player.Position.y);
                strikeEnd = Game.Player.Position;
                strike = KarmaVisuals.Box(transform, "훈련 장치 예고", strikeStart, new Vector2(0.7f, 2), new Color(1, 0.55f, 0.15f), 6);
                KarmaCombatVFX.ApplySprite(strike, "PlayerVFX", "Wave", new Vector2(0.7f, 2), true);
                strike.color = new Color(1, 0.65f, 0.3f);
                attackProgress = -0.8f; strikePaused = false;
            }
            attackProgress += Time.deltaTime;
            if (attackProgress < 0) return;
            Vector2 position = Vector2.Lerp(strikeStart, strikeEnd, Mathf.Clamp01(attackProgress / 0.65f));
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
            if (!contact && attackProgress < 0.65f) return;
            contactId = strikeId;
            if (contact) Game.Player.TakeDamage(1);
            contactId = 0;
            Destroy(strike.gameObject); strike = null; attackAt = Time.time + 1.1f;
        }
        public void FrameCamera()
        {
            var camera = Game.GameCamera;
            var original = KarmaArtwork.Settings.mountainBackground;
            camera.orthographicSize = Mathf.Max(25 / camera.aspect, 25 * original.rect.height / original.rect.width);
            float halfWidth = camera.orthographicSize * camera.aspect;
            float x = Mathf.Clamp(Game.Player.Position.x + 4, -25 + halfWidth, 155 - halfWidth);
            camera.transform.position = new Vector3(x, camera.orthographicSize * 0.48f, -10);
            GetComponent<KarmaTutorialBackdrop>().Frame(camera);
        }
    }
}
