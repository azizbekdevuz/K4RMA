using System;
using UnityEditor;
using UnityEngine;
using KarmaPrototype;

// Apply the reviewed combat values without replacing movement, fonts or artwork.
public static class ApplyCombatTuning
{
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before saving tuning.");
        var config = AssetDatabase.LoadAssetAtPath<KarmaConfig>("Assets/K4RMA/Settings/KarmaConfig.asset");
        Undo.RecordObject(config, "Player combat balance");
        config.playerHealth = 160; config.swordDamage = 60; config.enemyDamage = 14;
        config.flameDamage = 72; config.healAfterSacrifice = 45;
        config.Techniques.risingDamage = 60; config.Techniques.counterDamage = 85;
        var t = config.BossTuning;
        t.stageOneHealth = 1050; t.stageTwoHealth = 1550; t.stageThreeHealth = 2050; t.finalHealth = 2800;
        t.damageMultiplier = 1.15f; t.moveMultiplier = 1.7f; t.decisionMultiplier = 0.42f; t.recoveryMultiplier = 0.72f;
        t.enrageThreshold = 0.5f; t.enrageTempo = 0.82f; t.warningMultiplier = 0.9f;
        t.ultimateCooldown = 20; t.swordSpeed = 32; t.swordLifetime = 1.65f;
        t.aerialSwordCount = 7; t.aerialSwordGap = 0.4f; t.crossingWarning = t.crossingReset = 0.65f;
        t.autoShieldPeriod = 8; t.shieldFragments = 4; t.fragmentAimWarning = 0.5f;
        t.trialAttackCount = 7; t.trialAttackGap = 0.65f;
        EditorUtility.SetDirty(config);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/K4RMA/Runtime/Presentation/KarmaCombatGlow.shader");
        if (shader == null) throw new InvalidOperationException("Combat glow shader was not imported.");
        const string path = "Assets/K4RMA/Resources/KarmaCombatGlow.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        else { Undo.RecordObject(material, "Combat glow shader"); material.shader = shader; EditorUtility.SetDirty(material); }
        AssetDatabase.SaveAssets();
        return "Combat tuning saved: 160 HP, 60 sword / 72 wave / 60 rising / 85 counter damage, 45 transition healing; boss patterns retained.";
    }
}
