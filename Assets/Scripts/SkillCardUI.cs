using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillCardUI : MonoBehaviour
{
    [SerializeField] private TMP_Text slotText;
    [SerializeField] private TMP_Text iconText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image backgroundImage;

    public void SetSkill(int slotIndex, PlayerSkillType skill)
    {
        if (slotText != null)
        {
            slotText.text = $"{slotIndex + 1}";
        }

        SkillDisplayData data = GetDisplayData(skill);

        if (iconText != null)
        {
            iconText.text = data.icon;
        }

        if (nameText != null)
        {
            nameText.text = data.name;
        }

        if (descriptionText != null)
        {
            descriptionText.text = data.description;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = skill == PlayerSkillType.None
            ? new Color32(80, 80, 80, 180)
            : new Color32(30, 120, 160, 200);
        }
    }

    private SkillDisplayData GetDisplayData(PlayerSkillType skill)
    {
        switch (skill)
        {
            case PlayerSkillType.DoubleJump:
                return new SkillDisplayData(
                    "↑↑",
                    "ダブルジャンプ",
                    "空中で1回追加ジャンプ"
                );

            case PlayerSkillType.RapidFire:
                return new SkillDisplayData(
                    "連",
                    "連射強化",
                    "射撃間隔が短くなる"
                );

            case PlayerSkillType.BulletSpeedUp:
                return new SkillDisplayData(
                    "速",
                    "弾速強化",
                    "弾の速度が上がる"
                );

            case PlayerSkillType.DamageReduction:
                return new SkillDisplayData(
                    "盾",
                    "ダメージ軽減",
                    "受けるダメージを減らす"
                );

            default:
                return new SkillDisplayData(
                    "-",
                    "なし",
                    "選択できるスキルがありません"
                );
        }
    }

    private readonly struct SkillDisplayData
    {
        public readonly string icon;
        public readonly string name;
        public readonly string description;

        public SkillDisplayData(string icon, string name, string description)
        {
            this.icon = icon;
            this.name = name;
            this.description = description;
        }
    }
}