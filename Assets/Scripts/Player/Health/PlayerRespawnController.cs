using System;
using System.Collections;
using Fusion;
using UnityEngine;

public sealed class PlayerRespawnController
{
    private readonly GameObject ownerObject;
    private readonly Transform ownerTransform;
    private readonly NetworkTransform networkTransform;
    private readonly CharacterController characterController;
    private readonly PlayerMove playerMove;
    private readonly PlayerWeapon playerWeapon;
    private readonly PlayerCamera playerCamera;
    private readonly PlayerLifeVisualController lifeVisualController;
    private readonly Action notifyHealthChanged;
    private readonly Func<NetworkObject> getNetworkObject;
    private readonly Func<NetworkRunner> getRunner;

    public PlayerRespawnController(
        GameObject ownerObject,
        Transform ownerTransform,
        NetworkTransform networkTransform,
        CharacterController characterController,
        PlayerMove playerMove,
        PlayerWeapon playerWeapon,
        PlayerCamera playerCamera,
        PlayerLifeVisualController lifeVisualController,
        Action notifyHealthChanged,
        Func<NetworkObject> getNetworkObject,
        Func<NetworkRunner> getRunner
    )
    {
        this.ownerObject = ownerObject;
        this.ownerTransform = ownerTransform;
        this.networkTransform = networkTransform;
        this.characterController = characterController;
        this.playerMove = playerMove;
        this.playerWeapon = playerWeapon;
        this.playerCamera = playerCamera;
        this.lifeVisualController = lifeVisualController;
        this.notifyHealthChanged = notifyHealthChanged;
        this.getNetworkObject = getNetworkObject;
        this.getRunner = getRunner;
    }

    public void GetSpawnPose(
        Transform spawnPoint,
        out Vector3 respawnPosition,
        out Quaternion respawnRotation
    )
    {
        respawnPosition = ownerTransform.position;
        respawnRotation = ownerTransform.rotation;

        if (spawnPoint == null)
        {
            return;
        }

        respawnPosition = spawnPoint.position;
        respawnRotation = spawnPoint.rotation;
    }

    public void ApplyAuthoritativeTeleport(Vector3 position, Quaternion rotation)
    {
        NetworkObject networkObject = GetNetworkObjectOrNull();

        if (networkObject == null || !networkObject.HasStateAuthority)
        {
            return;
        }

        bool wasCharacterControllerEnabled =
            characterController != null && characterController.enabled;

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (networkTransform != null)
        {
            networkTransform.Teleport(position, rotation);
        }
        else
        {
            ownerTransform.SetPositionAndRotation(position, rotation);
        }

        if (characterController != null)
        {
            characterController.enabled = wasCharacterControllerEnabled;
        }

        Debug.Log(
            $"{ownerObject.name} ApplyAuthoritativeTeleport. " +
            $"HasStateAuthority={networkObject.HasStateAuthority}, " +
            $"Position={position}, " +
            $"ActualPosition={ownerTransform.position}"
        );
    }

    public void ApplyAfterRespawn(Vector3 position, Quaternion rotation)
    {
        NetworkObject networkObject = GetNetworkObjectOrNull();

        lifeVisualController.ApplyAliveState(true);
        notifyHealthChanged?.Invoke();

        if (networkObject != null && !networkObject.HasStateAuthority)
        {
            ApplyNonAuthorityTransform(position, rotation);
        }

        if (playerMove != null)
        {
            playerMove.ResetAfterRespawn();
        }

        if (playerCamera != null && networkObject != null && networkObject.HasInputAuthority)
        {
            playerCamera.UpdateCameraTarget();
        }

        if (networkObject != null && networkObject.HasInputAuthority)
        {
            NetworkLauncher launcher = UnityEngine.Object.FindFirstObjectByType<NetworkLauncher>();

            if (launcher != null)
            {
                launcher.NotifyLocalPlayerSpawnedOnWaitingPlanet();
            }
        }

        NetworkRunner runner = GetRunnerOrNull();

        Debug.Log(
            $"{ownerObject.name} RPC_AfterRespawn applied. " +
            $"RunnerLocalPlayer={(runner != null ? runner.LocalPlayer.ToString() : "null")}, " +
            $"InputAuthority={(networkObject != null ? networkObject.InputAuthority.ToString() : "null")}, " +
            $"HasInputAuthority={(networkObject != null && networkObject.HasInputAuthority)}, " +
            $"HasStateAuthority={(networkObject != null && networkObject.HasStateAuthority)}, " +
            $"Position={position}, " +
            $"ActualPosition={ownerTransform.position}"
        );
    }

    public void RefillWeaponAmmo()
    {
        if (playerWeapon != null)
        {
            playerWeapon.RefillAmmoImmediately();
        }
    }

    public IEnumerator CheckPositionAfterRespawn(float delay)
    {
        yield return new WaitForSeconds(delay);

        NetworkObject networkObject = GetNetworkObjectOrNull();
        NetworkRunner runner = GetRunnerOrNull();

        Debug.Log(
            $"{ownerObject.name} CheckPositionAfterRespawn. " +
            $"RunnerLocalPlayer={(runner != null ? runner.LocalPlayer.ToString() : "null")}, " +
            $"InputAuthority={(networkObject != null ? networkObject.InputAuthority.ToString() : "null")}, " +
            $"HasInputAuthority={(networkObject != null && networkObject.HasInputAuthority)}, " +
            $"HasStateAuthority={(networkObject != null && networkObject.HasStateAuthority)}, " +
            $"Position={ownerTransform.position}"
        );
    }

    private void ApplyNonAuthorityTransform(Vector3 position, Quaternion rotation)
    {
        bool wasCharacterControllerEnabled =
            characterController != null && characterController.enabled;

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        ownerTransform.SetPositionAndRotation(position, rotation);

        if (characterController != null)
        {
            characterController.enabled = wasCharacterControllerEnabled;
        }
    }

    private NetworkObject GetNetworkObjectOrNull()
    {
        return getNetworkObject != null ? getNetworkObject() : null;
    }

    private NetworkRunner GetRunnerOrNull()
    {
        return getRunner != null ? getRunner() : null;
    }
}
