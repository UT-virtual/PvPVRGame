using System;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(CharacterController))]
public class PlayerHealth : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 10;

    [Header("Death Visibility")]
    [SerializeField] private GameObject visualRoot;

    [Networked] public int NetworkedCurrentHealth { get; private set; }
    [Networked] public NetworkBool NetworkedIsDead { get; private set; }

    private NetworkTransform networkTransform;
    private CharacterController characterController;
    private PlayerMove playerMove;
    private PlayerWeapon playerWeapon;
    private PlayerCamera playerCamera;

    private Renderer[] renderers;
    private Collider[] colliders;

    public int CurrentHealth => NetworkedCurrentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => NetworkedIsDead;

    public event Action<PlayerHealth> OnDied;

    private void Awake()
    {
        networkTransform = GetComponent<NetworkTransform>();
        characterController = GetComponent<CharacterController>();
        playerMove = GetComponent<PlayerMove>();
        playerWeapon = GetComponent<PlayerWeapon>();
        playerCamera = GetComponent<PlayerCamera>();

        if (visualRoot != null)
        {
            renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            colliders = visualRoot.GetComponentsInChildren<Collider>(true);
        }
        else
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            NetworkedCurrentHealth = maxHealth;
            NetworkedIsDead = false;
        }

        ApplyAliveState(true);

        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (Object.InputAuthority == PlayerRef.None)
        {
            Debug.Log($"Skip round registration: {gameObject.name} has no InputAuthority");
            return;
        }

        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.RegisterPlayer(this);
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: RoundManager.Instance is null");
        }
    }

    private void OnDestroy()
    {
        if (RoundManager.Instance != null)
        {
            RoundManager.Instance.UnregisterPlayer(this);
        }
    }

    public void TakeDamage(int damage)
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            return;
        }

        if (IsDead)
        {
            return;
        }

        if (damage <= 0)
        {
            return;
        }

        NetworkedCurrentHealth -= damage;
        NetworkedCurrentHealth = Mathf.Max(NetworkedCurrentHealth, 0);

        Debug.Log($"{gameObject.name} HP: {NetworkedCurrentHealth}/{maxHealth}");

        if (NetworkedCurrentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (IsDead)
        {
            return;
        }

        NetworkedIsDead = true;

        Debug.Log($"{gameObject.name} died");
        Debug.Log($"OnDied has subscriber: {OnDied != null}");

        RPC_SetAliveState(false);

        OnDied?.Invoke(this);
    }

    public void RespawnForRound(Transform spawnPoint)
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            return;
        }

        Vector3 respawnPosition = transform.position;
        Quaternion respawnRotation = transform.rotation;

        if (spawnPoint != null)
        {
            respawnPosition = spawnPoint.position;
            respawnRotation = spawnPoint.rotation;
        }

        NetworkedCurrentHealth = maxHealth;
        NetworkedIsDead = false;

        Debug.Log(
            $"{gameObject.name} respawn request. " +
            $"SpawnPoint={(spawnPoint != null ? spawnPoint.name : "null")}, " +
            $"Position={respawnPosition}"
        );

        ApplyAuthoritativeTeleport(respawnPosition, respawnRotation);

        RPC_AfterRespawn(respawnPosition, respawnRotation);

        if (playerWeapon != null)
        {
            playerWeapon.ReloadAmmo();
        }

        Debug.Log($"{gameObject.name} respawned. HP: {NetworkedCurrentHealth}/{maxHealth}");
    }

    private void ApplyAuthoritativeTeleport(Vector3 position, Quaternion rotation)
    {
        if (!Object.HasStateAuthority)
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
            transform.SetPositionAndRotation(position, rotation);
        }

        if (characterController != null)
        {
            characterController.enabled = wasCharacterControllerEnabled;
        }

        Debug.Log(
            $"{gameObject.name} ApplyAuthoritativeTeleport. " +
            $"HasStateAuthority={Object.HasStateAuthority}, " +
            $"Position={position}, " +
            $"ActualPosition={transform.position}"
        );
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AfterRespawn(Vector3 position, Quaternion rotation)
    {
        ApplyAliveState(true);

        if (!Object.HasStateAuthority)
        {
            bool wasCharacterControllerEnabled =
                characterController != null && characterController.enabled;

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.SetPositionAndRotation(position, rotation);

            if (characterController != null)
            {
                characterController.enabled = wasCharacterControllerEnabled;
            }
        }

        if (playerMove != null)
        {
            playerMove.ResetAfterRespawn();
        }

        if (playerCamera != null && Object.HasInputAuthority)
        {
            playerCamera.UpdateCameraTarget();
        }

        Debug.Log(
            $"{gameObject.name} RPC_AfterRespawn applied. " +
            $"RunnerLocalPlayer={Runner.LocalPlayer}, " +
            $"InputAuthority={Object.InputAuthority}, " +
            $"HasInputAuthority={Object.HasInputAuthority}, " +
            $"HasStateAuthority={Object.HasStateAuthority}, " +
            $"Position={position}, " +
            $"ActualPosition={transform.position}"
        );

        StartCoroutine(CheckPositionAfterRespawn(0.5f));
    }

    private System.Collections.IEnumerator CheckPositionAfterRespawn(float delay)
    {
        yield return new WaitForSeconds(delay);

        Debug.Log(
            $"{gameObject.name} CheckPositionAfterRespawn. " +
            $"RunnerLocalPlayer={Runner.LocalPlayer}, " +
            $"InputAuthority={Object.InputAuthority}, " +
            $"HasInputAuthority={Object.HasInputAuthority}, " +
            $"HasStateAuthority={Object.HasStateAuthority}, " +
            $"Position={transform.position}"
        );
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetAliveState(bool alive)
    {
        ApplyAliveState(alive);
    }

    private void ApplyAliveState(bool alive)
    {
        bool shouldShowModel = alive && !Object.HasInputAuthority;

        if (renderers != null)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = shouldShowModel;
                }
            }
        }

        if (colliders != null)
        {
            foreach (Collider collider in colliders)
            {
                if (collider != null)
                {
                    collider.enabled = alive;
                }
            }
        }

        if (characterController != null)
        {
            characterController.enabled = alive;
        }
    }
}