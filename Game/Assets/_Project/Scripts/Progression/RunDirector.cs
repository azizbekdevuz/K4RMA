using UnityEngine;
using UnityEngine.SceneManagement;

namespace K4RMA
{
    public class RunDirector : MonoBehaviour
    {
        [SerializeField] Health playerHealth;
        [SerializeField] Rigidbody playerBody;
        [SerializeField] PlayerAbilityState playerAbilities;
        [SerializeField] BossController stageOneBoss;
        [SerializeField] BossController stageTwoBoss;
        [SerializeField] Altar altar;
        [SerializeField] HudPresenter hud;
        [SerializeField] Transform playerSpawn;
        [SerializeField] PlayerInputReader input;
        [SerializeField] SacrificeSequence sacrificeSequence;

        public RunState State { get; private set; }
        public bool ControlsLocked { get; private set; }
        public Altar Altar => altar;
        public BossController StageOneBoss => stageOneBoss;
        public BossController StageTwoBoss => stageTwoBoss;

        void Awake()
        {
            PrototypePhysics.Apply();
            State = RunState.CreatePrototypeStart();
            if (playerAbilities != null)
                playerAbilities.Bind(State);
            if (stageTwoBoss != null)
                stageTwoBoss.gameObject.SetActive(false);
            if (sacrificeSequence == null)
                sacrificeSequence = GetComponent<SacrificeSequence>();
        }

        public void SetControlsLocked(bool locked)
        {
            ControlsLocked = locked;
            if (playerBody == null)
                return;
            var motor = playerBody.GetComponent<PlayerController>();
            var combat = playerBody.GetComponent<PlayerCombat>();
            if (motor != null)
                motor.enabled = !locked;
            if (combat != null)
                combat.enabled = !locked;
            if (locked)
                playerBody.linearVelocity = Vector3.zero;
        }

        public void BeginSacrifice(string abilityId)
        {
            if (State == null || !SacrificeRules.CanSacrifice(State, abilityId))
                return;
            if (sacrificeSequence != null && sacrificeSequence.IsPlaying)
                return;
            if (sacrificeSequence != null)
                sacrificeSequence.Play(this, abilityId);
            else
                TrySacrifice(abilityId);
        }

        void OnEnable()
        {
            if (playerHealth != null)
                playerHealth.Died += OnPlayerDied;
            Subscribe(stageOneBoss, OnStageOneBossDied);
            Subscribe(stageTwoBoss, OnStageTwoBossDied);
        }

        void OnDisable()
        {
            if (playerHealth != null)
                playerHealth.Died -= OnPlayerDied;
            Unsubscribe(stageOneBoss, OnStageOneBossDied);
            Unsubscribe(stageTwoBoss, OnStageTwoBossDied);
        }

        void Update()
        {
            if (input == null || State == null)
                return;
            if (!input.RestartPressed)
                return;
            if (State.Phase == RunPhase.Defeat || State.Phase == RunPhase.SliceComplete)
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public bool TrySacrifice(string abilityId)
        {
            if (State == null || !SacrificeRules.TrySacrifice(State, abilityId))
                return false;

            playerAbilities?.NotifyChanged();
            if (playerBody != null && playerSpawn != null)
            {
                playerBody.position = playerSpawn.position;
                playerBody.linearVelocity = Vector3.zero;
            }

            if (stageOneBoss != null)
                stageOneBoss.gameObject.SetActive(false);
            if (stageTwoBoss != null)
                stageTwoBoss.gameObject.SetActive(true);

            playerHealth?.RestoreFull();
            return true;
        }

        void OnPlayerDied(Health _)
        {
            if (State == null || State.Phase == RunPhase.Defeat)
                return;
            State.Phase = RunPhase.Defeat;
            if (stageOneBoss != null)
                stageOneBoss.enabled = false;
            if (stageTwoBoss != null)
                stageTwoBoss.enabled = false;
        }

        void OnStageOneBossDied(Health _)
        {
            if (State == null || State.Phase != RunPhase.Fight || State.StageIndex != 1)
                return;
            if (playerHealth != null && !playerHealth.IsAlive)
                return;
            State.Phase = RunPhase.Altar;
        }

        void OnStageTwoBossDied(Health _)
        {
            if (State == null || State.StageIndex != 2)
                return;
            if (playerHealth != null && !playerHealth.IsAlive)
                return;
            State.Phase = RunPhase.SliceComplete;
        }

        static void Subscribe(BossController boss, System.Action<Health> handler)
        {
            if (boss == null)
                return;
            var health = boss.GetComponent<Health>();
            if (health != null)
                health.Died += handler;
        }

        static void Unsubscribe(BossController boss, System.Action<Health> handler)
        {
            if (boss == null)
                return;
            var health = boss.GetComponent<Health>();
            if (health != null)
                health.Died -= handler;
        }
    }
}
