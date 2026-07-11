using System.Collections.Generic;
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

    [Header("VR Two Slot Display")]
    [SerializeField] private GameObject secondSlotRoot;
    [SerializeField] private Image secondIconImage;
    [SerializeField] private Image secondDimOverlay;
    [SerializeField] private TMP_Text secondSkillNameText;
    [SerializeField] private TMP_Text secondCooldownText;
    [SerializeField] private Vector2 generatedSecondSlotOffset = new Vector2(0.0f, -115.0f);

    [Header("Skill Icons")]
    [SerializeField] private SkillIconEntry[] skillIcons;

    private PlayerSkillController skillController;
    private PlayerController playerController;
    private SkillSlotView firstSlotView;
    private SkillSlotView secondSlotView;
    private bool triedCreateSecondSlot;

    private sealed class SkillSlotView
    {
        public GameObject Root;
        public Image IconImage;
        public Image DimOverlay;
        public TMP_Text SkillNameText;
        public TMP_Text CooldownText;
    }

    public void SetupSkillController(PlayerSkillController targetSkillController)
    {
        SetupSkillController(targetSkillController, null);
    }

    public void SetupSkillController(
        PlayerSkillController targetSkillController,
        PlayerController targetPlayerController
    )
    {
        skillController = targetSkillController;
        playerController = targetPlayerController;
        UpdateView();
    }

    private void Awake()
    {
        firstSlotView = new SkillSlotView
        {
            Root = root != null ? root : gameObject,
            IconImage = iconImage,
            DimOverlay = dimOverlay,
            SkillNameText = skillNameText,
            CooldownText = cooldownText
        };

        secondSlotView = new SkillSlotView
        {
            Root = secondSlotRoot,
            IconImage = secondIconImage,
            DimOverlay = secondDimOverlay,
            SkillNameText = secondSkillNameText,
            CooldownText = secondCooldownText
        };

        UpdateView();
    }

    private void Update()
    {
        UpdateView();
    }

    private void UpdateView()
    {
        bool hasController = skillController != null;
        bool shouldShowVrSlots = hasController &&
            playerController != null &&
            playerController.IsCurrentInputVR;

        if (shouldShowVrSlots)
        {
            UpdateVrView();
            return;
        }

        HideSecondSlot();

        PlayerSkillType currentSkill = hasController
            ? skillController.CurrentSelectedRoundSkill
            : PlayerSkillType.None;
        bool hasSkill = currentSkill != PlayerSkillType.None;

        SetSlotVisible(firstSlotView, hasController && hasSkill);

        if (!hasController || !hasSkill)
        {
            return;
        }

        float cooldownRemaining = skillController.CurrentSkillCooldownRemaining;
        bool canUse = skillController.CanUseCurrentSelectedSkill;

        UpdateSlotView(firstSlotView, currentSkill, canUse, cooldownRemaining);
    }

    private void UpdateVrView()
    {
        EnsureSecondSlotView();

        PlayerSkillType firstSkill = skillController.GetRoundSkillForSlot(0);
        PlayerSkillType secondSkill = skillController.GetRoundSkillForSlot(1);

        UpdateSlotView(
            firstSlotView,
            firstSkill,
            skillController.CanUseSkillSlot(0),
            skillController.GetSkillCooldownRemainingForSlot(0)
        );

        UpdateSlotView(
            secondSlotView,
            secondSkill,
            skillController.CanUseSkillSlot(1),
            skillController.GetSkillCooldownRemainingForSlot(1)
        );
    }

    private void UpdateSlotView(
        SkillSlotView slotView,
        PlayerSkillType skill,
        bool canUse,
        float cooldownRemaining
    )
    {
        if (slotView == null)
        {
            return;
        }

        bool hasSkill = skill != PlayerSkillType.None;
        SetSlotVisible(slotView, hasSkill);

        if (!hasSkill)
        {
            return;
        }

        Sprite icon = GetIcon(skill);

        if (slotView.IconImage != null)
        {
            slotView.IconImage.sprite = icon;
            slotView.IconImage.enabled = icon != null;

            if (slotView.DimOverlay == null)
            {
                slotView.IconImage.color = canUse
                    ? Color.white
                    : new Color(0.35f, 0.35f, 0.35f, 1.0f);
            }
            else
            {
                slotView.IconImage.color = Color.white;
            }
        }

        if (slotView.SkillNameText != null)
        {
            slotView.SkillNameText.text = GetSkillName(skill);
        }

        bool isCoolingDown = cooldownRemaining > 0.05f;

        if (slotView.DimOverlay != null)
        {
            slotView.DimOverlay.gameObject.SetActive(!canUse);
        }

        if (slotView.CooldownText != null)
        {
            slotView.CooldownText.gameObject.SetActive(isCoolingDown);
            slotView.CooldownText.text = Mathf.CeilToInt(cooldownRemaining).ToString();
        }
    }

    private void SetSlotVisible(SkillSlotView slotView, bool visible)
    {
        if (slotView == null || slotView.Root == null)
        {
            return;
        }

        if (slotView.Root.activeSelf != visible)
        {
            slotView.Root.SetActive(visible);
        }
    }

    private void HideSecondSlot()
    {
        SetSlotVisible(secondSlotView, false);
    }

    private void EnsureSecondSlotView()
    {
        if (secondSlotView != null && secondSlotView.Root != null)
        {
            return;
        }

        if (triedCreateSecondSlot)
        {
            return;
        }

        triedCreateSecondSlot = true;

        if (firstSlotView == null ||
            firstSlotView.Root == null ||
            firstSlotView.Root == gameObject ||
            firstSlotView.Root.transform.parent == null)
        {
            return;
        }

        GameObject generatedRoot = Instantiate(
            firstSlotView.Root,
            firstSlotView.Root.transform.parent
        );

        generatedRoot.name = $"{firstSlotView.Root.name}_Slot2";

        RectTransform generatedRect = generatedRoot.GetComponent<RectTransform>();
        if (generatedRect != null)
        {
            generatedRect.anchoredPosition += generatedSecondSlotOffset;
        }

        Transform sourceRoot = firstSlotView.Root.transform;
        Transform clonedRoot = generatedRoot.transform;

        secondSlotView = new SkillSlotView
        {
            Root = generatedRoot,
            IconImage = FindClonedComponent(firstSlotView.IconImage, sourceRoot, clonedRoot),
            DimOverlay = FindClonedComponent(firstSlotView.DimOverlay, sourceRoot, clonedRoot),
            SkillNameText = FindClonedComponent(firstSlotView.SkillNameText, sourceRoot, clonedRoot),
            CooldownText = FindClonedComponent(firstSlotView.CooldownText, sourceRoot, clonedRoot)
        };
    }

    private T FindClonedComponent<T>(T sourceComponent, Transform sourceRoot, Transform clonedRoot)
        where T : Component
    {
        if (sourceComponent == null || sourceRoot == null || clonedRoot == null)
        {
            return null;
        }

        string path = GetRelativePath(sourceRoot, sourceComponent.transform);
        Transform clonedTransform = string.IsNullOrEmpty(path)
            ? clonedRoot
            : clonedRoot.Find(path);

        return clonedTransform != null
            ? clonedTransform.GetComponent<T>()
            : null;
    }

    private string GetRelativePath(Transform rootTransform, Transform targetTransform)
    {
        if (rootTransform == null ||
            targetTransform == null ||
            targetTransform == rootTransform)
        {
            return string.Empty;
        }

        List<string> names = new List<string>();
        Transform current = targetTransform;

        while (current != null && current != rootTransform)
        {
            names.Add(current.name);
            current = current.parent;
        }

        if (current != rootTransform)
        {
            return string.Empty;
        }

        names.Reverse();
        return string.Join("/", names);
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