using TMPro;
using UnityEngine;
using UnityEngine.UI;

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