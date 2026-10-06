using System.Collections.Generic;
using UnityEngine;

namespace KarmaPrototype
{
    public sealed partial class KarmaGame
    {
        public void StartRun()
        {
            ResetHitFeedback();
            Paused = false; Time.timeScale = 1;
            if (runRoot != null) { runRoot.gameObject.SetActive(false); Destroy(runRoot.gameObject); }
            Enemies.Clear(); Projectiles.Clear();
            runRoot = new GameObject("Generated K4RMA Run").transform;
            runRoot.SetParent(transform, false);
            var p = new GameObject("Player"); p.transform.SetParent(runRoot, false);
            Player = p.AddComponent<KarmaPlayer>(); Player.Initialize(this);
            Player.Essences.Cancel(); LastSacrifice = Essence.Flame; StageIndex = 0; Elapsed = 0; DamageTaken = 0; Retries = 0;
            for (int i = 0; i < BossSplits.Length; i++) BossSplits[i] = 0;
            LoadRoom(true);
        }
        void LoadRoom(bool fullHealth)
        {
            ResetHitFeedback();
            if (World != null) { World.gameObject.SetActive(false); Destroy(World.gameObject); }
            Enemies.Clear(); Projectiles.Clear();
            World = new GameObject("Stage " + (StageIndex + 1)).transform;
            World.SetParent(runRoot, false);
            KarmaStage.Build(this, World, StageIndex);
            Player.ResetForRoom(KarmaStage.Spawn, fullHealth);
            var enemyObject = new GameObject(StageIndex == 3 ? "마지막 수호자" : "수호자");
            enemyObject.transform.SetParent(World, false);
            enemyObject.transform.position = new Vector3(27, 1.15f, 0);
            var enemy = enemyObject.AddComponent<KarmaEnemy>(); enemy.Initialize(this, StageIndex);
            Enemies.Add(enemy);
            Phase = RunPhase.Combat;
            GameCamera.transform.position = new Vector3(12, 4.4f, -10);
        }
        public void EnemyDefeated(KarmaEnemy enemy)
        {
            BossSplits[StageIndex] = enemy.FightSeconds;
            Enemies.Remove(enemy);
            if (Enemies.Count != 0) return;
            Phase = StageIndex == 3 ? RunPhase.Victory : RunPhase.Choosing; Player.Essences.Cancel();
            Player.ClearStatus();
            ClearProjectiles();
        }
        public void Choose(Essence essence)
        {
            if (Phase != RunPhase.Choosing || Paused || !Player.Essences.HasActive(essence)) return;
            Player.Essences.Preview(essence);
        }
        public void CancelChoice() { if (Phase == RunPhase.Choosing) Player.Essences.Cancel(); }
        public void ConfirmChoice()
        {
            if (Phase != RunPhase.Choosing || Paused || !PendingChoice.HasValue) return;
            Essence essence;
            if (!Player.Essences.Confirm(out essence)) return;
            Player.Essences.Cancel();
            LastSacrifice = essence;
            Player.Heal(config.healAfterSacrifice);
            Phase = RunPhase.Transition; transitionLeft = 2.5f;
            KarmaVisuals.Flash(World, KarmaStage.Gate, new Vector2(3, 6), config.ColorOf(essence), 2);
        }
        public void PlayerDied()
        {
            if (Phase == RunPhase.Defeat || Phase == RunPhase.Victory) return;
            Phase = RunPhase.Defeat; ClearProjectiles();
            if (Player != null) { Player.ClearStatus(); Player.GetComponentInChildren<KarmaCharacterAnimator>().Die(); }
        }
        void ClearProjectiles()
        {
            foreach (var p in Projectiles.ToArray()) if (p != null) Destroy(p.gameObject);
            Projectiles.Clear();
        }
        public void RetryRoom()
        {
            if (Phase != RunPhase.Defeat) return;
            StartRun();
        }
        public void ShowPage(RunPhase page)
        {
            if (Phase == RunPhase.Title && (page == RunPhase.Story || page == RunPhase.Controls)) Phase = page;
        }
        public void ReturnToTitle()
        {
            ResetHitFeedback();
            Paused = false; Time.timeScale = 1; ClearProjectiles();
            if (runRoot != null) { runRoot.gameObject.SetActive(false); Destroy(runRoot.gameObject); }
            runRoot = null; World = null; Player = null; Enemies.Clear();
            Phase = RunPhase.Title;
        }
        public void TogglePause()
        {
            if (Phase != RunPhase.Combat && Phase != RunPhase.Gate && Phase != RunPhase.Choosing) return;
            Paused = !Paused; Time.timeScale = Paused ? 0 : 1;
        }
    }
}
