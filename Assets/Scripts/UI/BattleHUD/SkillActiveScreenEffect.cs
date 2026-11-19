using UnityEngine;
using UnityEngine.UI;

/// <summary>ローカルプレイヤーの継続スキルだけを四辺の淡い水色グラデーションで知らせる。</summary>
[DefaultExecutionOrder(10500)]
public sealed class SkillActiveScreenEffect : MaskableGraphic
{
    private PlayerSkillController skills;
    private PlayerHealth health;
    private Canvas effectCanvas;
    private Camera viewCamera;
    private bool wasVisible;

    public static void Create(PlayerSkillController owner)
    {
        GameObject root = new("SkillActiveScreenEffect", typeof(RectTransform), typeof(Canvas));
        root.layer = 5;
        root.transform.SetParent(owner.transform, false);
        SkillActiveScreenEffect effect = root.AddComponent<SkillActiveScreenEffect>();
        effect.skills = owner;
        effect.health = owner.GetComponent<PlayerHealth>();
        effect.effectCanvas = root.GetComponent<Canvas>();
        effect.effectCanvas.sortingOrder = 100;
        effect.effectCanvas.enabled = false;
        effect.raycastTarget = false;
        effect.color = new Color(0.2f, 0.85f, 1f, 0.4f);
        root.AddComponent<ScreenUIForeground>();
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
        RoundManager round = RoundManager.Instance;
        bool visible = skills != null && skills.Object != null && skills.Object.IsValid &&
            skills.Object.HasInputAuthority && health != null && !health.IsDead && skills.HasActiveTimedSkill &&
            (round == null || round.IsRoundPlaying || round.IsWaitingForReady) &&
            !(SpectatorCameraController.Instance != null && SpectatorCameraController.Instance.IsSpectatingActive);
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
