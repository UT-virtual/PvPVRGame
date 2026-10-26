using System;
using Fusion;
using UnityEngine;

/// <summary>
/// StateAuthorityでHP・生死・準備・チームを管理し、同期プロパティとRPCを通じて表示へ反映する。
/// 死亡をRoundManagerへ通知し、ラウンド復活時には移動状態・位置・弾薬も復旧する。
/// </summary>
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

    /// <summary>
    /// 準備状態の同期フラグを更新する。NetworkObjectがある場合はStateAuthorityに限る。
    /// </summary>
    public void SetReadyState(bool ready)
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            return;
        }

        NetworkedIsReady = ready;
    }

    /// <summary>
    /// 結果表示・待機UI・頭上アイコンのチーム色を同期する。NetworkObjectがある場合はStateAuthorityに限る。
    /// </summary>
    public void SetTeam(TeamColor team)
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            return;
        }

        NetworkedTeamIndex = (int)team;
        PlayerIdentity identity = GetComponent<PlayerIdentity>();
        if (identity != null)
        {
            identity.SetIdentity(identity.PlayerNumber, NetworkedTeamIndex);
        }
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

        ApplyAliveState(!NetworkedIsDead);
        NotifyHealthChanged();

        if (!Object.HasStateAuthority)
        {
            return;
        }

        // 観戦確認用ダミーなど操作所有者のないオブジェクトは通常のラウンド参加者へ登録しない。
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

    public override void Despawned(NetworkRunner runner, bool hasState)
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

    /// <summary>
    /// StateAuthority側で被ダメージを保留する状態を切り替える。
    /// 解除時は軽減済みの蓄積値をまとめて適用するため、死亡が発生し得る。
    /// </summary>
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

    /// <summary>
    /// 保存した初期値を基準に、表示モデルとCharacterControllerの寸法を一緒に変更する。
    /// Authorityによる制限は設けず、各端末のスキル効果反映から呼ばれる。
    /// </summary>
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

    /// <summary>
    /// StateAuthority側で正のダメージに軽減倍率を掛け、HP減少または保留分への加算を行う。
    /// HPが尽きると死亡状態を通知し、ラウンド進行へ死亡イベントを送る。
    /// </summary>
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

    /// <summary>
    /// StateAuthority側で生存者のHPを上限まで回復する。
    /// 回復不能またはHPが増えなかった場合はfalseを返し、回復アイテムの消費判定に使う。
    /// </summary>
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

    /// <summary>
    /// StateAuthority側でHPと被ダメージ倍率を戻し、位置の確定・全端末の復活後処理・弾薬補充を行う。
    /// spawnPointがnullなら現在の位置と回転を維持する。
    /// </summary>
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
        delayedDamageActive = false;
        delayedDamageTotal = 0.0f;

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

    /// <summary>
    /// CharacterControllerを一時停止し、NetworkTransform.Teleportで復活位置を確定する。
    /// NetworkTransformがない場合のみTransformを直接設定する。
    /// </summary>
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

    /// <summary>
    /// StateAuthorityから全端末へ復活後処理を通知する。
    /// 生存表示を戻して移動座標系を再構築し、InputAuthority側ではカメラと待機UIも更新する。
    /// </summary>
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
        // 生存中でも自分の一人称モデルと頭上アイコンは隠す。武器は別の表示規則を使う。
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