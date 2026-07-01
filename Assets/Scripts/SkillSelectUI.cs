using TMPro;
using UnityEngine;

public class SkillSelectUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text operationText;
    [SerializeField] private SkillCardUI[] skillCards = new SkillCardUI[4];

    private NetworkLauncher networkLauncher;
    private bool wasShowing;
    private float remainingTime;

    private void Awake()
    {
        SetVisible(false);
    }

    private void Update()
    {
        RoundManager roundManager = RoundManager.Instance;

        bool shouldShow =
            roundManager != null &&
            roundManager.IsSkillSelecting;

        if (shouldShow && !wasShowing)
        {
            remainingTime = roundManager.SkillSelectionDuration;
            SetSkillSelectionControlsVisible(true);
            RefreshCards(roundManager);
        }

        SetVisible(shouldShow);

        if (shouldShow)
        {
            EnsureNetworkLauncher();

            remainingTime -= Time.deltaTime;
            remainingTime = Mathf.Max(remainingTime, 0.0f);

            bool localPlayerSelected =
                networkLauncher != null &&
                networkLauncher.HasLocalSkillSelectionConfirmed;

            int selectionStep = networkLauncher != null
                ? networkLauncher.LocalSkillSelectionStep
                : 0;

            UpdateTexts(localPlayerSelected, selectionStep);
            SetSkillSelectionControlsVisible(!localPlayerSelected);

            if (!localPlayerSelected)
            {
                RefreshCards(roundManager);
            }
        }

        wasShowing = shouldShow;
    }

    private void EnsureNetworkLauncher()
    {
        if (networkLauncher != null)
        {
            return;
        }

        networkLauncher = FindFirstObjectByType<NetworkLauncher>();
    }

    private void SetVisible(bool visible)
    {
        if (panelRoot != null && panelRoot.activeSelf != visible)
        {
            panelRoot.SetActive(visible);
        }
    }

    private void UpdateTexts(bool localPlayerSelected, int selectionStep)
    {
        if (titleText != null)
        {
            if (localPlayerSelected)
            {
                titleText.text = "他の参加者が選択しています。";
            }
            else
            {
                titleText.text = selectionStep <= 0
                    ? "スキル選択 1/2"
                    : "スキル選択 2/2";
            }
        }

        if (timerText != null)
        {
            timerText.text = $"残り {remainingTime:0.0} 秒";
        }

        if (operationText != null)
        {
            operationText.text = "選択: 左スティック 決定: A";
        }
    }

    private void SetSkillSelectionControlsVisible(bool visible)
    {
        if (operationText != null && operationText.gameObject.activeSelf != visible)
        {
            operationText.gameObject.SetActive(visible);
        }

        if (skillCards == null)
        {
            return;
        }

        foreach (SkillCardUI skillCard in skillCards)
        {
            if (skillCard == null)
            {
                continue;
            }

            GameObject cardObject = skillCard.gameObject;

            if (cardObject.activeSelf != visible)
            {
                cardObject.SetActive(visible);
            }
        }
    }

    private void RefreshCards(RoundManager roundManager)
    {
        if (roundManager == null || skillCards == null)
        {
            return;
        }

        EnsureNetworkLauncher();

        int selectedSlot = networkLauncher != null
            ? networkLauncher.CurrentSkillSelectionSlot
            : -1;

        int selectionStep = networkLauncher != null
            ? networkLauncher.LocalSkillSelectionStep
            : 0;

        for (int i = 0; i < skillCards.Length; i++)
        {
            if (skillCards[i] == null)
            {
                continue;
            }

            PlayerSkillType skill = roundManager.GetSkillOption(i, selectionStep);

            skillCards[i].SetSkill(i, skill);
            skillCards[i].SetSelected(i == selectedSlot);
        }
    }
}