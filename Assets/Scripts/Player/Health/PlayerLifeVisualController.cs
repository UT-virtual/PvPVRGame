using System;
using UnityEngine;

public sealed class PlayerLifeVisualController
{
    private readonly GameObject ownerObject;
    private readonly GameObject visualRoot;
    private readonly GameObject weaponVisualRoot;
    private readonly GameObject overheadIconRoot;
    private readonly CharacterController characterController;
    private readonly Func<bool> hasInputAuthority;

    private readonly Renderer[] bodyRenderers;
    private readonly Renderer[] weaponRenderers;
    private readonly Collider[] bodyColliders;

    public GameObject OverheadIconRoot => overheadIconRoot;
    public Renderer[] BodyRenderers => bodyRenderers;

    public PlayerLifeVisualController(
        GameObject ownerObject,
        GameObject visualRoot,
        GameObject weaponVisualRoot,
        GameObject overheadIconRoot,
        CharacterController characterController,
        Func<bool> hasInputAuthority
    )
    {
        this.ownerObject = ownerObject;
        this.visualRoot = visualRoot;
        this.weaponVisualRoot = weaponVisualRoot;
        this.overheadIconRoot = overheadIconRoot;
        this.characterController = characterController;
        this.hasInputAuthority = hasInputAuthority;

        bodyRenderers = FindBodyRenderers();
        bodyColliders = FindBodyColliders();
        weaponRenderers = FindWeaponRenderers();
    }

    public void ApplyAliveState(bool alive)
    {
        bool isInputAuthority = hasInputAuthority != null && hasInputAuthority();

        bool shouldShowBodyModel = alive && !isInputAuthority;
        bool shouldShowWeapon = alive;
        bool shouldShowOverheadIcon = alive && !isInputAuthority;

        SetRenderersEnabled(bodyRenderers, shouldShowBodyModel);
        SetRenderersEnabled(weaponRenderers, shouldShowWeapon);

        if (overheadIconRoot != null)
        {
            overheadIconRoot.SetActive(shouldShowOverheadIcon);
        }

        SetCollidersEnabled(bodyColliders, alive);

        if (characterController != null)
        {
            characterController.enabled = alive;
        }
    }

    private Renderer[] FindBodyRenderers()
    {
        if (visualRoot != null)
        {
            return visualRoot.GetComponentsInChildren<Renderer>(true);
        }

        if (ownerObject != null)
        {
            return ownerObject.GetComponentsInChildren<Renderer>(true);
        }

        return Array.Empty<Renderer>();
    }

    private Collider[] FindBodyColliders()
    {
        if (visualRoot != null)
        {
            return visualRoot.GetComponentsInChildren<Collider>(true);
        }

        if (ownerObject != null)
        {
            return ownerObject.GetComponentsInChildren<Collider>(true);
        }

        return Array.Empty<Collider>();
    }

    private Renderer[] FindWeaponRenderers()
    {
        if (weaponVisualRoot != null)
        {
            return weaponVisualRoot.GetComponentsInChildren<Renderer>(true);
        }

        return Array.Empty<Renderer>();
    }

    private void SetRenderersEnabled(Renderer[] targetRenderers, bool enabled)
    {
        if (targetRenderers == null)
        {
            return;
        }

        foreach (Renderer renderer in targetRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = enabled;
            }
        }
    }

    private void SetCollidersEnabled(Collider[] targetColliders, bool enabled)
    {
        if (targetColliders == null)
        {
            return;
        }

        foreach (Collider collider in targetColliders)
        {
            if (collider != null)
            {
                collider.enabled = enabled;
            }
        }
    }
}
