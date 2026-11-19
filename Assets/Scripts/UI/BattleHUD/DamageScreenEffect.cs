using UnityEngine;
using UnityEngine.UI;

/// <summary>被弾した本人とその観戦者の画面四辺を、一瞬だけ赤くする。</summary>
[DefaultExecutionOrder(10500)]
public sealed class DamageScreenEffect : MaskableGraphic
{
    private LocalPlayerDamageVfxController owner;
    private float remaining;
    private float duration;
    private Color flashColor;
    private Canvas effectCanvas;
    private Camera viewCamera;
    private bool wasVisible;

    public static DamageScreenEffect Create(LocalPlayerDamageVfxController owner)
    {
        GameObject root = new("DamageScreenEffect", typeof(RectTransform), typeof(Canvas));
        root.layer = 5;
        root.transform.SetParent(owner.transform, false);
        DamageScreenEffect effect = root.AddComponent<DamageScreenEffect>();
        effect.owner = owner;
        effect.effectCanvas = root.GetComponent<Canvas>();
        effect.effectCanvas.sortingOrder = 110;
        effect.effectCanvas.enabled = false;
        effect.raycastTarget = false;
        effect.color = Color.clear;
        root.AddComponent<ScreenUIForeground>();
        return effect;
    }

    public void Flash(float seconds, Color tint)
    {
        duration = Mathf.Max(0.05f, seconds);
        remaining = duration;
        flashColor = tint;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Application.onBeforeRender += BeforeRender;
    }

    protected override void OnDisable()
    {
        Application.onBeforeRender -= BeforeRender;
        base.OnDisable();
    }

    private void LateUpdate()
    {
        bool visible = remaining > 0f && owner != null && owner.IsViewed;
        if (!visible) remaining = 0f;
        else
        {
            remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
            Color tint = flashColor;
            tint.a *= remaining / duration;
            color = tint;
        }
        if (effectCanvas == null) return;
        effectCanvas.enabled = visible;
        if (!visible) { wasVisible = false; return; }

        bool vr = LocalPlayModeController.IsVirtualReality;
        effectCanvas.renderMode = vr ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
        if (viewCamera == null || !viewCamera.isActiveAndEnabled)
        {
            Transform head = vr ? NetworkLauncher.Instance?.LocalVrHead : null;
            viewCamera = head != null ? head.GetComponent<Camera>() : Camera.main;
        }
        if (vr && viewCamera == null) { effectCanvas.enabled = false; return; }
        if (!wasVisible) SetVerticesDirty();
        wasVisible = true;
        PlaceInView();
    }

    [BeforeRenderOrder(10500)]
    private void BeforeRender() => PlaceInView();

    private void PlaceInView()
    {
        if (effectCanvas == null || !effectCanvas.enabled || effectCanvas.renderMode != RenderMode.WorldSpace || viewCamera == null) return;
        float depth = VRWorldSpaceUIFollower.SharedDistance;
        float height = 2f * depth * Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * viewCamera.aspect;
        rectTransform.sizeDelta = new Vector2(width, height) / VRWorldSpaceUIFollower.SharedVrCanvasScale;
        transform.localScale = Vector3.one * VRWorldSpaceUIFollower.SharedVrCanvasScale;
        transform.SetPositionAndRotation(viewCamera.transform.position + viewCamera.transform.forward * depth, viewCamera.transform.rotation);
        effectCanvas.worldCamera = viewCamera;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = GetPixelAdjustedRect();
        float inset = Mathf.Min(r.width, r.height) * 0.055f;
        Vector2[] outer = { new(r.xMin, r.yMin), new(r.xMin, r.yMax), new(r.xMax, r.yMax), new(r.xMax, r.yMin) };
        Vector2[] inner = { new(r.xMin + inset, r.yMin + inset), new(r.xMin + inset, r.yMax - inset),
            new(r.xMax - inset, r.yMax - inset), new(r.xMax - inset, r.yMin + inset) };
        Color clear = color;
        clear.a = 0f;
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            int start = mesh.currentVertCount;
            mesh.AddVert(outer[i], color, Vector2.zero);
            mesh.AddVert(outer[next], color, Vector2.zero);
            mesh.AddVert(inner[next], clear, Vector2.zero);
            mesh.AddVert(inner[i], clear, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
