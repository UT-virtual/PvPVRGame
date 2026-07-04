using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FinalResultUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Text")]
    [SerializeField] private TMP_Text titleText;

    [Header("Winner List")]
    [SerializeField] private Transform winnerContentRoot;
    [SerializeField] private GameObject winnerEntryPrefab;

    private readonly List<GameObject> winnerEntryObjects = new();

    private void Awake()
    {
        Hide();
    }

    public void ShowWinners(int winnerTeamMask)
    {
        SetVisible(true);
        ClearWinnerEntries();

        if (titleText != null)
        {
            titleText.text = "最終結果";
        }

        if (winnerContentRoot == null || winnerEntryPrefab == null)
        {
            Debug.LogWarning("[FinalResultUI] winnerContentRoot or winnerEntryPrefab is null.");
            return;
        }

        CreateWinnerEntryIfTeamExists(winnerTeamMask, RoundManager.TeamColor.Red);
        CreateWinnerEntryIfTeamExists(winnerTeamMask, RoundManager.TeamColor.Blue);
        CreateWinnerEntryIfTeamExists(winnerTeamMask, RoundManager.TeamColor.Green);
        CreateWinnerEntryIfTeamExists(winnerTeamMask, RoundManager.TeamColor.Yellow);
    }

    public void Hide()
    {
        ClearWinnerEntries();
        SetVisible(false);
    }

    private void CreateWinnerEntryIfTeamExists(
        int winnerTeamMask,
        RoundManager.TeamColor team
    )
    {
        int bit = 1 << (int)team;

        if ((winnerTeamMask & bit) == 0)
        {
            return;
        }

        GameObject entryObject = Instantiate(winnerEntryPrefab, winnerContentRoot);
        entryObject.SetActive(true);
        winnerEntryObjects.Add(entryObject);

        FinalResultWinnerEntryUI entryUI =
            entryObject.GetComponent<FinalResultWinnerEntryUI>();

        if (entryUI == null)
        {
            Debug.LogWarning("[FinalResultUI] FinalResultWinnerEntryUI was not found on winnerEntryPrefab.");
            return;
        }

        entryUI.Setup(GetTeamColor(team));
    }

    private void ClearWinnerEntries()
    {
        foreach (GameObject entryObject in winnerEntryObjects)
        {
            if (entryObject != null)
            {
                Destroy(entryObject);
            }
        }

        winnerEntryObjects.Clear();
    }

    private void SetVisible(bool visible)
    {
        GameObject targetRoot = root != null ? root : gameObject;

        if (targetRoot.activeSelf != visible)
        {
            targetRoot.SetActive(visible);
        }
    }

    private Color GetTeamColor(RoundManager.TeamColor team)
    {
        switch (team)
        {
            case RoundManager.TeamColor.Red:
                return new Color32(255, 77, 109, 255);

            case RoundManager.TeamColor.Blue:
                return new Color32(77, 171, 247, 255);

            case RoundManager.TeamColor.Green:
                return new Color32(64, 192, 87, 255);

            case RoundManager.TeamColor.Yellow:
                return new Color32(255, 212, 59, 255);

            default:
                return Color.gray;
        }
    }
}