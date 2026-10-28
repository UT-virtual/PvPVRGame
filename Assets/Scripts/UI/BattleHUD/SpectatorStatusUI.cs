using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 待機・選択・戦闘の全フェーズで、共通Canvasの左下に観戦対象と操作案内を表示する。
/// Sceneに残る旧表示は停止し、対象がいる場合は「観戦中：」の後にチーム色アイコンだけを添える。
/// </summary>
public class SpectatorStatusUI : MonoBehaviour
{
    [Header("Legacy Scene UI")]
    [SerializeField] private GameObject root;

    private RoundManager manager;
    private SpectatorCameraController spectatorController;
    private GameObject viewRoot;
    private TMP_Text spectatorText;
    private Image targetColorIcon;
    private TMP_Text operationText;
    private bool viewInitialized;
    private bool hadTarget;

    private void Awake()
    {
        if (root == null) return;

        // 旧Sceneの中央表示を無効化する。描画は共通Canvasに生成したインスタンスへ一本化する。
        if (transform.IsChildOf(root.transform))
        {
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        else
        {
            root.SetActive(false);
        }
        enabled = false;
    }

    /// <summary>
    /// RoundBattleStatusUIから共通Canvasへ一度だけ作る。フェーズごとの再生成や別表示への切替は行わない。
    /// </summary>
    public void Initialize(RoundManager roundManager, Transform canvasRoot, TMP_FontAsset font, Sprite icon)
    {
        if (viewRoot != null) return;
        manager = roundManager;
        RectTransform panel = RoundDamageRankingUI.CreateRect("SpectatorStatus", canvasRoot,
            new Vector2(0f, 0f), new Vector2(16f, 16f), new Vector2(260f, 54f));
        panel.pivot = new Vector2(0f, 0f);
        viewRoot = panel.gameObject;
        Image background = viewRoot.AddComponent<Image>();
        background.color = new Color(0.02f, 0.04f, 0.08f, 0.6f);
        background.raycastTarget = false;
        spectatorText = RoundDamageRankingUI.CreateText("SpectatorTarget", panel, font, 18f,
            new Vector2(10f, -2f), new Vector2(80f, 28f));
        RectTransform colorRect = RoundDamageRankingUI.CreateRect("PlayerColor", panel,
            new Vector2(0f, 1f), new Vector2(92f, -9f), new Vector2(16f, 16f));
        targetColorIcon = colorRect.gameObject.AddComponent<Image>();
        targetColorIcon.sprite = icon;
        targetColorIcon.raycastTarget = false;
        operationText = RoundDamageRankingUI.CreateText("SpectatorControls", panel, font, 14f,
            new Vector2(10f, -30f), new Vector2(240f, 22f));
        operationText.text = "切替：左右スティック / A・D";
        viewRoot.SetActive(false);
        enabled = true;
    }

    private void Update()
    {
        if (viewRoot == null) return;
        if (manager == null || !manager.IsNetworkReady)
        {
            viewRoot.SetActive(false);
            return;
        }
        if (spectatorController == null) spectatorController = FindFirstObjectByType<SpectatorCameraController>();
        bool spectating = manager.IsLocalDedicatedSpectator ||
            (spectatorController != null && spectatorController.IsSpectatingActive);
        viewRoot.SetActive(spectating);
        if (!spectating) return;

        PlayerHealth target = spectatorController != null ? spectatorController.CurrentSpectatorTarget : null;
        bool hasTarget = target != null && target.Object != null && target.Object.IsValid && !target.IsDead;
        if (!viewInitialized || hadTarget != hasTarget)
        {
            // 対象不在時はアイコンと操作案内を消し、待機メッセージに横幅を渡す。
            spectatorText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, hasTarget ? 80f : 240f);
            spectatorText.fontSize = hasTarget ? 18f : 16f;
            operationText.gameObject.SetActive(hasTarget);
            ((RectTransform)viewRoot.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, hasTarget ? 54f : 34f);
            hadTarget = hasTarget;
            viewInitialized = true;
        }
        targetColorIcon.enabled = hasTarget;
        if (hasTarget)
        {
            spectatorText.text = "観戦中：";
            targetColorIcon.color = RoundDamageRankingUI.GetTeamColor(target.NetworkedTeamIndex);
        }
        else
        {
            spectatorText.text = spectatorController != null && spectatorController.IsWaitingOverview
                ? "対戦者の参加を待っています" : "観戦対象を待っています";
        }
    }

    private void OnDisable()
    {
        if (viewRoot != null) viewRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (viewRoot != null) Destroy(viewRoot);
    }
}
