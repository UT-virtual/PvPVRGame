using System.Collections.Generic;
using TMPro;
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

    [SerializeField] private bool roomOpened = false;
    private bool isVisible = false;
    public void ShowRoomUI()
    {
        isVisible = true;
    }

    public void HideRoomUI()
    {
        isVisible = false;
    }

    private readonly List<GameObject> currentEntries = new();

    private void Update()
    {
        if (RoundManager.Instance == null)
        {
            return;
        }

        waitingRoomPanel.SetActive(isVisible);

        if (isVisible)
        {
            RefreshPlayerList();
        }
    }

    private void RefreshPlayerList()
    {
        // 古い表示を削除
        foreach (GameObject entry in currentEntries)
        {
            if (entry != null)
            {
                Destroy(entry);
            }
        }

        currentEntries.Clear();

        // プレイヤー人数表示
        playerCountText.text =
            $"Players : {RoundManager.Instance.RegisteredPlayerCount}";

        // プレイヤー一覧生成
        foreach (PlayerHealth player in RoundManager.Instance.Players)
        {
            if (player == null)
            {
                continue;
            }

            GameObject entry =
                Instantiate(playerEntryPrefab, contentRoot);

            currentEntries.Add(entry);

            // 子オブジェクト取得
            Image teamColorImage =
                entry.transform.Find("PlayerColor")
                .GetComponent<Image>();



            TMP_Text readyText =
                entry.transform.Find("Ready")
                .GetComponent<TMP_Text>();



            // Ready状態
            bool isReady =
                RoundManager.Instance.IsPlayerReady(player);

            readyText.text = isReady
                ? "READY"
                : "NOT READY";

            readyText.color = isReady
                ? new Color32(0, 255, 200, 255)    // ネオンシアン
                : new Color32(255, 80, 120, 255);
            // チーム色
            PlayerTeam team =
                player.GetComponent<PlayerTeam>();

            if (team != null)
            {
                switch (team.Team)
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
                }
            }
            else
            {
                teamColorImage.color = Color.gray;
            }
        }
    }
}