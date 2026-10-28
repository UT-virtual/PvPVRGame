using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillHudUI : MonoBehaviour
{
    [System.Serializable]
    private struct SkillIconEntry
    {
        public PlayerSkillType skill;
        public Sprite icon;
    }

    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image dimOverlay;
    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private TMP_Text cooldownText;

    [Header("Skill Icons")]
    [SerializeField] private SkillIconEntry[] skillIcons;

    private PlayerSkillController skillController;

    public void SetupSkillController(PlayerSkillController targetSkillController)
    {
        skillController = targetSkillController;
        UpdateView();
    }

    private void Awake()
    {
        UpdateView();
    }

    private void Update()
    {
        UpdateView();
    }

    private void UpdateView()
    {
        bool hasController = skillController != null;
        PlayerSkillType currentSkill = hasController
            ? skillController.CurrentSelectedRoundSkill
            : PlayerSkillType.None;

        bool hasSkill = currentSkill != PlayerSkillType.None;

        SetRootVisible(hasController && hasSkill);

        if (!hasController || !hasSkill)
        {
            return;
        }

        Sprite icon = GetIcon(currentSkill);

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;

            if (dimOverlay == null)
            {
                iconImage.color = skillController.CanUseCurrentSelectedSkill
                    ? Color.white
                    : new Color(0.35f, 0.35f, 0.35f, 1.0f);
            }
            else
            {
                iconImage.color = Color.white;
            }
        }

        if (skillNameText != null)
        {
            skillNameText.text = GetSkillName(currentSkill);
        }

        float cooldownRemaining = skillController.CurrentSkillCooldownRemaining;
        bool isCoolingDown = cooldownRemaining > 0.05f;
        bool canUse = skillController.CanUseCurrentSelectedSkill;

        if (dimOverlay != null)
        {
            dimOverlay.gameObject.SetActive(!canUse);
        }

        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(isCoolingDown);
            cooldownText.text = Mathf.CeilToInt(cooldownRemaining).ToString();
        }
    }

    private void SetRootVisible(bool visible)
    {
        GameObject targetRoot = root != null ? root : gameObject;

        if (targetRoot.activeSelf != visible)
        {
            targetRoot.SetActive(visible);
        }
    }

    private Sprite GetIcon(PlayerSkillType skill)
    {
        if (skillIcons == null)
        {
            return null;
        }

        foreach (SkillIconEntry entry in skillIcons)
        {
            if (entry.skill == skill)
            {
                return entry.icon;
            }
        }

        return null;
    }

    private string GetSkillName(PlayerSkillType skill)
    {
        switch (skill)
        {
            case PlayerSkillType.DoubleJump:
                return "二段ジャンプ";

            case PlayerSkillType.RapidFire:
                return "速射強化";

            case PlayerSkillType.BulletSpeedUp:
                return "弾速強化";

            case PlayerSkillType.DamageReduction:
                return "防御強化";

            case PlayerSkillType.XRayVision:
                return "透視";

            case PlayerSkillType.MoveSpeedUp:
                return "移動強化";

            case PlayerSkillType.SlowFall:
                return "落下軽減";

            case PlayerSkillType.Shrink:
                return "縮小";

            case PlayerSkillType.DelayedDamageInvincible:
                return "遅延無敵";

            case PlayerSkillType.InstantReload:
                return "即時装填";

            case PlayerSkillType.GravityBurstReload:
                return "拡散曲射弾";

            case PlayerSkillType.HeavyBulletReload:
                return "重弾装填";

            case PlayerSkillType.NextShotDamageBoost:
                return "次弾強化";

            default:
                return "";
        }
    }
}