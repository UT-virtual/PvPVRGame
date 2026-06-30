using System.Collections.Generic;
using TMPro;
using Fusion;
using UnityEngine;
using UnityEngine.UI;
using static RoundManager;

public class WaitingRoomUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private GameObject playerEntryPrefab;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private GameObject waitingRoomPanel;

    private bool localPlayerSpawned = false;

    private readonly List<GameObject> currentEntries = new();

    private void Awake()
    {
        ClearEntries();
        SetPanelVisible(false);
    }

    public void ShowRoomUI()
    {
        // 実際に表示するかどうかは Update() で判定する
    }

    public void HideRoomUI()
    {
        ClearEntries();
        SetPanelVisible(false);
    }

    public void SetLocalPlayerSpawned(bool spawned)
    {
        localPlayerSpawned = spawned;

        Debug.Log($"[WaitingRoomUI] LocalPlayerSpawned={localPlayerSpawned}");

        if (!localPlayerSpawned)
        {
            ClearEntries();
            SetPanelVisible(false);
        }
    }

    private void Update()
    {
        bool shouldShow =
            localPlayerSpawned &&
            RoundManager.Instance != null &&
            RoundManager.Instance.CanShowWaitingRoomUI;

        SetPanelVisible(shouldShow);

        if (shouldShow)
        {
            RefreshPlayerList();
        }
        else
        {
            ClearEntries();
        }
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

        if (playerCountText != null)
        {
            playerCountText.text = "参加プレイヤー : 0人";
        }
    }

    private void RefreshPlayerList()
    {
        ClearEntries();

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(
            FindObjectsSortMode.None
        );

        if (playerCountText != null)
        {
            playerCountText.text = $"参加プレイヤー : {players.Length}人";
        }

        foreach (PlayerHealth player in players)
        {
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
                    : "未完了";

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