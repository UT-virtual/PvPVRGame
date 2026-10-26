using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 同期された撃破数・与ダメージ一覧を、待機画面と同じ色アイコンで表示する。
/// 戦闘HUDと結果UIで共用し、観戦対象ではなく接続者本人の行に「（あなた）」を付ける。
/// </summary>
public sealed class RoundDamageRankingUI : MonoBehaviour
{
    private const float RowSpacing = 64f;
    public const float PreferredHeight = NetworkSessionController.MaxCombatantCount * RowSpacing;

    private sealed class Row
    {
        public GameObject Root;
        public Image Background;
        public Image Icon;
        public TMP_Text Rank;
        public TMP_Text Kills;
        public TMP_Text Value;
    }

    private readonly List<RoundDamageEntry> entries = new(NetworkSessionController.MaxCombatantCount);
    private readonly List<Row> rows = new(NetworkSessionController.MaxCombatantCount);

    public static float GetPreferredHeight(int rowCount, float contentScale = 1f)
    {
        return Mathf.Clamp(rowCount, 1, NetworkSessionController.MaxCombatantCount) * RowSpacing * contentScale;
    }

    public static RoundDamageRankingUI Create(Transform parent, TMP_FontAsset font, Sprite icon, float width,
        float contentScale = 1f, int rowCount = NetworkSessionController.MaxCombatantCount)
    {
        GameObject root = new("DamageRanking", typeof(RectTransform), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)root.transform;
        // 親のサイズやLayoutGroupに依存せず、上端中央を配置基準にする。
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, GetPreferredHeight(rowCount, contentScale));
        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = rect.sizeDelta.y;
        RoundDamageRankingUI view = root.AddComponent<RoundDamageRankingUI>();
        view.BuildRows(font, icon, width, contentScale);
        return view;
    }

    private void BuildRows(TMP_FontAsset font, Sprite sprite, float width, float scale)
    {
        for (int i = 0; i < NetworkSessionController.MaxCombatantCount; i++)
        {
            RectTransform rect = CreateRect("RankRow", transform, new Vector2(0f, 1f),
                new Vector2(0f, -i * RowSpacing * scale), new Vector2(width, 60f * scale));
            Image background = rect.gameObject.AddComponent<Image>();
            background.raycastTarget = false;
            TMP_Text rank = CreateText("Rank", rect, font, 20f * scale,
                new Vector2(10f, 0f) * scale, new Vector2(43f, 60f) * scale);
            RectTransform iconRect = CreateRect("PlayerColor", rect, new Vector2(0f, 1f),
                new Vector2(56f, -18f) * scale, new Vector2(24f, 24f) * scale);
            Image image = iconRect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            TMP_Text kills = CreateText("Kills", rect, font, 24f * scale,
                new Vector2(92f, 0f) * scale, new Vector2(76f, 60f) * scale);
            kills.fontStyle = FontStyles.Bold;
            TMP_Text value = CreateText("Damage", rect, font, 20f * scale,
                new Vector2(180f * scale, 0f), new Vector2(width - 190f * scale, 60f * scale));
            value.enableAutoSizing = true;
            value.fontSizeMin = 18f * scale;
            value.fontSizeMax = 20f * scale;
            value.overflowMode = TextOverflowModes.Ellipsis;
            rows.Add(new Row { Root = rect.gameObject, Background = background, Icon = image, Rank = rank, Kills = kills, Value = value });
        }
    }

    /// <summary>
    /// 同期リビジョンまたは表示情報の変更時に呼ぶ。HPは観戦専用端末に限って表示する。
    /// 死亡者の記録を残し、撃破数・与ダメージ・残りHPが同じなら同順位として描画する。
    /// </summary>
    public void Refresh(RoundManager manager, bool showHealth)
    {
        entries.Clear();
        for (int i = 0; i < manager.DamageEntryCount; i++) entries.Add(manager.DamageEntries[i]);
        RoundDamageRanking.Sort(entries);
        PlayerRef localPlayer = manager.Runner.LocalPlayer;
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            row.Root.SetActive(i < entries.Count);
            if (i >= entries.Count) continue;

            RoundDamageEntry entry = entries[i];
            bool isLocal = entry.Player == localPlayer;
            row.Rank.text = $"{RoundDamageRanking.GetRank(entries, i)}位";
            row.Kills.text = $"{entry.Kills}撃破";
            string status = !entry.IsAlive ? "脱落" : showHealth
                ? $"HP {entry.Health:0.##}/{entry.MaxHealth:0.##}" : "生存";
            row.Value.text = $"{entry.Damage:0.##} ダメージ\n{status}" +
                (isLocal ? "（あなた）" : "");
            Color textColor = entry.IsAlive ? Color.white : new Color32(155, 160, 170, 255);
            row.Rank.color = textColor;
            row.Kills.color = textColor;
            row.Value.color = textColor;
            row.Value.fontStyle = isLocal ? FontStyles.Bold : FontStyles.Normal;
            // 死亡行はグレー表示にしつつ、誰の記録か分かるようアイコンの色味を薄く残す。
            Color teamColor = GetTeamColor(entry.TeamIndex);
            row.Icon.color = entry.IsAlive ? teamColor : Color.Lerp(teamColor, Color.gray, 0.65f);
            row.Background.color = isLocal ? new Color(0.12f, 0.42f, 0.55f, 0.9f) : new Color(0.02f, 0.04f, 0.08f, 0.82f);
        }
    }

    public static Color GetTeamColor(int teamIndex)
    {
        switch ((TeamColor)teamIndex)
        {
            case TeamColor.Red: return new Color32(255, 77, 109, 255);
            case TeamColor.Blue: return new Color32(77, 171, 247, 255);
            case TeamColor.Green: return new Color32(64, 192, 87, 255);
            case TeamColor.Yellow: return new Color32(255, 212, 59, 255);
            default: return Color.gray;
        }
    }

    internal static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        GameObject child = new(name, typeof(RectTransform));
        child.layer = 5;
        RectTransform rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    internal static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, float size,
        Vector2 position, Vector2 dimensions)
    {
        RectTransform rect = CreateRect(name, parent, new Vector2(0f, 1f), position, dimensions);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }
}
