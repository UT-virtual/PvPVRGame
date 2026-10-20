using System.Collections.Generic;
using TMPro;
using Fusion;
using UnityEngine;
using UnityEngine.UI;
using static RoundManager;

/// <summary>
/// ローカルプレイヤーの存在と同期フェーズから待機パネルの表示を決める。
/// 端末内のPlayerHealthを列挙し、同期されたチーム色と準備状態を表示する。
/// </summary>
public class WaitingRoomUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject playerEntryPrefab;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private GameObject waitingRoomPanel;

    private bool localPlayerSpawned = false;

    private const float RefreshInterval = 0.1f;
    private float nextRefreshTime;
    private readonly List<(PlayerHealth player, int team, bool ready, bool local)> nextPlayers = new();
    private readonly List<GameObject> currentEntries = new();
    private readonly List<(PlayerHealth player, int team, bool ready, bool local)> displayedPlayers = new();

    private void Awake()
    {
        ClearEntries();
        SetPanelVisible(false);
    }

    public void ShowRoomUI()
    {
        // 実際に表示するかどうかは Update() で判定する
    }

    /// <summary>
    /// 現在の一覧とパネルを隠す。次のUpdateではローカルプレイヤーの存在とフェーズから表示を再判定する。
    /// </summary>
    public void HideRoomUI()
    {
        ClearEntries();
        SetPanelVisible(false);
    }

    public void SetLocalPlayerSpawned(bool spawned)
    {
        localPlayerSpawned = spawned;
        nextRefreshTime = 0f;

        Debug.Log($"[WaitingRoomUI] LocalPlayerSpawned={localPlayerSpawned}");

        if (!localPlayerSpawned)
        {
            ClearEntries();
            SetPanelVisible(false);
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.unscaledTime + RefreshInterval;
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.InstanceID);
        bool hasLocalPlayer = HasLocalInputAuthorityPlayer(players);

        if (hasLocalPlayer != localPlayerSpawned)
        {
            localPlayerSpawned = hasLocalPlayer;
            Debug.Log("[WaitingRoomUI] Local player detected automatically.");
        }

        RoundManager roundManager = RoundManager.Instance;

        bool canShowByRoundManager =
            roundManager != null &&
            roundManager.IsNetworkReady &&
            roundManager.CanShowWaitingRoomUI;

        // ローカル生成とRoundManagerのSpawn順は固定せず、同期フェーズが読めるまで待機表示を許可する。
        bool roundManagerNotReadyYet =
            roundManager == null ||
            !roundManager.IsNetworkReady;

        bool shouldShow =
            hasLocalPlayer &&
            (
                canShowByRoundManager ||
                roundManagerNotReadyYet
            );

        SetPanelVisible(shouldShow);

        if (shouldShow)
        {
            RefreshPlayerList(players);
        }
        else
        {
            ClearEntries();
        }
    }

    private bool HasLocalInputAuthorityPlayer(PlayerHealth[] players)
    {

        foreach (PlayerHealth player in players)
        {
            if (player == null)
            {
                continue;
            }

            NetworkObject networkObject = player.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                continue;
            }

            if (networkObject.IsValid && networkObject.HasInputAuthority)
            {
                return true;
            }
        }

        return false;
    }

    private void SetPanelVisible(bool visible)
    {
        if (waitingRoomPanel == null)
        {
            Debug.LogWarning("[WaitingRoomUI] waitingRoomPanel is null.");
            return;
        }

        if (waitingRoomPanel.activeSelf == visible)
        {
            return;
        }

        waitingRoomPanel.SetActive(visible);

        Debug.Log($"[WaitingRoomUI] Panel Active={visible}");
    }

    private void ClearEntries()
    {
        foreach (GameObject entry in currentEntries)
        {
            if (entry != null)
            {
                Destroy(entry);
            }
        }

        currentEntries.Clear();
        displayedPlayers.Clear();

        if (playerCountText != null)
        {
            playerCountText.text = "参加プレイヤー : 0人";
        }
    }

    private void RefreshPlayerList(PlayerHealth[] players)
    {
        nextPlayers.Clear();
        foreach (PlayerHealth player in players)
        {
            if (player == null || player.Object == null || !player.Object.IsValid || player.Object.InputAuthority == PlayerRef.None)
            {
                continue;
            }

            nextPlayers.Add((player, player.NetworkedTeamIndex, player.IsReady, player.Object.HasInputAuthority));
        }

        bool changed = nextPlayers.Count != displayedPlayers.Count;
        for (int i = 0; !changed && i < nextPlayers.Count; i++)
        {
            changed = !nextPlayers[i].Equals(displayedPlayers[i]);
        }

        if (!changed)
        {
            return;
        }

        ClearEntries();
        displayedPlayers.AddRange(nextPlayers);

        if (playerCountText != null)
        {
            playerCountText.text = $"参加プレイヤー : {nextPlayers.Count}人";
        }

        foreach (var state in nextPlayers)
        {
            PlayerHealth player = state.player;
            if (player == null)
            {
                continue;
            }

            if (playerEntryPrefab == null || contentRoot == null)
            {
                Debug.LogWarning("[WaitingRoomUI] playerEntryPrefab or contentRoot is null.");
                return;
            }

            GameObject entry = Instantiate(playerEntryPrefab, contentRoot);
            currentEntries.Add(entry);

            Image teamColorImage = null;
            TMP_Text readyText = null;

            Transform colorTransform = entry.transform.Find("PlayerColor");
            if (colorTransform != null)
            {
                teamColorImage = colorTransform.GetComponent<Image>();
            }

            Transform readyTransform = entry.transform.Find("Ready");
            if (readyTransform != null)
            {
                readyText = readyTransform.GetComponent<TMP_Text>();
            }

            if (readyText != null)
            {
                bool isReady = player.IsReady;

                NetworkObject networkObject = player.GetComponent<NetworkObject>();
                bool isLocalPlayer =
                    networkObject != null &&
                    networkObject.HasInputAuthority;

                string readyLabel = isReady
                    ? "準備完了"
                    : "準備中";

                readyText.text = isLocalPlayer
                    ? $"{readyLabel}（あなた）"
                    : readyLabel;

                readyText.color = isReady
                    ? new Color32(0, 255, 200, 255)
                    : new Color32(255, 80, 120, 255);
            }

            if (teamColorImage != null)
            {
                if (!player.HasTeamAssigned)
                {
                    teamColorImage.color = Color.gray;
                    continue;
                }

                switch (player.Team)
                {
                    case TeamColor.Red:
                        teamColorImage.color = new Color32(255, 77, 109, 255);
                        break;

                    case TeamColor.Blue:
                        teamColorImage.color = new Color32(77, 171, 247, 255);
                        break;

                    case TeamColor.Green:
                        teamColorImage.color = new Color32(64, 192, 87, 255);
                        break;

                    case TeamColor.Yellow:
                        teamColorImage.color = new Color32(255, 212, 59, 255);
                        break;

                    default:
                        teamColorImage.color = Color.gray;
                        break;
                }
            }
        }
    }
}