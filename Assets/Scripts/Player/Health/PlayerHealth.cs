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

    [Networked, OnChangedRender(nameof(OnNetworkedHealthChanged))]
    public float NetworkedCurrentHealth { get; private set; }

    [Networked, OnChangedRender(nameof(OnNetworkedDeadChanged))]
    public NetworkBool NetworkedIsDead { get; private set; }

    [Networked]
    public NetworkBool NetworkedIsReady { get; private set; }

    [Networked]
    public int NetworkedTeamIndex { get; private set; }

    public GameObject OverheadIconRoot => lifeVisualController?.OverheadIconRoot;
    public Renderer[] BodyRenderers => lifeVisualController?.BodyRenderers ?? Array.Empty<Renderer>();

    public float CurrentHealth => NetworkedCurrentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => NetworkedIsDead;

    public bool IsReady => NetworkedIsReady;
    public bool HasTeamAssigned => NetworkedTeamIndex >= 0;
    public TeamColor Team => (TeamColor)NetworkedTeamIndex;

    public event Action<PlayerHealth> OnDied;

    private NetworkTransform networkTransform;
    private CharacterController characterController;
    private PlayerMove playerMove;
    private PlayerWeapon playerWeapon;
    private PlayerCamera playerCamera;

    private PlayerLifeVisualController lifeVisualController;
    private PlayerBodyScaleController bodyScaleController;
    private PlayerRespawnController respawnController;

    private float damageTakenMultiplier = 1.0f;

    private bool delayedDamageActive;
    private float delayedDamageTotal;

    private void Awake()
    {
        networkTransform = GetComponent<NetworkTransform>();
        characterController = GetComponent<CharacterController>();
        playerMove = GetComponent<PlayerMove>();
        playerWeapon = GetComponent<PlayerWeapon>();
        playerCamera = GetComponent<PlayerCamera>();

        lifeVisualController = new PlayerLifeVisualController(
            gameObject,
            visualRoot,
            weaponVisualRoot,
            overheadIconRoot,
            characterController,
            () => Object != null && Object.HasInputAuthority
        );

        bodyScaleController = new PlayerBodyScaleController(
            visualRoot,
            characterController
        );

        respawnController = new PlayerRespawnController(
            gameObject,
            transform,
            networkTransform,
            characterController,
            playerMove,
            playerWeapon,
            playerCamera,
            lifeVisualController,
            NotifyHealthChanged,
            () => Object,
            () => Runner
        );
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

    public void SetBodySizeMultiplier(float multiplier)
    {
        bodyScaleController.SetBodySizeMultiplier(multiplier);
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

    public void RespawnForRound(Transform spawnPoint)
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            return;
        }

        respawnController.GetSpawnPose(
            spawnPoint,
            out Vector3 respawnPosition,
            out Quaternion respawnRotation
        );

        NetworkedCurrentHealth = maxHealth;
        NetworkedIsDead = false;
        damageTakenMultiplier = 1.0f;

        NotifyHealthChanged();

        Debug.Log(
            $"{gameObject.name} respawn request. " +
            $"SpawnPoint={(spawnPoint != null ? spawnPoint.name : "null")}, " +
            $"Position={respawnPosition}"
        );

        respawnController.ApplyAuthoritativeTeleport(respawnPosition, respawnRotation);

        RPC_AfterRespawn(respawnPosition, respawnRotation);

        respawnController.RefillWeaponAmmo();

        Debug.Log($"{gameObject.name} respawned. HP: {NetworkedCurrentHealth:0.00}/{maxHealth:0.00}");
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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AfterRespawn(Vector3 position, Quaternion rotation)
    {
        respawnController.ApplyAfterRespawn(position, rotation);
        StartCoroutine(respawnController.CheckPositionAfterRespawn(0.5f));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetAliveState(bool alive)
    {
        ApplyAliveState(alive);
    }

    private void ApplyAliveState(bool alive)
    {
        lifeVisualController.ApplyAliveState(alive);
    }
}
