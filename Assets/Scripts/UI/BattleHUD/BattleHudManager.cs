using UnityEngine;
using Fusion;

public class BattleHudManager : MonoBehaviour
{
    [SerializeField] private GameObject battleHudRoot;

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

        bool shouldShow =
            isLocalPlayer &&
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