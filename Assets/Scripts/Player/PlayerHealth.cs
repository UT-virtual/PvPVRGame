using System;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(CharacterController))]
public class PlayerHealth : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 10.0f;

    public event Action<float, float> OnHealthChanged;

    [Header("Death Visibility")]
    [SerializeField] private GameObject visualRoot;

    [Header("Weapon Visibility")]
    [SerializeField] private GameObject weaponVisualRoot;

    [Header("Death UI Visibility")]
    [SerializeField] private GameObject overheadIconRoot;

    public GameObject OverheadIconRoot => overheadIconRoot;
    public Renderer[] BodyRenderers => renderers;

    private Renderer[] weaponRenderers;

    [Networked, OnChangedRender(nameof(OnNetworkedHealthChanged))]
    public float NetworkedCurrentHealth { get; private set; }

    [Networked, OnChangedRender(nameof(OnNetworkedDeadChanged))]
    public NetworkBool NetworkedIsDead { get; private set; }

    [Networked]
    public NetworkBool NetworkedIsReady { get; private set; }

    [Networked]
    public int NetworkedTeamIndex { get; private set; }

    public bool IsReady => NetworkedIsReady;
    public bool HasTeamAssigned => NetworkedTeamIndex >= 0;
    public TeamColor Team => (TeamColor)NetworkedTeamIndex;

    public void SetReadyState(bool ready)
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            return;
        }

        NetworkedIsReady = ready;
    }

    public void SetTeam(TeamColor team)
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            return;
        }

        NetworkedTeamIndex = (int)team;
    }

    private NetworkTransform networkTransform;
    private CharacterController characterController;
    private PlayerMove playerMove;
    private PlayerWeapon playerWeapon;
    private PlayerCamera playerCamera;

    private Renderer[] renderers;
    private Collider[] colliders;

    private float damageTakenMultiplier = 1.0f;

    private bool delayedDamageActive;
    private float delayedDamageTotal;

    private Vector3 originalVisualRootScale = Vector3.one;
    private bool hasOriginalVisualRootScale;

    private float originalCharacterControllerHeight;
    private float originalCharacterControllerRadius;
    private Vector3 originalCharacterControllerCenter;
    private bool hasOriginalCharacterControllerValues;

    public float CurrentHealth => NetworkedCurrentHealth;
    public float MaxHealth => maxHealth;
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
            originalVisualRootScale = visualRoot.transform.localScale;
            hasOriginalVisualRootScale = true;
        }
        else
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
        }

        if (weaponVisualRoot != null)
        {
            weaponRenderers = weaponVisualRoot.GetComponentsInChildren<Renderer>(true);
        }
        else
        {
            weaponRenderers = System.Array.Empty<Renderer>();
        }

        if (characterController != null)
        {
            originalCharacterControllerHeight = characterController.height;
            originalCharacterControllerRadius = characterController.radius;
            originalCharacterControllerCenter = characterController.center;
            hasOriginalCharacterControllerValues = true;
        }
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            NetworkedCurrentHealth = maxHealth;
            NetworkedIsDead = false;
            NetworkedIsReady = false;
            NetworkedTeamIndex = -1;
            damageTakenMultiplier = 1.0f;
        }

        ApplyAliveState(true);
        NotifyHealthChanged();

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

    private void OnNetworkedHealthChanged()
    {
        NotifyHealthChanged();
    }

    private void OnNetworkedDeadChanged()
    {
        ApplyAliveState(!NetworkedIsDead);
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke(NetworkedCurrentHealth, maxHealth);
    }

    public void SetDamageTakenMultiplier(float multiplier)
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            return;
        }

        damageTakenMultiplier = Mathf.Clamp(multiplier, 0.0f, 10.0f);
    }

    public void SetDelayedDamageMode(bool active)
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            return;
        }

        if (delayedDamageActive == active)
        {
            return;
        }

        delayedDamageActive = active;

        if (!delayedDamageActive)
        {
            ApplyDelayedDamage();
        }
    }

    private void ApplyDelayedDamage()
    {
        if (delayedDamageTotal <= 0.0f)
        {
            delayedDamageTotal = 0.0f;
            return;
        }

        if (IsDead)
        {
            delayedDamageTotal = 0.0f;
            return;
        }

        float damageToApply = delayedDamageTotal;
        delayedDamageTotal = 0.0f;

        ApplyDamageImmediately(
            damageToApply,
            damageToApply,
            "DelayedDamage"
        );
    }

    private void ApplyDamageImmediately(float actualDamage, float baseDamage, string reason)
    {
        NetworkedCurrentHealth -= actualDamage;
        NetworkedCurrentHealth = Mathf.Max(NetworkedCurrentHealth, 0.0f);

        NotifyHealthChanged();

        Debug.Log(
            $"{gameObject.name} HP: {NetworkedCurrentHealth:0.00}/{maxHealth:0.00}, " +
            $"Reason={reason}, " +
            $"BaseDamage={baseDamage:0.00}, " +
            $"ActualDamage={actualDamage:0.00}, " +
            $"DamageTakenMultiplier={damageTakenMultiplier:0.00}"
        );

        if (NetworkedCurrentHealth <= 0.0f)
        {
            Die();
        }
    }

    public void SetBodySizeMultiplier(float multiplier)
    {
        multiplier = Mathf.Clamp(multiplier, 0.2f, 2.0f);

        if (visualRoot != null && hasOriginalVisualRootScale)
        {
            visualRoot.transform.localScale = originalVisualRootScale * multiplier;
        }

        if (characterController != null && hasOriginalCharacterControllerValues)
        {
            characterController.height = originalCharacterControllerHeight * multiplier;
            characterController.radius = originalCharacterControllerRadius * multiplier;
            characterController.center = originalCharacterControllerCenter * multiplier;
        }
    }

    public void TakeDamage(float damage)
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            return;
        }

        if (IsDead)
        {
            return;
        }

        if (damage <= 0.0f)
        {
            return;
        }

        float actualDamage = damage * damageTakenMultiplier;
        actualDamage = Mathf.Max(actualDamage, 0.0f);

        if (actualDamage <= 0.0f)
        {
            Debug.Log(
                $"{gameObject.name} damage ignored. " +
                $"BaseDamage={damage}, Multiplier={damageTakenMultiplier}"
            );
            return;
        }

        if (delayedDamageActive)
        {
            delayedDamageTotal += actualDamage;

            Debug.Log(
                $"{gameObject.name} delayed damage stored. " +
                $"Stored={delayedDamageTotal:0.00}, " +
                $"Incoming={actualDamage:0.00}"
            );

            return;
        }

        ApplyDamageImmediately(
            actualDamage,
            damage,
            "TakeDamage"
        );
    }

    public bool Heal(float amount)
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            return false;
        }

        if (IsDead)
        {
            return false;
        }

        if (amount <= 0.0f)
        {
            return false;
        }

        if (NetworkedCurrentHealth >= maxHealth)
        {
            return false;
        }

        float beforeHealth = NetworkedCurrentHealth;

        NetworkedCurrentHealth += amount;
        NetworkedCurrentHealth = Mathf.Min(NetworkedCurrentHealth, maxHealth);

        NotifyHealthChanged();

        Debug.Log(
            $"{gameObject.name} healed: " +
            $"{beforeHealth:0.00} -> {NetworkedCurrentHealth:0.00}/{maxHealth:0.00}"
        );

        return NetworkedCurrentHealth > beforeHealth;
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
        damageTakenMultiplier = 1.0f;

        NotifyHealthChanged();

        Debug.Log(
            $"{gameObject.name} respawn request. " +
            $"SpawnPoint={(spawnPoint != null ? spawnPoint.name : "null")}, " +
            $"Position={respawnPosition}"
        );

        ApplyAuthoritativeTeleport(respawnPosition, respawnRotation);

        RPC_AfterRespawn(respawnPosition, respawnRotation);

        if (playerWeapon != null)
        {
            playerWeapon.RefillAmmoImmediately();
        }

        Debug.Log($"{gameObject.name} respawned. HP: {NetworkedCurrentHealth:0.00}/{maxHealth:0.00}");
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
        NotifyHealthChanged();

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

        if (Object.HasInputAuthority)
        {
            NetworkLauncher launcher = FindFirstObjectByType<NetworkLauncher>();

            if (launcher != null)
            {
                launcher.NotifyLocalPlayerSpawnedOnWaitingPlanet();
            }
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
        bool shouldShowBodyModel = alive && !Object.HasInputAuthority;
        bool shouldShowWeapon = alive;
        bool shouldShowOverheadIcon = alive && !Object.HasInputAuthority;

        SetRenderersEnabled(renderers, shouldShowBodyModel);
        SetRenderersEnabled(weaponRenderers, shouldShowWeapon);

        if (overheadIconRoot != null)
        {
            overheadIconRoot.SetActive(shouldShowOverheadIcon);
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
}