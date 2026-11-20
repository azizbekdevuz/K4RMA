using UnityEditor;

namespace KarmaPrototype.EditorTools
{
    public static class KarmaGuardAirborneAssets
    {
        public static void Import()
        {
            AssetDatabase.ImportAsset("Assets/Arts/Player/GD04AirborneHit.png",
                ImportAssetOptions.ForceUpdate);
            KarmaMotionBindings.Apply();
        }
    }
}
