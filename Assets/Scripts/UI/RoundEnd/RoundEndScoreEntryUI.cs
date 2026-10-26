using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RoundEndUIが生成する一覧行へ、チーム色と勝利数を設定する。
/// </summary>
public class RoundEndScoreEntryUI : MonoBehaviour
{
    [SerializeField] private Image colorIcon;
    [SerializeField] private TMP_Text scoreText;

    public void Setup(Color color, int winCount)
    {
        if (colorIcon != null)
        {
            colorIcon.color = color;
        }

        if (scoreText != null)
        {
            scoreText.text = $"{winCount}勝";
        }
    }
}