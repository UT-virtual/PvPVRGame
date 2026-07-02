using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoundEndUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("UI References")]
    [SerializeField] private TMP_Text roundEndText;
    [SerializeField] private Image winnerColorIcon;
    [SerializeField] private TMP_Text winSuffixText;

    private void Awake()
    {
        Hide();
    }

    public void Show(int roundNumber, PlayerHealth winner)
    {
        SetVisible(true);

        if (roundEndText != null)
        {
            roundEndText.text = $"ラウンド{roundNumber}終了";
        }

        if (winner == null)
        {
            if (winnerColorIcon != null)
            {
                winnerColorIcon.gameObject.SetActive(false);
            }

            if (winSuffixText != null)
            {
                winSuffixText.text = "勝者なし";
            }

            return;
        }

        if (winnerColorIcon != null)
        {
            winnerColorIcon.gameObject.SetActive(true);
            winnerColorIcon.color = GetTeamColor(winner);
        }

        if (winSuffixText != null)
        {
            winSuffixText.text = "の勝利";
        }
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        GameObject targetRoot = root != null ? root : gameObject;

        if (targetRoot.activeSelf != visible)
        {
            targetRoot.SetActive(visible);
        }
    }

    private Color GetTeamColor(PlayerHealth player)
    {
        if (player == null || !player.HasTeamAssigned)
        {
            return Color.gray;
        }

        switch (player.Team)
        {
            case RoundManager.TeamColor.Red:
                return new Color32(255, 77, 109, 255);

            case RoundManager.TeamColor.Blue:
                return new Color32(77, 171, 247, 255);

            case RoundManager.TeamColor.Green:
                return new Color32(64, 192, 87, 255);

            case RoundManager.TeamColor.Yellow:
                return new Color32(255, 212, 59, 255);

            default:
                return Color.gray;
        }
    }
}