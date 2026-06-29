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

    private bool isVisible = false;

    private readonly List<GameObject> currentEntries = new();

    public void ShowRoomUI()
    {
        isVisible = true;
    }

    public void HideRoomUI()
    {
        isVisible = false;
    }

    private void Update()
    {
        if (waitingRoomPanel == null)
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
        foreach (GameObject entry in currentEntries)
        {
            if (entry != null)
            {
                Destroy(entry);
            }
        }

        currentEntries.Clear();

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(
            FindObjectsSortMode.None
        );

        if (playerCountText != null)
        {
            playerCountText.text = $"Players : {players.Length}";
        }

        foreach (PlayerHealth player in players)
        {
            if (player == null)
            {
                continue;
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

                readyText.text = isReady
                    ? "READY"
                    : "NOT READY";

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