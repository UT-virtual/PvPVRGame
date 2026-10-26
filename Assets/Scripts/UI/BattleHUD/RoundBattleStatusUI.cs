using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// 同期されたラウンド数・タイマーと、撃破数優先のランキングを各端末の戦闘HUDへ表示する。
/// 勝敗やサドンデス効果の確定は行わず、戦闘以外のフェーズでは表示を閉じる。
/// 観戦専用端末では最初から、対戦者はサドンデスからランキングを表示する。
/// </summary>
public sealed class RoundBattleStatusUI : MonoBehaviour
{
    private const float RankingWidth = 420f;
    private RoundManager manager;
    private Canvas canvas;
    private RectTransform canvasRect;
    private GameObject timerRoot;
    private GameObject rankingRoot;
    private TMP_Text timerText;
    private TMP_Text roundProgressText;
    private TMP_Text rankingTitle;
    private TMP_Text announcement;
    private RoundDamageRankingUI ranking;
    private VRWorldSpaceUIFollower follower;
    private int displayedRevision = -1;
    private int announcedVersion = -1;
    private float announcementUntil;
    private int displayedSeconds = -1;
    private int displayedRound = -1;
    private int displayedTotalRounds = -1;
    private bool displayedHealth;

    /// <summary>
    /// RoundManagerの初期化から一度だけ呼ぶ。既存UIのフォントと色アイコンを再利用し、Sceneごとの手動設定を不要にする。
    /// </summary>
    public void Initialize(RoundManager roundManager, TMP_FontAsset font, Sprite icon)
    {
        if (canvas != null) return;
        manager = roundManager;
        GameObject canvasObject = new("RoundBattleStatus", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.layer = 5;
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.sortingOrder = 30;
        canvasRect = (RectTransform)canvasObject.transform;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        follower = canvasObject.AddComponent<VRWorldSpaceUIFollower>();

        RectTransform progressRect = RoundDamageRankingUI.CreateRect("RoundProgress", canvasRect,
            new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(244f, 52f));
        Image progressBackground = progressRect.gameObject.AddComponent<Image>();
        progressBackground.color = new Color(0.02f, 0.04f, 0.08f, 0.85f);
        progressBackground.raycastTarget = false;
        roundProgressText = RoundDamageRankingUI.CreateText("Round", progressRect, font, 28f,
            Vector2.zero, progressRect.sizeDelta);
        roundProgressText.alignment = TextAlignmentOptions.Center;

        RectTransform timerRect = RoundDamageRankingUI.CreateRect("RoundTimer", canvasRect,
            new Vector2(0.5f, 1f), new Vector2(-90f, -20f), new Vector2(180f, 52f));
        timerRoot = timerRect.gameObject;
        Image timerBackground = timerRoot.AddComponent<Image>();
        timerBackground.color = new Color(0.02f, 0.04f, 0.08f, 0.85f);
        timerBackground.raycastTarget = false;
        timerText = RoundDamageRankingUI.CreateText("Time", timerRect, font, 36f, Vector2.zero, timerRect.sizeDelta);
        timerText.alignment = TextAlignmentOptions.Center;

        RectTransform rankingRect = RoundDamageRankingUI.CreateRect("RankingPanel", canvasRect,
            new Vector2(1f, 1f), new Vector2(-RankingWidth - 24f, -84f), new Vector2(RankingWidth, RoundDamageRankingUI.PreferredHeight + 34f));
        rankingRoot = rankingRect.gameObject;
        rankingTitle = RoundDamageRankingUI.CreateText("RankingTitle", rankingRect, font, 20f,
            Vector2.zero, new Vector2(RankingWidth, 32f));
        ranking = RoundDamageRankingUI.Create(rankingRect, font, icon, RankingWidth);
        RectTransform rankingRows = (RectTransform)ranking.transform;
        rankingRows.anchorMin = rankingRows.anchorMax = new Vector2(0f, 1f);
        rankingRows.pivot = new Vector2(0f, 1f);
        rankingRows.anchoredPosition = new Vector2(0f, -34f);

        announcement = RoundDamageRankingUI.CreateText("SuddenDeath", canvasRect, font, 24f,
            Vector2.zero, new Vector2(420f, 84f));
        RectTransform announcementRect = announcement.rectTransform;
        announcementRect.anchorMin = announcementRect.anchorMax = new Vector2(0.5f, 1f);
        announcementRect.anchoredPosition = new Vector2(-270f, -84f);
        announcement.alignment = TextAlignmentOptions.Center;
        announcement.text = "サドンデス！\n攻撃力2倍・全員透視・回復なし";
        announcement.color = new Color32(255, 110, 70, 255);
        canvas.enabled = false;
    }

    private void Update()
    {
        if (manager == null || canvas == null || !manager.IsNetworkReady)
        {
            if (canvas != null) canvas.enabled = false;
            return;
        }

        bool playing = manager.IsRoundPlaying;
        // 確定記録は保持しても、このHUDを結果・スキル選択フェーズへ持ち越さない。
        canvas.enabled = playing;
        if (!canvas.enabled) return;

        ConfigureCanvas();
        timerRoot.SetActive(playing);
        bool suddenDeath = manager.IsSuddenDeath;
        if (playing)
        {
            int seconds = Mathf.CeilToInt(manager.RoundRemainingSeconds);
            if (seconds != displayedSeconds)
            {
                timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
                displayedSeconds = seconds;
            }
            timerText.color = suddenDeath ? new Color32(255, 110, 70, 255) : Color.white;
        }

        bool dedicatedSpectator = manager.ConnectedParticipants.TryGet(manager.Runner.LocalPlayer,
            out ParticipantRole role) && role == ParticipantRole.Spectator;
        rankingRoot.SetActive(suddenDeath || dedicatedSpectator);
        if (displayedRound != manager.CurrentRound || displayedTotalRounds != manager.TotalRoundCount)
        {
            roundProgressText.text = $"ラウンド {manager.CurrentRound}/{manager.TotalRoundCount}";
            rankingTitle.text = $"ROUND {manager.CurrentRound}　ランキング";
            displayedRound = manager.CurrentRound;
            displayedTotalRounds = manager.TotalRoundCount;
        }
        if (rankingRoot.activeSelf && (displayedRevision != manager.DamageRevision || displayedHealth != dedicatedSpectator))
        {
            ranking.Refresh(manager, dedicatedSpectator);
            displayedRevision = manager.DamageRevision;
            displayedHealth = dedicatedSpectator;
        }

        if (suddenDeath && announcedVersion != manager.DamageRoundVersion)
        {
            announcedVersion = manager.DamageRoundVersion;
            announcementUntil = Time.unscaledTime + 3f;
            RoundSeManager.Instance?.NotifySuddenDeath();
        }
        announcement.gameObject.SetActive(suddenDeath && Time.unscaledTime < announcementUntil);
    }

    private void ConfigureCanvas()
    {
        bool vr = XRSettings.isDeviceActive;
        RenderMode mode = vr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
        if (canvas.renderMode != mode) canvas.renderMode = mode;
        follower.enabled = vr;
        if (vr)
        {
            canvasRect.sizeDelta = new Vector2(1280f, 720f);
            canvasRect.localScale = Vector3.one * 0.0015f;
            canvas.worldCamera = Camera.main;
        }
        else
        {
            canvasRect.localScale = Vector3.one;
        }
    }
}
