using UnityEngine;
using UnityEngine.Rendering;

namespace KarmaPrototype
{
    // Read projection only: compensate the moon even when an Editor viewport is non-square.
    // No camera, background transform, framing, or encounter parameter is ever written here.
    public sealed class KarmaMoonProjection : MonoBehaviour
    {
        SpriteRenderer picture;
        MaterialPropertyBlock properties;
        float sourceAspect;
        Sprite ownedSprite;
        internal void Initialize(SpriteRenderer renderer, float aspect, Sprite owned = null)
        {
            picture = renderer; sourceAspect = aspect; ownedSprite = owned; properties = new MaterialPropertyBlock();
        }
        void OnDestroy() { if (ownedSprite != null) Destroy(ownedSprite); }
        void OnEnable() { RenderPipelineManager.beginCameraRendering += Prepare; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= Prepare; }
        void Prepare(ScriptableRenderContext context, Camera camera)
        {
            if (picture == null || properties == null || camera.pixelHeight <= 0 || camera.aspect <= 0) return;
            float projectionAspect = camera.pixelRect.width / camera.pixelRect.height / camera.aspect;
            picture.GetPropertyBlock(properties);
            properties.SetFloat("_MoonStretch", sourceAspect * projectionAspect);
            picture.SetPropertyBlock(properties);
        }
    }
}
