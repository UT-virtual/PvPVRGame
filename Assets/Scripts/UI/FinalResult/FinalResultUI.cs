using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FinalResultUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Result Announcement")]
    [SerializeField] private Image resultAnnouncementImage;

    [Header("Winner Text")]
    [SerializeField] private TMP_Text winnerText;

    [Header("Winner Fade Out")]
    [SerializeField] private float winnerFadeOutDuration = 0.8f;

    [Header("Title Text")]
    [SerializeField] private TMP_Text titleText;

    [Header("Winner Ranking")]
    [SerializeField] private Transform winnerContentRoot;
    [SerializeField] private GameObject winnerEntryPrefab;

    [Header("Confetti")]
    [SerializeField] private ParticleSystem confettiParticle;

    [Header("Timing")]
    [SerializeField] private float announcementSlideDuration = 0.6f;
    [SerializeField] private float announcementDisplayTime = 2.0f;
    [SerializeField] private float beforeWinnerDisplayDelay = 0.5f;
    [SerializeField] private float winnerDisplayTime = 3.0f;
    [SerializeField] private float rankingDisplayTime = 5.0f;

    [Header("Slide")]
    [SerializeField] private float slideStartOffset = 1000f;

    private readonly List<GameObject> winnerEntryObjects = new();

    private Coroutine resultCoroutine;


    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        Hide();
    }


    // =========================================================
    // Public
    // =========================================================

    /// <summary>
    /// 最終結果UIを表示する。
    /// </summary>
    public void ShowWinners(int winnerTeamMask)
    {
        SetVisible(true);

        ClearWinnerEntries();

        // 最初は勝利チームを非表示
        if (winnerText != null)
        {
            winnerText.gameObject.SetActive(false);
        }

        // タイトルは使用しない
        if (titleText != null)
        {
            titleText.text = "";
            titleText.gameObject.SetActive(false);
        }

        // 順位表も最初は非表示
        if (winnerContentRoot != null)
        {
            winnerContentRoot.gameObject.SetActive(false);
        }

        // 結果発表画像を表示
        if (resultAnnouncementImage != null)
        {
            resultAnnouncementImage.gameObject.SetActive(true);
        }

        // 紙吹雪は最初は停止
        if (confettiParticle != null)
        {
            confettiParticle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        // 既に演出中なら停止
        if (resultCoroutine != null)
        {
            StopCoroutine(resultCoroutine);
        }

        // 演出開始
        resultCoroutine =
            StartCoroutine(
                ShowResultSequence(winnerTeamMask)
            );
    }


    /// <summary>
    /// 最終結果UIを非表示にする。
    /// </summary>
    public void Hide()
    {
        if (resultCoroutine != null)
        {
            StopCoroutine(resultCoroutine);
            resultCoroutine = null;
        }

        ClearWinnerEntries();

        // 結果発表画像
        if (resultAnnouncementImage != null)
        {
            resultAnnouncementImage.gameObject.SetActive(false);
        }

        // 勝利チーム文字
        if (winnerText != null)
        {
            winnerText.gameObject.SetActive(false);
        }

        // タイトル
        if (titleText != null)
        {
            titleText.text = "";
            titleText.gameObject.SetActive(false);
        }

        // 順位表
        if (winnerContentRoot != null)
        {
            winnerContentRoot.gameObject.SetActive(false);
        }

        // 紙吹雪
        if (confettiParticle != null)
        {
            confettiParticle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        SetVisible(false);
    }


    // =========================================================
    // Result Sequence
    // =========================================================

    private IEnumerator ShowResultSequence(int winnerTeamMask)
    {
        // =====================================================
        // ① 「結果発表！」を左からスライド
        // =====================================================

        if (resultAnnouncementImage != null)
        {
            resultAnnouncementImage.gameObject.SetActive(true);

            RectTransform rect =
                resultAnnouncementImage.rectTransform;

            Vector2 endPosition =
                rect.anchoredPosition;

            Vector2 startPosition =
                endPosition;

            startPosition.x -= slideStartOffset;

            rect.anchoredPosition =
                startPosition;

            float elapsed = 0f;

            while (elapsed < announcementSlideDuration)
            {
                elapsed += Time.deltaTime;

                float t =
                    elapsed / announcementSlideDuration;

                // SmoothStep
                t = t * t * (3f - 2f * t);

                rect.anchoredPosition =
                    Vector2.Lerp(
                        startPosition,
                        endPosition,
                        t
                    );

                yield return null;
            }

            rect.anchoredPosition =
                endPosition;
        }


        // =====================================================
        // ② 「結果発表！」を表示
        // =====================================================

        yield return new WaitForSeconds(
            announcementDisplayTime
        );


        // =====================================================
        // ③ 「結果発表！」を消す
        // =====================================================

        if (resultAnnouncementImage != null)
        {
            resultAnnouncementImage.gameObject.SetActive(false);
        }


        // =====================================================
        // ④ 少し待つ
        // =====================================================

        yield return new WaitForSeconds(
            beforeWinnerDisplayDelay
        );


        // =====================================================
        // ⑤ 勝利チームを表示
        // =====================================================

        ShowWinnerText(winnerTeamMask);


        // =====================================================
        // ⑥ 勝利チーム＋紙吹雪を表示
        // =====================================================

        yield return new WaitForSeconds(
            winnerDisplayTime
        );


        // =====================================================
        // ⑦ 勝利チームの文字をフェードアウト
        // =====================================================

        yield return StartCoroutine(
            FadeOutWinnerText()
        );


        // =====================================================
        // ⑧ 紙吹雪を停止
        // =====================================================

        if (confettiParticle != null)
        {
            confettiParticle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }


        // =====================================================
        // ⑨ 順位表を表示
        // =====================================================

        ShowWinnerRanking(winnerTeamMask);


        // =====================================================
        // ⑩ 順位表を表示しておく
        // =====================================================

        yield return new WaitForSeconds(
            rankingDisplayTime
        );


        // =====================================================
        // ⑪ 最後に全部消す
        // =====================================================

        Hide();

        resultCoroutine = null;
    }


    // =========================================================
    // Winner Text
    // =========================================================

    private void ShowWinnerText(int winnerTeamMask)
    {
        if (winnerText == null)
        {
            Debug.LogWarning(
                "[FinalResultUI] winnerText is null."
            );

            return;
        }

        List<TeamColor> winningTeams =
            GetWinningTeams(winnerTeamMask);

        winnerText.gameObject.SetActive(true);


        // -----------------------------------------------------
        // 勝利チームなし
        // -----------------------------------------------------

        if (winningTeams.Count == 0)
        {
            winnerText.text =
                "勝利チームなし";

            winnerText.color =
                Color.white;
        }


        // -----------------------------------------------------
        // 1チーム勝利
        // -----------------------------------------------------

        else if (winningTeams.Count == 1)
        {
            TeamColor winningTeam =
                winningTeams[0];

            winnerText.text =
                $"{GetTeamName(winningTeam)}チームの勝利！";

            Color color =
                GetTeamColor(winningTeam);

            color.a = 1f;

            winnerText.color = color;
        }


        // -----------------------------------------------------
        // 複数チーム
        // -----------------------------------------------------

        else
        {
            winnerText.text =
                "引き分け！";

            winnerText.color =
                Color.white;
        }


        // -----------------------------------------------------
        // 紙吹雪
        // -----------------------------------------------------

        if (confettiParticle != null)
        {
            confettiParticle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );

            confettiParticle.Play();
        }
    }


    // =========================================================
    // Ranking
    // =========================================================

    private void ShowWinnerRanking(int winnerTeamMask)
    {
        if (
            winnerContentRoot == null ||
            winnerEntryPrefab == null
        )
        {
            Debug.LogWarning(
                "[FinalResultUI] " +
                "winnerContentRoot or winnerEntryPrefab is null."
            );

            return;
        }

        // 念のため既存の順位表を削除
        ClearWinnerEntries();

        // 順位表を表示
        winnerContentRoot.gameObject.SetActive(true);

        // 元々の順位表の生成処理
        CreateWinnerEntryIfTeamExists(
            winnerTeamMask,
            TeamColor.Red
        );

        CreateWinnerEntryIfTeamExists(
            winnerTeamMask,
            TeamColor.Blue
        );

        CreateWinnerEntryIfTeamExists(
            winnerTeamMask,
            TeamColor.Green
        );

        CreateWinnerEntryIfTeamExists(
            winnerTeamMask,
            TeamColor.Yellow
        );
    }


    private void CreateWinnerEntryIfTeamExists(
        int winnerTeamMask,
        TeamColor team
    )
    {
        int bit =
            1 << (int)team;

        // このチームが勝利チームでなければ何もしない
        if ((winnerTeamMask & bit) == 0)
        {
            return;
        }

        GameObject entryObject =
            Instantiate(
                winnerEntryPrefab,
                winnerContentRoot
            );

        entryObject.SetActive(true);

        winnerEntryObjects.Add(
            entryObject
        );

        FinalResultWinnerEntryUI entryUI =
            entryObject.GetComponent<
                FinalResultWinnerEntryUI
            >();

        if (entryUI == null)
        {
            Debug.LogWarning(
                "[FinalResultUI] " +
                "FinalResultWinnerEntryUI was not found " +
                "on winnerEntryPrefab."
            );

            return;
        }

        entryUI.Setup(
            GetTeamColor(team)
        );
    }


    // =========================================================
    // Winner Teams
    // =========================================================

    private List<TeamColor> GetWinningTeams(
        int winnerTeamMask
    )
    {
        List<TeamColor> teams =
            new();

        AddTeamIfExists(
            teams,
            winnerTeamMask,
            TeamColor.Red
        );

        AddTeamIfExists(
            teams,
            winnerTeamMask,
            TeamColor.Blue
        );

        AddTeamIfExists(
            teams,
            winnerTeamMask,
            TeamColor.Green
        );

        AddTeamIfExists(
            teams,
            winnerTeamMask,
            TeamColor.Yellow
        );

        return teams;
    }


    private void AddTeamIfExists(
        List<TeamColor> teams,
        int winnerTeamMask,
        TeamColor team
    )
    {
        int bit =
            1 << (int)team;

        if ((winnerTeamMask & bit) != 0)
        {
            teams.Add(team);
        }
    }


    // =========================================================
    // Clear Ranking
    // =========================================================

    private void ClearWinnerEntries()
    {
        foreach (
            GameObject entryObject
            in winnerEntryObjects
        )
        {
            if (entryObject != null)
            {
                Destroy(entryObject);
            }
        }

        winnerEntryObjects.Clear();
    }


    // =========================================================
    // Root
    // =========================================================

    private void SetVisible(bool visible)
    {
        GameObject targetRoot =
            root != null
                ? root
                : gameObject;

        if (
            targetRoot.activeSelf != visible
        )
        {
            targetRoot.SetActive(
                visible
            );
        }
    }


    // =========================================================
    // Team Name
    // =========================================================

    private string GetTeamName(
        TeamColor team
    )
    {
        switch (team)
        {
            case TeamColor.Red:
                return "赤";

            case TeamColor.Blue:
                return "青";

            case TeamColor.Green:
                return "緑";

            case TeamColor.Yellow:
                return "黄";

            default:
                return "不明";
        }
    }


    // =========================================================
    // Team Color
    // =========================================================

    private Color GetTeamColor(
        TeamColor team
    )
    {
        switch (team)
        {
            case TeamColor.Red:
                return new Color32(
                    255,
                    77,
                    109,
                    255
                );

            case TeamColor.Blue:
                return new Color32(
                    77,
                    171,
                    247,
                    255
                );

            case TeamColor.Green:
                return new Color32(
                    64,
                    192,
                    87,
                    255
                );

            case TeamColor.Yellow:
                return new Color32(
                    255,
                    212,
                    59,
                    255
                );

            default:
                return Color.gray;
        }
    }

    private IEnumerator FadeOutWinnerText()
    {
        if (winnerText == null)
        {
            yield break;
        }

        Color startColor = winnerText.color;
        Color endColor = startColor;
        endColor.a = 0f;

        float elapsed = 0f;

        while (elapsed < winnerFadeOutDuration)
        {
            elapsed += Time.deltaTime;

            float t =
                elapsed / winnerFadeOutDuration;

            winnerText.color =
                Color.Lerp(startColor, endColor, t);

            yield return null;
        }

        winnerText.color = endColor;
        winnerText.gameObject.SetActive(false);
    }
}