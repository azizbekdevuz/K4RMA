using UnityEngine;
using UnityEngine.Rendering;

namespace KarmaPrototype
{
    // Per-camera visibility only. Actors, collision, AI and authored sprite
    // colours stay intact, including cancellation or a room change mid-shot.
    public sealed class KarmaGrowthFocus : MonoBehaviour
    {
        sealed class Actor
        {
            Transform root;
            SpriteRenderer[] pictures = System.Array.Empty<SpriteRenderer>();
            Color[] colors = System.Array.Empty<Color>();
            bool[] hidden = System.Array.Empty<bool>();
            bool applied;
            public void Bind(Transform target)
            {
                if (root == target) return;
                Restore(); root = target;
                pictures = target != null ? target.GetComponentsInChildren<SpriteRenderer>(true)
                    : System.Array.Empty<SpriteRenderer>();
                colors = new Color[pictures.Length]; hidden = new bool[pictures.Length];
            }
            public void Apply(float opacity)
            {
                Restore();
                for (int i = 0; i < pictures.Length; i++)
                {
                    var picture = pictures[i]; if (picture == null) continue;
                    colors[i] = picture.color; hidden[i] = picture.forceRenderingOff;
                    var color = colors[i]; color.a *= opacity; picture.color = color;
                    picture.forceRenderingOff = hidden[i] || opacity <= 0.001f;
                }
                applied = true;
            }
            public void Restore()
            {
                if (!applied) return;
                for (int i = 0; i < pictures.Length; i++)
                    if (pictures[i] != null)
                    { pictures[i].color = colors[i]; pictures[i].forceRenderingOff = hidden[i]; }
                applied = false;
            }
        }
        readonly Actor player = new Actor(), guardian = new Actor();
        KarmaGame game;
        Transform fallen;
        float shotAge;
        bool guardianShot;
        internal float PlayerOpacity { get; private set; } = 1;
        internal float GuardianOpacity { get; private set; } = 1;

        internal static void Begin(KarmaGame game, Transform fallen)
        {
            var go = new GameObject("Growth shot focus");
            go.transform.SetParent(game.Player.transform.parent, false);
            var focus = go.AddComponent<KarmaGrowthFocus>();
            focus.game = game; focus.fallen = fallen;
            focus.UpdateActors();
        }
        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
            Camera.onPreCull += BeforeCamera;
            Camera.onPostRender += AfterCamera;
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            Camera.onPreCull -= BeforeCamera;
            Camera.onPostRender -= AfterCamera;
            player.Restore(); guardian.Restore();
        }
        void BeginCamera(ScriptableRenderContext context, Camera camera) => BeforeCamera(camera);
        void EndCamera(ScriptableRenderContext context, Camera camera) => AfterCamera(camera);
        void BeforeCamera(Camera camera)
        {
            if (game == null || camera != game.GameCamera || game.Phase != RunPhase.Transition) return;
            UpdateActors();
            player.Apply(PlayerOpacity); guardian.Apply(GuardianOpacity);
        }
        void AfterCamera(Camera camera)
        {
            if (game == null || camera != game.GameCamera) return;
            player.Restore(); guardian.Restore();
        }
        void LateUpdate()
        {
            if (game == null || game.Phase != RunPhase.Transition || game.Player == null)
            { Destroy(gameObject); return; }
            if (guardianShot != game.GuardianInheriting)
            { guardianShot = game.GuardianInheriting; shotAge = 0; }
            if (!game.Paused && !game.GameplayFrozen) shotAge += Time.deltaTime;
            if (guardianShot)
            {
                PlayerOpacity = Mathf.SmoothStep(0, 1, game.GrowthReturnProgress);
                GuardianOpacity = Mathf.SmoothStep(0, 1, Mathf.Clamp01(shotAge / 0.5f));
            }
            else
            {
                PlayerOpacity = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(
                    KarmaPersonalizationVFX.PlayerDuration - 0.5f, KarmaPersonalizationVFX.PlayerDuration, shotAge));
                GuardianOpacity = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(shotAge / 0.5f));
            }
        }
        void UpdateActors()
        {
            player.Bind(game.Player != null ? game.Player.transform : null);
            guardian.Bind(game.GuardianInheriting && game.Enemies.Count > 0
                ? game.Enemies[0].transform : fallen);
        }
    }
}
