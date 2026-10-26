using System.Collections.Generic;
using TMPro;
using Fusion;
using UnityEngine;
using UnityEngine.UI;
using static RoundManager;

/// <summary>
/// 接続状態と同期フェーズから、キャラクターのない観戦者にも待機一覧を表示する。
/// 端末内のPlayerHealthを列挙し、同期されたチーム色と準備状態を表示する。
/// </summary>
public class WaitingRoomUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject playerEntryPrefab;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private GameObject waitingRoomPanel;

    public TMP_FontAsset DisplayFont => playerCountText != null ? playerCountText.font : null;
    public Sprite ColorIconSprite => playerEntryPrefab != null
        ? playerEntryPrefab.transform.Find("PlayerColor")?.GetComponent<Image>()?.sprite : null;

    private bool localPlayerSpawned = false;
    private NetworkLauncher networkLauncher;

    private const float RefreshInterval = 0.1f;
    private float nextRefreshTime;
    private readonly List<(PlayerRef player, int team, bool ready, bool local, ParticipantRole role)> nextPlayers = new();
    private readonly List<GameObject> currentEntries = new();
    private readonly List<(PlayerRef player, int team, bool ready, bool local, ParticipantRole role)> displayedPlayers = new();

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

        if (networkLauncher == null)
        {
            networkLauncher = FindFirstObjectByType<NetworkLauncher>();
        }
        bool connected = networkLauncher != null && networkLauncher.IsConnected;
        bool shouldShow =
            (connected || hasLocalPlayer) &&
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
            playerCountText.text = "接続 : 0人（対戦 0人・観戦 0人）";
        }
    }

    private void RefreshPlayerList(PlayerHealth[] players)
    {
        nextPlayers.Clear();
        RoundManager manager = RoundManager.Instance;
        if (manager == null || !manager.IsNetworkReady)
        {
            return;
        }

        // 観戦者はPlayerHealthを持たないため、一覧の基準は同期された接続者のPlayerRefにする。
        foreach (var participant in manager.ConnectedParticipants)
        {
            PlayerHealth health = null;
            foreach (PlayerHealth candidate in players)
            {
                if (candidate != null && candidate.Object != null && candidate.Object.IsValid &&
                    candidate.Object.InputAuthority == participant.Key)
                {
                    health = candidate;
                    break;
                }
            }
            nextPlayers.Add((participant.Key, health != null ? health.NetworkedTeamIndex : -1,
                health != null && health.IsReady, participant.Key == manager.Runner.LocalPlayer, participant.Value));
        }
        nextPlayers.Sort((a, b) => a.player.PlayerId.CompareTo(b.player.PlayerId));

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
            int combatants = 0;
            int spectators = 0;
            foreach (var participant in nextPlayers)
            {
                if (participant.role == ParticipantRole.Combatant)
                {
                    combatants++;
                }
                else if (participant.role == ParticipantRole.Spectator)
                {
                    spectators++;
                }
            }
            playerCountText.text = $"接続 : {nextPlayers.Count}/{NetworkSessionController.MaxConnectionCount}人\n" +
                $"対戦 : {combatants}/{NetworkSessionController.MaxCombatantCount}人　観戦 : {spectators}人";
        }

        foreach (var state in nextPlayers)
        {
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
                string label = state.role switch
                {
                    ParticipantRole.Spectator => "観戦中",
                    ParticipantRole.Unselected => "参加処理中",
                    _ => state.ready ? "準備完了" : "準備中"
                };
                readyText.text = state.local ? $"{label}（あなた）" : label;
                readyText.color = state.role == ParticipantRole.Spectator
                    ? new Color32(77, 171, 247, 255)
                    : state.ready ? new Color32(0, 255, 200, 255) : new Color32(255, 80, 120, 255);
            }

            if (teamColorImage != null)
            {
                if (state.role != ParticipantRole.Combatant || state.team < 0)
                {
                    teamColorImage.color = Color.gray;
                    continue;
                }

                switch ((TeamColor)state.team)
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
