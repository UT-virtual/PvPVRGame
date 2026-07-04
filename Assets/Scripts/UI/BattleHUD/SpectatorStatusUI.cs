using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpectatorStatusUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text spectatorText;
    [SerializeField] private Image targetColorIcon;
    [SerializeField] private TMP_Text operationText;

    private SpectatorCameraController spectatorController;

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        spectatorController = FindFirstObjectByType<SpectatorCameraController>();
        ApplyStaticTexts();
        SetVisible(false);
    }

    private void Update()
    {
        if (spectatorController == null)
        {
            spectatorController = FindFirstObjectByType<SpectatorCameraController>();
        }

        if (spectatorController == null || !spectatorController.IsSpectatingActive)
        {
            SetVisible(false);
            return;
        }

        PlayerHealth target = spectatorController.CurrentSpectatorTarget;

        if (target == null)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        ApplyStaticTexts();
        UpdateTargetColor(target);
    }

    private void ApplyStaticTexts()
    {
        if (spectatorText != null)
        {
            spectatorText.text = "観戦中";
        }

        if (operationText != null)
        {
            operationText.text = "観戦対象切り替え: 左スティック";
        }
    }

    private void UpdateTargetColor(PlayerHealth target)
    {
        if (targetColorIcon == null)
        {
            return;
        }

        if (!target.HasTeamAssigned)
        {
            targetColorIcon.color = Color.gray;
            return;
        }

        switch (target.Team)
        {
            case RoundManager.TeamColor.Red:
                targetColorIcon.color = new Color32(255, 77, 109, 255);
                break;

            case RoundManager.TeamColor.Blue:
                targetColorIcon.color = new Color32(77, 171, 247, 255);
                break;

            case RoundManager.TeamColor.Green:
                targetColorIcon.color = new Color32(64, 192, 87, 255);
                break;

            case RoundManager.TeamColor.Yellow:
                targetColorIcon.color = new Color32(255, 212, 59, 255);
                break;

            default:
                targetColorIcon.color = Color.gray;
                break;
        }
    }

    private void SetVisible(bool visible)
    {
        if (root != null && root.activeSelf != visible)
        {
            root.SetActive(visible);
        }
    }
}