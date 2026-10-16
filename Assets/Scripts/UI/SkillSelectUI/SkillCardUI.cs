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

    private PlayerSkillType currentSkill;
    private bool isSelected;

    public void SetSkill(int slotIndex, PlayerSkillType skill)
    {
        currentSkill = skill;

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
                    "飛",
                    "ダブルジャンプ",
                    "空中でも1回ジャンプできる"
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
                    "受けるダメージを半減させる"
                );
            
            case PlayerSkillType.XRayVision:
                return new SkillDisplayData(
                    "透",
                    "透視",
                    "他プレイヤーを透過し壁越しに索敵"
                );

            case PlayerSkillType.MoveSpeedUp:
                return new SkillDisplayData(
                    "走",
                    "移動速度強化",
                    "移動速度が2倍になる"
                );
            
            case PlayerSkillType.SlowFall:
                return new SkillDisplayData(
                    "羽",
                    "低重力",
                    "一定時間、落下がゆっくりになる"
                );

            case PlayerSkillType.Shrink:
                return new SkillDisplayData(
                    "縮",
                    "縮小",
                    "与えるダメージが下がるが、体が縮小する"
                );

            case PlayerSkillType.DelayedDamageInvincible:
                return new SkillDisplayData(
                    "耐",
                    "ダメージ保留",
                    "5秒間無敵。終了後に受けたダメージをまとめて受ける"
                );

            case PlayerSkillType.InstantReload:
                return new SkillDisplayData(
                    "弾",
                    "即時装填",
                    "10秒間、リロードが即座に完了する"
                );
            
            case PlayerSkillType.GravityBurstReload:
                return new SkillDisplayData(
                    "炸",
                    "拡散曲射弾",
                    "重力で落ち、着弾時に周囲へダメージを与える弾を5発装填"
                );

            case PlayerSkillType.HeavyBulletReload:
                return new SkillDisplayData(
                    "重",
                    "重弾装填",
                    "連射しにくいが高威力・高速の弾を5発装填"
                );

            case PlayerSkillType.NextShotDamageBoost:
                return new SkillDisplayData(
                    "強",
                    "次弾強化",
                    "次に撃つ弾のダメージが1.5倍になる"
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