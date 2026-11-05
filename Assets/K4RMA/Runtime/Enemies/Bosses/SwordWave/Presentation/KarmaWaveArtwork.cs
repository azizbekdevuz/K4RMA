using UnityEngine;
namespace KarmaPrototype
{
    // Alpha >= 128 source-pixel bounds map to the unchanged damage rectangle.
    // Transparent corners remain part of that rectangle; no released outline.
    public static class KarmaWaveArtwork
    {
        static string Resolve(string pose)
        {
            switch (pose)
            {
                // SW01 and SW02 share the exact same complete source Sprite.
                case "SW02_AimedSlash": case "SW02_AimedSlash_v2":
                case "SW01_CrescentWave_v4": case "SWBasic_CrescentWave_v3": return "Sword_Crescent_v6";
                case "SW03_GroundScar_v5": case "SW03_GroundScar_v6": return "SW03_GroundScar_v7";
                case "SW03_RisingBlade_v5": case "SW03_RisingBlade_v6": return "SW03_RisingBlade_v7";
                case "SW04_CounterBlade_v2": return "SW04_CounterBlade_v6";
                case "SW03_HitFlash_v5": case "Sword_HitBurst_v2": return "Sword_Impact_v6";
                default: return pose;
            }
        }
        public static string Pose(EnemyAttack attack, bool stationary = false)
        {
            if (attack == EnemyAttack.SW02) return "SW02_AimedSlash";
            if (attack == EnemyAttack.SWBasic) return "SWBasic_CrescentWave_v3";
            if (attack == EnemyAttack.SW01) return "SW01_CrescentWave_v4";
            if (attack == EnemyAttack.SW03 && stationary) return "SW03_RisingBlade_v5";
            if (attack == EnemyAttack.SW04) return "SW04_CounterBlade_v2";
            if (attack == EnemyAttack.SW05) return "SW05_AerialSword";
            return "SW01_StaggeredWave";
        }
        public static Rect CorePixels(string pose)
        {
            pose = Resolve(pose);
            switch (pose)
            {
                case "Sword_Crescent_v6": return new Rect(59, 66, 942, 1412);
                case "SW03_GroundScar_v6": return new Rect(30, 37, 966, 179);
                // Bottom is the bright ground-contact point, not the glow's tail.
                case "SW03_RisingBlade_v6": return new Rect(162, 146, 700, 1344);
                case "SW03_GroundScar_v7": return new Rect(80, 287, 2018, 172);
                case "SW03_RisingBlade_v7": return new Rect(325, 140, 374, 1348);
                case "SW03_HitCut_v7": return new Rect(113, 108, 1076, 1038);
                case "SW04_CounterBlade_v6": return new Rect(46, 251, 2073, 244);
                case "Sword_Impact_v6": return new Rect(41, 91, 398, 364);
                case "Sword_Release_v6": return new Rect(70, 95, 391, 327);
                case "Sword_GuardRing_v6": return new Rect(27, 51, 459, 409);
                case "Sword_Shard_v6": return new Rect(4, 9, 121, 13);
                case "SW01_CrescentWave_v4": return new Rect(95, 52, 908, 1436);
                case "SWBasic_CrescentWave_v3": return new Rect(114, 36, 893, 1454);
                case "SW02_AimedSlash_v2": return new Rect(124, 38, 878, 1445);
                case "SW01_AimedSlash_UpperHalf_v2": return new Rect(134, 0, 866, 715);
                case "SW01_AimedSlash_LowerHalf_v2": return new Rect(124, 38, 878, 730);
                case "SW03_GroundScar_v5": return new Rect(33, 167, 2094, 359);
                case "SW03_RisingBlade_v5": return new Rect(37, 10, 650, 2135);
                case "SW03_HitFlash_v5": return new Rect(64, 64, 1121, 1093);
                case "SWBasic_Wave_v2": return new Rect(76, 122, 1989, 489);
                case "SW01_StaggeredWave_v3": return new Rect(26, 27, 2119, 670);
                case "SW03_GroundScar_v4": return new Rect(12, 208, 2147, 173);
                case "SW03_RisingBlade_v4": return new Rect(74, 6, 743, 1751);
                case "SW03_HitFlash_v4": return new Rect(43, 30, 1176, 1160);
                case "Sword_HitBurst_v2": return new Rect(153, 155, 1027, 935);
                case "SW03_GroundScar_v3": return new Rect(25, 96, 2133, 481);
                case "SW03_RisingBlade_v3": return new Rect(289, 25, 303, 1714);
                case "SW03_HitFlash_v3": return new Rect(79, 44, 1198, 1098);
                case "SW01_StaggeredWave_v2": return new Rect(84, 197, 2018, 328);
                case "SW03_GroundScar_v2": return new Rect(35, 232, 1920, 293);
                case "SW03_RisingBlade_v2": return new Rect(34, 23, 661, 2138);
                case "SW04_CounterBlade_v2": return new Rect(132, 167, 1994, 370);
                case "SW01_StaggeredWave": return new Rect(43, 35, 1373, 279);
                case "SW02_AimedSlash": return new Rect(124, 38, 878, 1445);
                case "SW03_RisingBlade": return new Rect(39, 41, 233, 1325);
                case "SW03_GroundImpact": return new Rect(39, 40, 359, 344);
                case "SW04_CounterWave": return new Rect(41, 32, 1325, 233);
                case "SW05_AerialSword": return new Rect(44, 51, 1366, 282);
                default: throw new System.ArgumentOutOfRangeException(nameof(pose));
            }
        }
        public static Sprite Sprite(string pose)
        { return KarmaArtwork.Sprite("SwordBossVFX", Resolve(pose)); }
        public static void Apply(SpriteRenderer renderer, string pose, Vector2 size, Vector2 velocity)
        {
            renderer.sprite = Sprite(pose);
            if (renderer.sprite == null) throw new System.InvalidOperationException("Missing SwordBossVFX/" + pose);
            // New poses load independently; legacy SW05 and shared
            // SW03_GroundImpact references keep their original library sprites.
            KarmaVisuals.ApplyMaterial(renderer);
            if (Resolve(pose) == "SW03_GroundScar_v7") KarmaSwordSkillSurface.Apply(renderer, 5);
            else if (Resolve(pose) == "SW04_CounterBlade_v6") KarmaSwordSkillSurface.Apply(renderer, 3);
            if (pose == "SW05_AerialSword")
            {
                var material = Resources.Load<Material>("KarmaAerialSword");
                if (material == null) throw new System.InvalidOperationException("Missing KarmaAerialSword material");
                renderer.sharedMaterial = material;
            }
            Rect core = CorePixels(pose);
            float ppu = renderer.sprite.pixelsPerUnit;
            // The new crescent faces +X. Mirror horizontal lanes without turning
            // the upper half upside down when firing left.
            bool crescent = pose == "SW02_AimedSlash" || pose == "SW02_AimedSlash_v2"
                || pose == "Sword_Crescent_v6"
                || pose == "SW01_CrescentWave_v4" || pose == "SWBasic_CrescentWave_v3"
                || pose == "SW01_AimedSlash_UpperHalf_v2" || pose == "SW01_AimedSlash_LowerHalf_v2";
            bool horizontalSlash = crescent && Mathf.Abs(velocity.y) < 0.000001f;
            renderer.flipX = horizontalSlash && velocity.x < 0;
            renderer.flipY = false;
            renderer.transform.localScale = new Vector3(size.x * ppu / core.width, size.y * ppu / core.height, 1);
            renderer.transform.localRotation = Quaternion.Euler(0, 0, !horizontalSlash && velocity.sqrMagnitude > 0.000001f
                ? Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg : 0);
        }
    }
}
