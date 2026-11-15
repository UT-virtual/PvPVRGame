using UnityEngine;
using Fusion;

public class BattleHudManager : MonoBehaviour
{
    [SerializeField] private GameObject battleHudRoot;
    [SerializeField] private bool showInWaitingRoom = true; // 追加

    private NetworkObject networkObject;

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
        
        bool isTargetPhase =
        RoundManager.Instance != null &&
        (
            RoundManager.Instance.IsRoundPlaying ||
            (showInWaitingRoom && RoundManager.Instance.IsWaitingForReady)
        );

        bool shouldShow = isLocalPlayer && isTargetPhase;

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