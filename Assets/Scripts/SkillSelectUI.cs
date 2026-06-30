using TMPro;
using UnityEngine;

public class SkillSelectUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text operationText;
    [SerializeField] private SkillCardUI[] skillCards = new SkillCardUI[4];

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
            RefreshCards(roundManager);
        }

        SetVisible(shouldShow);

        if (shouldShow)
        {
            remainingTime -= Time.deltaTime;
            remainingTime = Mathf.Max(remainingTime, 0.0f);

            UpdateTexts(roundManager);
            RefreshCards(roundManager);
        }

        wasShowing = shouldShow;
    }

    private void SetVisible(bool visible)
    {
        if (panelRoot != null && panelRoot.activeSelf != visible)
        {
            panelRoot.SetActive(visible);
        }
    }

    private void UpdateTexts(RoundManager roundManager)
    {
        if (titleText != null)
        {
            titleText.text = "スキル選択";
        }

        if (timerText != null)
        {
            timerText.text = $"残り {remainingTime:0.0} 秒";
        }

        if (operationText != null)
        {
            operationText.text = "1 / 2 / 3 / 4 で選択";
        }
    }

    private void RefreshCards(RoundManager roundManager)
    {
        if (roundManager == null || skillCards == null)
        {
            return;
        }

        for (int i = 0; i < skillCards.Length; i++)
        {
            if (skillCards[i] == null)
            {
                continue;
            }

            PlayerSkillType skill = roundManager.GetSkillOption(i);
            skillCards[i].SetSkill(i, skill);
        }
    }
}