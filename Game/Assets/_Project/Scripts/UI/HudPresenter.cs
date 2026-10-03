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
        [SerializeField] Image playerFill;
        [SerializeField] Image bossFill;
        [SerializeField] Image interactGlyph;
        [SerializeField] Image[] abilitySlots;
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

            if (banner != null)
                banner.text = string.Empty;
            if (abilityLabel != null)
                abilityLabel.text = string.Empty;
            if (prompt != null)
                prompt.text = string.Empty;
            if (interactGlyph != null)
            {
                bool showGlyph = run != null && run.State != null && run.State.Phase == RunPhase.Altar
                    && run.Altar != null && run.Altar.PlayerInside;
                interactGlyph.enabled = showGlyph;
            }

            PaintSlots();
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

        void PaintSlots()
        {
            if (abilitySlots == null)
                return;
            var state = abilities != null ? abilities.State : null;
            bool surrendered = state != null && state.SacrificedAbilityId == PrototypeIds.Projectile;
            for (int i = 0; i < abilitySlots.Length; i++)
            {
                if (abilitySlots[i] == null)
                    continue;
                bool shotSlot = i == 1;
                if (shotSlot && surrendered)
                    abilitySlots[i].color = new Color(0.25f, 0.25f, 0.28f, 0.45f);
                else if (shotSlot)
                    abilitySlots[i].color = new Color(0.55f, 0.86f, 1f, 0.95f);
                else
                    abilitySlots[i].color = new Color(0.75f, 0.72f, 0.62f, 0.55f);
            }
        }
    }
}
