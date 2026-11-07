using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

/// <summary>
/// 同期されたラウンド数・タイマーと、撃破数優先のランキングを各端末の戦闘HUDへ表示する。
/// 共通Canvasは観戦案内にも使い、戦闘用の表示だけをフェーズで切り替える。
/// 観戦専用端末では最初から、対戦者はサドンデスからランキングを表示する。
/// </summary>
public sealed class RoundBattleStatusUI : MonoBehaviour
{
    private const float RankingWidth = 300f;
    private RoundManager manager;
    private Canvas canvas;
    private RectTransform canvasRect;
    private GameObject timerRoot;
    private GameObject battleRoot;
    private GameObject rankingRoot;
    private TMP_Text timerText;
    private TMP_Text roundProgressText;
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
        RectTransform battleRect = RoundDamageRankingUI.CreateRect("BattleWidgets", canvasRect,
            Vector2.zero, Vector2.zero, Vector2.zero);
        battleRect.anchorMin = Vector2.zero;
        battleRect.anchorMax = Vector2.one;
        battleRect.offsetMin = battleRect.offsetMax = Vector2.zero;
        battleRoot = battleRect.gameObject;
        RoundHappeningUI happeningUI = battleRoot.AddComponent<RoundHappeningUI>();
        happeningUI.Initialize(manager, font);
        SpectatorStatusUI spectatorStatus = canvasObject.AddComponent<SpectatorStatusUI>();
        spectatorStatus.Initialize(manager, canvasRect, font, icon);

        RectTransform progressRect = RoundDamageRankingUI.CreateRect("RoundProgress", battleRect,
            new Vector2(0f, 1f), new Vector2(16f, -14f), new Vector2(172f, 40f));
        Image progressBackground = progressRect.gameObject.AddComponent<Image>();
        progressBackground.color = new Color(0.02f, 0.04f, 0.08f, 0.6f);
        progressBackground.raycastTarget = false;
        roundProgressText = RoundDamageRankingUI.CreateText("Round", progressRect, font, 20f,
            Vector2.zero, progressRect.sizeDelta);
        roundProgressText.alignment = TextAlignmentOptions.Center;

        RectTransform timerRect = RoundDamageRankingUI.CreateRect("RoundTimer", battleRect,
            new Vector2(0.5f, 1f), new Vector2(-56f, -14f), new Vector2(112f, 40f));
        timerRoot = timerRect.gameObject;
        Image timerBackground = timerRoot.AddComponent<Image>();
        timerBackground.color = new Color(0.02f, 0.04f, 0.08f, 0.6f);
        timerBackground.raycastTarget = false;
        timerText = RoundDamageRankingUI.CreateText("Time", timerRect, font, 26f, Vector2.zero, timerRect.sizeDelta);
        timerText.alignment = TextAlignmentOptions.Center;

        RectTransform rankingRect = RoundDamageRankingUI.CreateRect("RankingPanel", battleRect,
            new Vector2(1f, 1f), new Vector2(-RankingWidth - 16f, -60f),
            new Vector2(RankingWidth, RoundDamageRankingUI.GetPreferredHeight(NetworkSessionController.MaxCombatantCount, compact: true) + 24f));
        rankingRoot = rankingRect.gameObject;
        TMP_Text rankingTitle = RoundDamageRankingUI.CreateText("RankingTitle", rankingRect, font, 16f,
            Vector2.zero, new Vector2(RankingWidth, 24f));
        rankingTitle.text = "ランキング";
        ranking = RoundDamageRankingUI.Create(rankingRect, font, icon, RankingWidth, compact: true);
        RectTransform rankingRows = (RectTransform)ranking.transform;
        rankingRows.anchorMin = rankingRows.anchorMax = new Vector2(0f, 1f);
        rankingRows.pivot = new Vector2(0f, 1f);
        rankingRows.anchoredPosition = new Vector2(0f, -24f);

        // 常設の説明パネルは置かず、開始時だけ短い通知を表示する。
        announcement = RoundDamageRankingUI.CreateText("SuddenDeath", battleRect, font, 22f,
            Vector2.zero, new Vector2(360f, 64f));
        RectTransform announcementRect = announcement.rectTransform;
        announcementRect.anchorMin = announcementRect.anchorMax = new Vector2(0.5f, 1f);
        announcementRect.anchoredPosition = new Vector2(-180f, -64f);
        announcement.alignment = TextAlignmentOptions.Center;
        announcement.text = "サドンデス！\n<size=16>攻撃力2倍・全員透視・回復なし</size>";
        announcement.color = new Color32(255, 110, 70, 255);
        announcement.gameObject.SetActive(false);
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
        // 観戦案内は待機中も使う。戦闘用の子ルートだけを切り替え、タイマーや順位を持ち越さない。
        canvas.enabled = true;
        ConfigureCanvas();
        battleRoot.SetActive(playing);
        if (!playing) return;
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
