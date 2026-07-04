using System.Collections.Generic;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(PlayerSkillController))]
[RequireComponent(typeof(PlayerHealth))]
public class XRayVisionController : NetworkBehaviour
{
    [Header("XRay")]
    [SerializeField] private Material xRayMaterial;

    private PlayerSkillController skillController;
    private PlayerHealth localHealth;

    private readonly Dictionary<Renderer, Material[]> originalMaterials = new();
    private bool isApplied;

    private void Awake()
    {
        skillController = GetComponent<PlayerSkillController>();
        localHealth = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        if (Object == null || !Object.HasInputAuthority)
        {
            if (isApplied)
            {
                ClearXRay();
            }

            return;
        }

        if (localHealth != null && localHealth.IsDead)
        {
            ClearXRay();
            return;
        }

        bool shouldApply =
            skillController != null &&
            skillController.IsXRayVisionActive();

        if (shouldApply)
        {
            RefreshXRayTargets();
        }
        else
        {
            ClearXRay();
        }
    }

    private void RefreshXRayTargets()
    {
        if (xRayMaterial == null)
        {
            Debug.LogWarning("[XRayVisionController] XRay Material is not assigned.");
            return;
        }

        isApplied = true;

        HashSet<Renderer> currentTargets = new();

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);

        foreach (PlayerHealth player in players)
        {
            if (!IsValidTarget(player))
            {
                continue;
            }

            Renderer[] bodyRenderers = player.BodyRenderers;

            if (bodyRenderers == null)
            {
                continue;
            }

            foreach (Renderer renderer in bodyRenderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                currentTargets.Add(renderer);

                if (!originalMaterials.ContainsKey(renderer))
                {
                    originalMaterials[renderer] = renderer.materials;
                }

                Material[] xRayMaterials = new Material[renderer.materials.Length];

                for (int i = 0; i < xRayMaterials.Length; i++)
                {
                    xRayMaterials[i] = xRayMaterial;
                }

                renderer.enabled = true;
                renderer.materials = xRayMaterials;
            }
        }

        RestoreRenderersNoLongerTargeted(currentTargets);
    }

    private bool IsValidTarget(PlayerHealth player)
    {
        if (player == null)
        {
            return false;
        }

        if (player == localHealth)
        {
            return false;
        }

        if (player.Object == null)
        {
            return false;
        }

        if (player.IsDead)
        {
            return false;
        }

        return true;
    }

    private void RestoreRenderersNoLongerTargeted(HashSet<Renderer> currentTargets)
    {
        List<Renderer> restoreTargets = new();

        foreach (Renderer renderer in originalMaterials.Keys)
        {
            if (renderer == null)
            {
                restoreTargets.Add(renderer);
                continue;
            }

            if (!currentTargets.Contains(renderer))
            {
                restoreTargets.Add(renderer);
            }
        }

        foreach (Renderer renderer in restoreTargets)
        {
            RestoreRenderer(renderer);
        }
    }

    private void ClearXRay()
    {
        List<Renderer> renderers = new(originalMaterials.Keys);

        foreach (Renderer renderer in renderers)
        {
            RestoreRenderer(renderer);
        }

        originalMaterials.Clear();
        isApplied = false;
    }

    private void RestoreRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            originalMaterials.Remove(renderer);
            return;
        }

        if (!originalMaterials.TryGetValue(renderer, out Material[] materials))
        {
            return;
        }

        renderer.materials = materials;
        originalMaterials.Remove(renderer);
    }

    private void OnDisable()
    {
        ClearXRay();
    }

    private void OnDestroy()
    {
        ClearXRay();
    }
}