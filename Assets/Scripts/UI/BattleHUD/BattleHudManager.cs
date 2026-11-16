using UnityEngine;
using Fusion;

/// <summary>
/// ローカルプレイヤーの戦闘フェーズでHUDを表示する。
/// 親にNetworkObjectがないシーン配置のHUDも表示対象として扱う。
/// </summary>
public class BattleHudManager : MonoBehaviour
{
    [SerializeField] private GameObject battleHudRoot;

    private NetworkObject networkObject;
    private NetworkLauncher networkLauncher;
    private SpectatorCameraController spectatorController;

    private void Awake()
    {
        networkObject = GetComponentInParent<NetworkObject>();
    }

    private void Start()
    {
        SetVisible(false);
    }

    private void Update()
    {
        bool isLocalPlayer =
            networkObject == null || networkObject.HasInputAuthority;

        if (networkLauncher == null) networkLauncher = FindFirstObjectByType<NetworkLauncher>();
        if (spectatorController == null) spectatorController = FindFirstObjectByType<SpectatorCameraController>();
        bool hasViewTarget = (networkLauncher != null && networkLauncher.IsLocalCombatant) ||
            (spectatorController != null && spectatorController.CurrentSpectatorTarget != null);
        bool shouldShow =
            hasViewTarget && isLocalPlayer &&
            RoundManager.Instance != null &&
            RoundManager.Instance.IsRoundPlaying;

        SetVisible(shouldShow);
    }

    public void ShowBattleUI()
    {
        SetVisible(true);
    }

    public void HideBattleUI()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        if (battleHudRoot == null)
        {
            return;
        }

        if (battleHudRoot.activeSelf == visible)
        {
            return;
        }

        battleHudRoot.SetActive(visible);
    }
}