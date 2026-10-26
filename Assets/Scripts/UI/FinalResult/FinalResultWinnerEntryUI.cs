using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FinalResultUIが生成する勝者行へ、チーム色と勝利文言を設定する。
/// </summary>
public class FinalResultWinnerEntryUI : MonoBehaviour
{
    [SerializeField] private Image winnerColorIcon;
    [SerializeField] private TMP_Text championSuffixText;

    public void Setup(Color teamColor)
    {
        if (winnerColorIcon != null)
        {
            winnerColorIcon.color = teamColor;
        }

        if (championSuffixText != null)
        {
            championSuffixText.text = "の優勝！";
        }
    }
}