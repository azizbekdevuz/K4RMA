using UnityEngine;
using UnityEngine.UI;

namespace K4RMA
{
    public class HudPresenter : MonoBehaviour
    {
        [SerializeField] RunDirector run;
        [SerializeField] Health playerHealth;
        [SerializeField] Health stageOneHealth;
        [SerializeField] Health stageTwoHealth;
        [SerializeField] PlayerAbilityState abilities;
        [SerializeField] PlayerCombat combat;
        [SerializeField] Image playerFill;
        [SerializeField] Image bossFill;
        [SerializeField] Text playerLabel;
        [SerializeField] Text bossLabel;
        [SerializeField] Text abilityLabel;
        [SerializeField] Text banner;
        [SerializeField] Text prompt;

        string message = "";
        float messageUntil;

        public void ShowMessage(string text, float seconds = 0f)
        {
            message = text ?? "";
            messageUntil = seconds > 0f ? Time.unscaledTime + seconds : 0f;
            if (banner != null)
                banner.text = message;
        }

        void Update()
        {
            if (messageUntil > 0f && Time.unscaledTime >= messageUntil)
            {
                message = string.Empty;
                messageUntil = 0f;
            }

            DrawHealth(playerLabel, playerFill, playerHealth, "Player");
            var bossHealth = ActiveBossHealth();
            string bossName = "Guardian";
            if (run != null && run.State != null && run.State.StageIndex >= 2 && run.StageTwoBoss != null && run.StageTwoBoss.Config != null)
                bossName = run.StageTwoBoss.Config.stageLabel;
            else if (run != null && run.StageOneBoss != null && run.StageOneBoss.Config != null)
                bossName = run.StageOneBoss.Config.stageLabel;
            DrawHealth(bossLabel, bossFill, bossHealth, bossName);

            if (abilityLabel != null)
                abilityLabel.text = BuildAbilityText();
            if (banner != null)
                banner.text = message;
            if (prompt != null)
                prompt.text = BuildPrompt();
        }

        Health ActiveBossHealth()
        {
            if (run != null && run.State != null && run.State.StageIndex >= 2 && stageTwoHealth != null && stageTwoHealth.gameObject.activeInHierarchy)
                return stageTwoHealth;
            return stageOneHealth;
        }

        static void DrawHealth(Text label, Image fill, Health health, string name)
        {
            int current = health != null ? health.Current : 0;
            int max = health != null ? health.MaxHealth : 1;
            if (label != null)
                label.text = $"{name}  {current}/{max}";
            if (fill != null)
                fill.fillAmount = max <= 0 ? 0f : (float)current / max;
        }

        string BuildAbilityText()
        {
            var state = abilities != null ? abilities.State : null;
            bool sacrificed = state != null && state.SacrificedAbilityId == PrototypeIds.Projectile;
            bool deflecting = state != null && state.CounterAbilityId == PrototypeIds.Deflect;
            string shot = sacrificed
                ? "<color=#8d8680>Projectile</color>"
                : "<color=#d7f4ff>Projectile</color>";
            string deflect = deflecting
                ? (combat != null && combat.DeflectOpen ? "<color=#ffffff>Deflect</color>" : "<color=#8fdfff>Deflect</color>")
                : "<color=#66707f>Deflect</color>";
            return $"Dash          {shot}          Guard\n              {deflect}";
        }

        string BuildPrompt()
        {
            if (run == null || run.State == null)
                return string.Empty;

            switch (run.State.Phase)
            {
                case RunPhase.Altar:
                    return run.Altar != null && run.Altar.PlayerInside
                        ? "E    Surrender the shot"
                        : string.Empty;
                case RunPhase.Defeat:
                    return "R    Try again";
                case RunPhase.SliceComplete:
                    return "R    Again";
                default:
                    if (run.State.CounterAbilityId == PrototypeIds.Deflect)
                        return combat != null && combat.DeflectOpen ? "Deflect" : "K    Deflect the shot";
                    return string.Empty;
            }
        }
    }
}
