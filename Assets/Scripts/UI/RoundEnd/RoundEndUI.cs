using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoundEndUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Round Result")]
    [SerializeField] private TMP_Text roundEndText;
    [SerializeField] private Image winnerColorIcon;
    [SerializeField] private TMP_Text winSuffixText;

    [Header("Score List")]
    [SerializeField] private TMP_Text scoreTitleText;
    [SerializeField] private Transform scoreContentRoot;
    [SerializeField] private GameObject scoreEntryPrefab;

    private readonly List<GameObject> scoreEntryObjects = new();

    private void Awake()
    {
        Hide();
    }

    public void Show(
        int roundNumber,
        PlayerHealth winner,
        IReadOnlyList<PlayerHealth> players,
        IReadOnlyDictionary<PlayerHealth, int> points
    )
    {
        SetVisible(true);

        if (roundEndText != null)
        {
            roundEndText.text = $"ラウンド{roundNumber}終了";
        }

        ApplyWinner(winner);
        RefreshScoreList(players, points);
    }

    public void ShowTeamScores(
    int roundNumber,
    int winnerTeamIndex,
    int teamMask,
    int redWins,
    int blueWins,
    int greenWins,
    int yellowWins
    )
    {
        SetVisible(true);

        if (roundEndText != null)
        {
            roundEndText.text = $"ラウンド{roundNumber}終了";
        }

        ApplyWinnerByTeamIndex(winnerTeamIndex);
        RefreshScoreListByTeam(teamMask, redWins, blueWins, greenWins, yellowWins);
    }

    public void Hide()
    {
        ClearScoreEntries();
        SetVisible(false);
    }

    private void ApplyWinner(PlayerHealth winner)
    {
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

    private void RefreshScoreList(
        IReadOnlyList<PlayerHealth> players,
        IReadOnlyDictionary<PlayerHealth, int> points
    )
    {
        ClearScoreEntries();

        if (scoreTitleText != null)
        {
            scoreTitleText.text = "現在の勝利数";
        }

        if (scoreContentRoot == null || scoreEntryPrefab == null)
        {
            Debug.LogWarning("[RoundEndUI] scoreContentRoot or scoreEntryPrefab is null.");
            return;
        }

        if (players == null)
        {
            return;
        }

        List<PlayerHealth> displayPlayers = new();

        foreach (PlayerHealth player in players)
        {
            if (player != null)
            {
                displayPlayers.Add(player);
            }
        }

        displayPlayers.Sort((a, b) => GetTeamSortOrder(a).CompareTo(GetTeamSortOrder(b)));

        foreach (PlayerHealth player in displayPlayers)
        {
            int winCount = 0;

            if (points != null && points.TryGetValue(player, out int point))
            {
                winCount = point;
            }

            GameObject entryObject = Instantiate(scoreEntryPrefab, scoreContentRoot);
            entryObject.SetActive(true);
            scoreEntryObjects.Add(entryObject);

            RoundEndScoreEntryUI entryUI = entryObject.GetComponent<RoundEndScoreEntryUI>();

            if (entryUI == null)
            {
                Debug.LogWarning("[RoundEndUI] RoundEndScoreEntryUI was not found on scoreEntryPrefab.");
                continue;
            }

            entryUI.Setup(GetTeamColor(player), winCount);
        }
    }

    private void ClearScoreEntries()
    {
        foreach (GameObject entryObject in scoreEntryObjects)
        {
            if (entryObject != null)
            {
                Destroy(entryObject);
            }
        }

        scoreEntryObjects.Clear();
    }

    private void SetVisible(bool visible)
    {
        GameObject targetRoot = root != null ? root : gameObject;

        if (targetRoot.activeSelf != visible)
        {
            targetRoot.SetActive(visible);
        }
    }

    private int GetTeamSortOrder(PlayerHealth player)
    {
        if (player == null || !player.HasTeamAssigned)
        {
            return 999;
        }

        switch (player.Team)
        {
            case RoundManager.TeamColor.Red:
                return 0;

            case RoundManager.TeamColor.Blue:
                return 1;

            case RoundManager.TeamColor.Green:
                return 2;

            case RoundManager.TeamColor.Yellow:
                return 3;

            default:
                return 999;
        }
    }

    private void ApplyWinnerByTeamIndex(int winnerTeamIndex)
    {
        if (winnerTeamIndex < 0)
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

        RoundManager.TeamColor winnerTeam = (RoundManager.TeamColor)winnerTeamIndex;

        if (winnerColorIcon != null)
        {
            winnerColorIcon.gameObject.SetActive(true);
            winnerColorIcon.color = GetTeamColor(winnerTeam);
        }

        if (winSuffixText != null)
        {
            winSuffixText.text = "の勝利";
        }
    }

    private void RefreshScoreListByTeam(
        int teamMask,
        int redWins,
        int blueWins,
        int greenWins,
        int yellowWins
    )
    {
        ClearScoreEntries();

        if (scoreTitleText != null)
        {
            scoreTitleText.text = "現在の勝利数";
        }

        if (scoreContentRoot == null || scoreEntryPrefab == null)
        {
            Debug.LogWarning("[RoundEndUI] scoreContentRoot or scoreEntryPrefab is null.");
            return;
        }

        CreateScoreEntryIfTeamExists(teamMask, RoundManager.TeamColor.Red, redWins);
        CreateScoreEntryIfTeamExists(teamMask, RoundManager.TeamColor.Blue, blueWins);
        CreateScoreEntryIfTeamExists(teamMask, RoundManager.TeamColor.Green, greenWins);
        CreateScoreEntryIfTeamExists(teamMask, RoundManager.TeamColor.Yellow, yellowWins);
    }

    private void CreateScoreEntryIfTeamExists(
        int teamMask,
        RoundManager.TeamColor team,
        int winCount
    )
    {
        int bit = 1 << (int)team;

        if ((teamMask & bit) == 0)
        {
            return;
        }

        GameObject entryObject = Instantiate(scoreEntryPrefab, scoreContentRoot);
        entryObject.SetActive(true);
        scoreEntryObjects.Add(entryObject);

        RoundEndScoreEntryUI entryUI = entryObject.GetComponent<RoundEndScoreEntryUI>();

        if (entryUI == null)
        {
            Debug.LogWarning("[RoundEndUI] RoundEndScoreEntryUI was not found on scoreEntryPrefab.");
            return;
        }

        entryUI.Setup(GetTeamColor(team), winCount);
    }

    private Color GetTeamColor(PlayerHealth player)
    {
        if (player == null || !player.HasTeamAssigned)
        {
            return Color.gray;
        }

        return GetTeamColor(player.Team);
    }

    private Color GetTeamColor(RoundManager.TeamColor team)
    {
        switch (team)
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