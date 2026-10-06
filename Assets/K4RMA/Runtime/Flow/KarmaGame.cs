using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    // Composition root: create this one component in an empty scene.
    public sealed partial class KarmaGame : MonoBehaviour
    {
        [SerializeField] KarmaConfig config;
        public KarmaConfig Config { get { return config; } }
        public RunPhase Phase { get; private set; } = RunPhase.Title;
        public KarmaPlayer Player { get; private set; }
        public readonly List<KarmaEnemy> Enemies = new List<KarmaEnemy>();
        public readonly List<KarmaProjectile> Projectiles = new List<KarmaProjectile>();
        public Transform World { get; private set; }
        public int StageIndex { get; private set; }
        public bool Paused { get; private set; }
        public bool IsCombat { get { return Phase == RunPhase.Combat && !Paused; } }
        public bool CanMove { get { return !Paused && (Phase == RunPhase.Combat || Phase == RunPhase.Gate); } }
        public float Elapsed { get; private set; }
        public float DamageTaken { get; set; }
        public int Retries { get; private set; }
        public readonly float[] BossSplits = new float[4];
        public Essence? PendingChoice { get { return Player == null ? (Essence?)null : Player.Essences.Pending; } }
        public Essence LastSacrifice { get; private set; }
        public Camera GameCamera { get; private set; }
        Transform runRoot;
        float transitionLeft;
        bool ownsConfig;
        void Awake()
        {
            if (config == null) { config = ScriptableObject.CreateInstance<KarmaConfig>(); ownsConfig = true; }
            KarmaVisuals.UseMaterial(config.spriteMaterial);
            GameCamera = Camera.main;
            if (GameCamera == null)
            {
                var cameraObject = new GameObject("K4RMA Camera");
                cameraObject.transform.SetParent(transform);
                GameCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }
            GameCamera.orthographic = true; GameCamera.orthographicSize = 6.8f;
            GameCamera.clearFlags = CameraClearFlags.SolidColor;
            GameCamera.backgroundColor = new Color(0.065f, 0.075f, 0.12f);
            GameCamera.transform.rotation = Quaternion.identity;
            GameCamera.transform.position = new Vector3(12, 4.4f, -10);
            gameObject.AddComponent<KarmaHUD>().Initialize(this);
            Time.timeScale = 1;
        }
        void Update()
        {
            UpdateHitFeedback();
            if (Phase == RunPhase.Title)
            {
                if (KarmaInput.Pressed(KeyCode.Return)) StartRun();
                else if (KarmaInput.Pressed(KeyCode.S)) ShowPage(RunPhase.Story);
                else if (KarmaInput.Pressed(KeyCode.H)) ShowPage(RunPhase.Controls);
                return;
            }
            if (Phase == RunPhase.Story || Phase == RunPhase.Controls)
            {
                if (KarmaInput.Pressed(KeyCode.Escape)) ReturnToTitle();
                return;
            }
            if (KarmaInput.Pressed(KeyCode.Escape))
            {
                if (Phase == RunPhase.Choosing && !Paused) CancelChoice(); else TogglePause();
            }
            if (KarmaInput.Pressed(KeyCode.T) && (Paused || Phase == RunPhase.Victory || Phase == RunPhase.Defeat)) { ReturnToTitle(); return; }
            if (KarmaInput.Pressed(KeyCode.R) && (Paused || Phase == RunPhase.Victory || Phase == RunPhase.Defeat)) { StartRun(); return; }
            if (Paused) return;
            if (Phase == RunPhase.Combat || Phase == RunPhase.Gate || Phase == RunPhase.Choosing || Phase == RunPhase.Transition)
                Elapsed += Time.deltaTime;
            if (Phase == RunPhase.Gate && Player.Position.x > 35.5f && KarmaInput.Pressed(KeyCode.F))
                Phase = RunPhase.Choosing;
            if (Phase == RunPhase.Choosing)
            {
                if (KarmaInput.Pressed(KeyCode.Alpha1)) Choose(Essence.Flame);
                else if (KarmaInput.Pressed(KeyCode.Alpha2)) Choose(Essence.Dash);
                else if (KarmaInput.Pressed(KeyCode.Alpha3)) Choose(Essence.Ward);
                else if (KarmaInput.Pressed(KeyCode.Return)) ConfirmChoice();
            }
            if (Phase == RunPhase.Transition)
            {
                transitionLeft -= Time.deltaTime;
                if (transitionLeft <= 0) { StageIndex++; LoadRoom(false); }
            }
            if (Phase == RunPhase.Defeat && KarmaInput.Pressed(KeyCode.R)) RetryRoom();
        }

        void OnDisable() { Time.timeScale = 1; }
        void OnDestroy() { if (ownsConfig && config != null) Destroy(config); }
    }
}
