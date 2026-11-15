using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 勝者、確定した撃破数優先の順位、累積勝利数を表示する。
/// 結果RPCと同期スナップショットの到着順が異なっても、対象ラウンドの記録を待って描画する。
/// </summary>
public class RoundEndUI : MonoBehaviour
{
    private const float FinalRankingScale = 1.25f;
    private const float FinalRankingTop = 224f;
    private const float ResultHorizontalPadding = 40f;
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
    private int networkResultRound;
    private int rankingRevision = -1;
    private RoundDamageRankingUI finalDamageRanking;
    private readonly int[] teamWins = new int[4];
    private int resultTeamMask;
    private bool rankingShowsHealth;

    private void Awake()
    {
        Hide();
    }

    private void LateUpdate()
    {
        RoundManager manager = RoundManager.Instance;
        if (networkResultRound == 0 || manager == null || !manager.IsNetworkReady) return;
        bool showHealth = manager.ConnectedParticipants.TryGet(manager.Runner.LocalPlayer,
            out ParticipantRole role) && role == ParticipantRole.Spectator;
        if (
            !manager.HasFinalDamageRanking || manager.DamageRoundNumber != networkResultRound ||
            (finalDamageRanking != null && rankingRevision == manager.DamageRevision && rankingShowsHealth == showHealth) ||
            scoreContentRoot == null) return;

        if (finalDamageRanking == null)
        {
            BuildFinalRanking(manager.DamageEntryCount);
        }

        if (finalDamageRanking == null) return;
        finalDamageRanking.Refresh(manager, showHealth);
        rankingRevision = manager.DamageRevision;
        rankingShowsHealth = showHealth;
    }

    private void BuildFinalRanking(int entryCount)
    {
        RectTransform panel = scoreContentRoot.parent as RectTransform;
        if (panel == null) return;

        ClearScoreEntries();
        // 背景パネルを基準に配置し、Sceneの一覧幅やLayoutGroupによる位置ずれを防ぐ。
        scoreContentRoot.gameObject.SetActive(false);
        float width = panel.rect.width - ResultHorizontalPadding * 2f;
        float rankingHeight = RoundDamageRankingUI.GetPreferredHeight(entryCount, FinalRankingScale);
        float contentHeight = rankingHeight + 64f;
        panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, FinalRankingTop + contentHeight + 32f);

        ConfigureResultText(roundEndText, 36f, 32f, width, 44f);
        ConfigureResultText(winSuffixText, 44f, 84f, 260f, 64f);
        ConfigureResultText(scoreTitleText, 30f, 168f, width, 40f);
        if (scoreTitleText != null) scoreTitleText.text = "最終順位";
        if (winSuffixText != null && winnerColorIcon != null && winnerColorIcon.gameObject.activeSelf)
        {
            winSuffixText.rectTransform.anchoredPosition += new Vector2(24f, 0f);
            RectTransform icon = winnerColorIcon.rectTransform;
            SetTopCenteredRect(icon, new Vector2(44f, 44f), new Vector2(-88f, -94f));
        }

        finalDamageRanking = RoundDamageRankingUI.Create(panel,
            scoreTitleText != null ? scoreTitleText.font : null,
            winnerColorIcon != null ? winnerColorIcon.sprite : null, width, FinalRankingScale, entryCount);
        RectTransform rankingRect = (RectTransform)finalDamageRanking.transform;
        SetTopCenteredRect(rankingRect, new Vector2(width, contentHeight), new Vector2(0f, -FinalRankingTop));
        finalDamageRanking.GetComponent<LayoutElement>().preferredHeight = contentHeight;
        scoreEntryObjects.Add(finalDamageRanking.gameObject);

        TMP_Text wins = RoundDamageRankingUI.CreateText("MatchWins", finalDamageRanking.transform,
            scoreTitleText != null ? scoreTitleText.font : null, 26f,
            new Vector2(0f, -rankingHeight - 12f), new Vector2(width, 48f));
        wins.alignment = TextAlignmentOptions.Center;
        wins.enableAutoSizing = true;
        wins.fontSizeMin = 22f;
        wins.fontSizeMax = 26f;
        wins.overflowMode = TextOverflowModes.Ellipsis;
        wins.text = "累計勝利数：";
        for (int i = 0; i < teamWins.Length; i++)
        {
            if ((resultTeamMask & (1 << i)) == 0) continue;
            string color = ColorUtility.ToHtmlStringRGB(RoundDamageRankingUI.GetTeamColor(i));
            wins.text += $" <color=#{color}>■</color> {teamWins[i]}勝";
        }
    }

    private static void ConfigureResultText(TMP_Text text, float fontSize, float top, float width, float height)
    {
        if (text == null) return;
        SetTopCenteredRect(text.rectTransform, new Vector2(width, height), new Vector2(0f, -top));
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.alignment = TextAlignmentOptions.Center;
    }

    private static void SetTopCenteredRect(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.localScale = Vector3.one;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    public void Show(
        int roundNumber,
        PlayerHealth winner,
        IReadOnlyList<PlayerHealth> players,
        IReadOnlyDictionary<PlayerHealth, int> points
    )
    {
        SetVisible(true);
        networkResultRound = 0;

        if (roundEndText != null)
        {
            roundEndText.text = $"ラウンド{roundNumber}終了";
        }

        ApplyWinner(winner);
        RefreshScoreList(players, points);
    }

    /// <summary>
    /// 結果RPCで受け取った勝者と各チームの累積勝利数を表示する。
    /// winnerTeamIndexが負なら引き分け。最終順位は対象ラウンドの同期スナップショットを待って表示する。
    /// </summary>
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

        networkResultRound = roundNumber;
        resultTeamMask = teamMask;
        teamWins[0] = redWins;
        teamWins[1] = blueWins;
        teamWins[2] = greenWins;
        teamWins[3] = yellowWins;

        if (roundEndText != null)
        {
            roundEndText.text = $"ラウンド{roundNumber}終了";
        }

        ApplyWinnerByTeamIndex(winnerTeamIndex);
        ClearScoreEntries();
        if (scoreTitleText != null) scoreTitleText.text = "最終順位";
    }

    public void Hide()
    {
        networkResultRound = 0;
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
                winSuffixText.text = "引き分け";
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
        if (scoreContentRoot != null) scoreContentRoot.gameObject.SetActive(true);
        finalDamageRanking = null;
        rankingRevision = -1;
        foreach (GameObject entryObject in scoreEntryObjects)
        {
            if (entryObject != null)
            {
                entryObject.SetActive(false);
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
            case TeamColor.Red:
                return 0;

            case TeamColor.Blue:
                return 1;

            case TeamColor.Green:
                return 2;

            case TeamColor.Yellow:
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
                winSuffixText.text = "引き分け";
            }

            return;
        }

        TeamColor winnerTeam = (TeamColor)winnerTeamIndex;

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

    private Color GetTeamColor(PlayerHealth player)
    {
        if (player == null || !player.HasTeamAssigned)
        {
            return Color.gray;
        }

        return GetTeamColor(player.Team);
    }

    private Color GetTeamColor(TeamColor team)
    {
        switch (team)
        {
            case TeamColor.Red:
                return new Color32(255, 77, 109, 255);

            case TeamColor.Blue:
                return new Color32(77, 171, 247, 255);

            case TeamColor.Green:
                return new Color32(64, 192, 87, 255);

            case TeamColor.Yellow:
                return new Color32(255, 212, 59, 255);

            default:
                return Color.gray;
        }
    }
}
