using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>画面用Canvas内だけを深度に隠れない描画にし、文字と背景のUI順序を保つ。</summary>
[DefaultExecutionOrder(11000)]
public sealed class ScreenUIForeground : MonoBehaviour
{
    private readonly List<Graphic> graphics = new();
    private readonly Dictionary<Material, Material> copies = new();
    private readonly HashSet<Material> generated = new();
    private readonly Dictionary<Material, Material> originalByCopy = new();
    private readonly Dictionary<Graphic, Material> originals = new();
    private Shader imageShader;
    private Shader textShader;

    private void LateUpdate()
    {
        if (imageShader == null) imageShader = Resources.Load<Shader>("ScreenUIForeground");
        if (textShader == null)
        {
            Material template = Resources.Load<Material>("ScreenUITextForeground");
            if (template != null) textShader = template.shader;
        }
        // 非表示のパネルと後から生成されるスキル枠・ランキングにも適用する。
        GetComponentsInChildren(true, graphics);
        foreach (Graphic graphic in graphics)
        {
            TMP_Text text = graphic as TMP_Text;
            TMP_SubMeshUI submesh = graphic as TMP_SubMeshUI;
            Material source = text != null ? text.fontSharedMaterial :
                submesh != null ? submesh.sharedMaterial : graphic.material;
            if (source == null) continue;
            if (generated.Contains(source))
            {
                // 複製されたスキル2枠目は前面材質を引き継ぐため、復元先も登録する。
                if (!originals.ContainsKey(graphic)) originals.Add(graphic, originalByCopy[source]);
                continue;
            }
            if (!copies.TryGetValue(source, out Material copy))
            {
                Shader shader = source.HasProperty("_GradientScale") ? textShader : imageShader;
                if (shader == null) continue;
                copy = new Material(source) { shader = shader, renderQueue = 4000 };
                copies.Add(source, copy);
                generated.Add(copy);
                originalByCopy.Add(copy, source);
            }
            if (!originals.ContainsKey(graphic)) originals.Add(graphic, source);
            SetMaterial(graphic, copy);
        }
    }

    private static void SetMaterial(Graphic graphic, Material material)
    {
        // TMPはGraphic.materialではなくfontSharedMaterialから実際の文字を描画する。
        if (graphic is TMP_Text text) text.fontSharedMaterial = material;
        else if (graphic is TMP_SubMeshUI submesh) submesh.sharedMaterial = material;
        else graphic.material = material;
    }

    private void OnDisable()
    {
        foreach (var entry in originals)
            if (entry.Key != null) SetMaterial(entry.Key, entry.Value);
        originals.Clear();
        foreach (Material material in generated) Destroy(material);
        generated.Clear();
        originalByCopy.Clear();
        copies.Clear();
    }
}
