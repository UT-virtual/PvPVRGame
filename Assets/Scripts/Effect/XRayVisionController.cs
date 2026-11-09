using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>見ているプレイヤーの透視を、通常描画に重ねた遮蔽輪郭として再現する。</summary>
[RequireComponent(typeof(PlayerSkillController))]
[RequireComponent(typeof(PlayerHealth))]
public class XRayVisionController : NetworkBehaviour
{
    [SerializeField] private Material xRayMaterial;
    private PlayerSkillController skillController;
    private PlayerHealth localHealth;
    private static XRayVisionController activeViewController;
    private sealed class OverlayPair
    {
        public Renderer Mask;
        public Renderer Outline;
    }

    private Material maskMaterial;
    private Material outlineMaterial;
    private readonly Dictionary<Renderer, OverlayPair> overlays = new();
    private readonly HashSet<Renderer> targets = new();
    private readonly List<Renderer> staleTargets = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => activeViewController = null;

    private void Awake()
    {
        skillController = GetComponent<PlayerSkillController>();
        localHealth = GetComponent<PlayerHealth>();
    }

    private void LateUpdate()
    {
        SpectatorCameraController spectator = SpectatorCameraController.Instance;
        bool controlsView = Object != null && Object.IsValid && localHealth != null && !localHealth.IsDead &&
            (spectator != null && spectator.IsSpectatingActive
                ? spectator.CurrentSpectatorTarget == localHealth
                : Object.HasInputAuthority);
        if (!controlsView)
        {
            if (activeViewController == this)
            {
                ClearXRay();
                activeViewController = null;
            }
            return;
        }
        if (activeViewController != this)
        {
            if (activeViewController != null) activeViewController.ClearXRay();
            activeViewController = this;
        }
        bool enabled = skillController.IsXRayVisionActive() ||
            (RoundManager.Instance != null && RoundManager.Instance.IsSuddenDeath);
        if (enabled && xRayMaterial != null) RefreshTargets();
        else ClearXRay();
    }

    private void RefreshTargets()
    {
        EnsureMaterials();
        targets.Clear();
        foreach (PlayerHealth player in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
        {
            if (player == localHealth || player.Object == null || !player.Object.IsValid || player.IsDead ||
                player.BodyRenderers == null) continue;
            foreach (Renderer source in player.BodyRenderers)
            {
                if (source == null || IsWeaponRenderer(player, source)) continue;
                targets.Add(source);
                if (!overlays.TryGetValue(source, out OverlayPair pair))
                {
                    Renderer mask = CreateOverlay(source, maskMaterial, "XRay Silhouette Mask");
                    if (mask == null) continue;
                    pair = new OverlayPair
                    {
                        Mask = mask,
                        Outline = CreateOverlay(source, outlineMaterial, "XRay Occluded Contour")
                    };
                    overlays[source] = pair;
                }
                UpdateOverlay(source, pair.Mask);
                UpdateOverlay(source, pair.Outline);
            }
        }
        staleTargets.Clear();
        foreach (Renderer source in overlays.Keys)
            if (source == null || !targets.Contains(source)) staleTargets.Add(source);
        foreach (Renderer source in staleTargets) RemoveOverlay(source);
    }

    private void EnsureMaterials()
    {
        if (maskMaterial != null) return;
        // 全身の元サイズを先にステンシルへ記録し、体の内側の線を描かせない。
        // 別Renderer/Queueに分け、URPで全てのマスクを輪郭より先に描画する。
        maskMaterial = new Material(xRayMaterial) { name = "XRay Silhouette Mask", renderQueue = 2990 };
        maskMaterial.SetFloat("_OutlineWidth", 0f);
        maskMaterial.SetInt("_Cull", (int)CullMode.Off);
        maskMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
        maskMaterial.SetInt("_ColorMask", 0);
        maskMaterial.SetInt("_StencilComp", (int)CompareFunction.Always);
        // URPユーザー領域のbit 2を使用。既存UIの1段目Mask(bit 1)と分離する。
        maskMaterial.SetInt("_StencilWriteMask", 2);

        outlineMaterial = new Material(xRayMaterial) { name = "XRay Occluded Contour", renderQueue = 2991 };
        outlineMaterial.SetInt("_Cull", (int)CullMode.Front);
        outlineMaterial.SetInt("_ZTest", (int)CompareFunction.Greater);
        outlineMaterial.SetInt("_ColorMask", (int)ColorWriteMask.All);
        outlineMaterial.SetInt("_StencilComp", (int)CompareFunction.NotEqual);
        outlineMaterial.SetInt("_StencilWriteMask", 0);
    }

    private static void UpdateOverlay(Renderer source, Renderer overlay)
    {
        if (overlay == null) return;
        overlay.enabled = source.enabled;
        overlay.forceRenderingOff = source.forceRenderingOff;
        if (source is SkinnedMeshRenderer skin && overlay is SkinnedMeshRenderer overlaySkin && skin.sharedMesh != null)
            for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                overlaySkin.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
    }

    private static bool IsWeaponRenderer(PlayerHealth player, Renderer source)
    {
        if (player.WeaponRenderers == null) return false;
        foreach (Renderer weapon in player.WeaponRenderers)
            if (source == weapon) return true;
        return false;
    }

    private Renderer CreateOverlay(Renderer source, Material material, string objectName)
    {
        Mesh mesh = source is SkinnedMeshRenderer skinned ? skinned.sharedMesh :
            source.GetComponent<MeshFilter>()?.sharedMesh;
        if (mesh == null || mesh.subMeshCount == 0) return null;
        GameObject view = new(objectName);
        view.layer = source.gameObject.layer;
        view.transform.SetParent(source.transform, false);
        Renderer overlay;
        if (source is SkinnedMeshRenderer skin)
        {
            SkinnedMeshRenderer copy = view.AddComponent<SkinnedMeshRenderer>();
            copy.sharedMesh = mesh;
            copy.bones = skin.bones;
            copy.rootBone = skin.rootBone;
            copy.localBounds = skin.localBounds;
            copy.updateWhenOffscreen = skin.updateWhenOffscreen;
            overlay = copy;
        }
        else
        {
            view.AddComponent<MeshFilter>().sharedMesh = mesh;
            overlay = view.AddComponent<MeshRenderer>();
        }
        Material[] materials = new Material[mesh.subMeshCount];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        overlay.sharedMaterials = materials;
        overlay.shadowCastingMode = ShadowCastingMode.Off;
        overlay.receiveShadows = false;
        overlay.allowOcclusionWhenDynamic = false;
        overlay.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        return overlay;
    }

    private void RemoveOverlay(Renderer source)
    {
        if (overlays.TryGetValue(source, out OverlayPair pair)) DestroyPair(pair);
        overlays.Remove(source);
    }

    private void DestroyPair(OverlayPair pair)
    {
        if (pair.Mask != null)
        {
            pair.Mask.enabled = false;
            Destroy(pair.Mask.gameObject);
        }
        if (pair.Outline != null)
        {
            pair.Outline.enabled = false;
            Destroy(pair.Outline.gameObject);
        }
    }

    private void ClearXRay()
    {
        foreach (OverlayPair pair in overlays.Values) DestroyPair(pair);
        overlays.Clear();
        targets.Clear();
        staleTargets.Clear();
    }

    private void OnDisable()
    {
        ClearXRay();
        if (activeViewController == this) activeViewController = null;
    }

    private void OnDestroy()
    {
        OnDisable();
        if (maskMaterial != null) Destroy(maskMaterial);
        if (outlineMaterial != null) Destroy(outlineMaterial);
    }
}
