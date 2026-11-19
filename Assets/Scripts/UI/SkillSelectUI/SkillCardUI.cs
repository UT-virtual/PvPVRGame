using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillCardUI : MonoBehaviour
{
    [SerializeField] private TMP_Text slotText;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image backgroundImage;

    private PlayerSkillType currentSkill;
    private bool isSelected;

    public void SetSkill(int slotIndex, PlayerSkillType skill, Sprite icon)
    {
        currentSkill = skill;

        if (slotText != null)
        {
            slotText.text = $"{slotIndex + 1}";
        }

        SkillDisplayData data = GetDisplayData(skill);

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = skill != PlayerSkillType.None && icon != null;
            iconImage.preserveAspect = true;
        }

        if (nameText != null)
        {
            nameText.text = data.name;
        }

        if (descriptionText != null)
        {
            descriptionText.text = data.description;
        }

        ApplyVisual();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (backgroundImage != null)
        {
            if (isSelected)
            {
                backgroundImage.color = new Color32(70, 180, 220, 255);
            }
            else if (currentSkill == PlayerSkillType.None)
            {
                backgroundImage.color = new Color32(80, 80, 80, 230);
            }
            else
            {
                backgroundImage.color = new Color32(30, 120, 160, 245);
            }
        }

        transform.localScale = isSelected
            ? new Vector3(1.05f, 1.05f, 1.0f)
            : Vector3.one;
    }

    private SkillDisplayData GetDisplayData(PlayerSkillType skill)
    {
        switch (skill)
        {
            case PlayerSkillType.DoubleJump:
                return new SkillDisplayData(
                    "ダブルジャンプ",
                    "空中でも1回ジャンプできる"
                );

            case PlayerSkillType.RapidFire:
                return new SkillDisplayData(
                    "連射強化",
                    "射撃間隔が短くなる"
                );

            case PlayerSkillType.DamageReduction:
                return new SkillDisplayData(
                    "ダメージ軽減",
                    "受けるダメージを半減させる"
                );
            
            case PlayerSkillType.XRayVision:
                return new SkillDisplayData(
                    "透視",
                    "他プレイヤーを透過し壁越しに索敵"
                );

            case PlayerSkillType.MoveSpeedUp:
                return new SkillDisplayData(
                    "移動速度強化",
                    "移動速度が2倍になる"
                );
            
            case PlayerSkillType.SlowFall:
                return new SkillDisplayData(
                    "低重力",
                    "一定時間、落下がゆっくりになる"
                );

            case PlayerSkillType.Shrink:
                return new SkillDisplayData(
                    "縮小",
                    "与えるダメージが下がるが、体が縮小する"
                );

            case PlayerSkillType.DelayedDamageInvincible:
                return new SkillDisplayData(
                    "ダメージ保留",
                    "5秒間無敵。終了後に受けたダメージをまとめて受ける"
                );

            case PlayerSkillType.InstantReload:
                return new SkillDisplayData(
                    "即時装填",
                    "10秒間、リロードが即座に完了する"
                );
            

            case PlayerSkillType.HeavyBulletReload:
                return new SkillDisplayData(
                    "重弾装填",
                    "連射しにくいが高威力・高速の弾を5発装填"
                );

            case PlayerSkillType.NextShotDamageBoost:
                return new SkillDisplayData(
                    "次弾強化",
                    "次に撃つ弾のダメージが1.5倍になる"
                );

            default:
                return new SkillDisplayData(
                    "なし",
                    "選択できるスキルがありません"
                );
        }
    }

    private readonly struct SkillDisplayData
    {
        public readonly string name;
        public readonly string description;

        public SkillDisplayData(string name, string description)
        {
            this.name = name;
            this.description = description;
        }
    }
}
