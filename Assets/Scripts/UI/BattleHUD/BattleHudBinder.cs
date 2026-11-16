using System.Collections;
using Fusion;
using UnityEngine;

/// <summary>
/// InputAuthorityを持つプレイヤーを継続探索し、HP・弾薬・スキルのHUD参照を接続する。
/// 観戦中は自動接続を抑止し、SpectatorCameraControllerが指定する対象へ切り替える。
/// </summary>
public class BattleHudBinder : MonoBehaviour
{
    [SerializeField] private HealthBar healthBar;
    [SerializeField] private AmmoBar ammoBar;
    [SerializeField] private SkillHudUI skillHudUI;

    private PlayerHealth currentHealth;
    private PlayerWeapon currentWeapon;
    private PlayerSkillController currentSkillController;

    private bool spectatorOverride;
    private Coroutine bindLocalCoroutine;

    private void Awake()
    {
        if (healthBar == null)
        {
            healthBar = FindFirstObjectByType<HealthBar>();
        }

        if (ammoBar == null)
        {
            ammoBar = FindFirstObjectByType<AmmoBar>();
        }

        if (skillHudUI == null)
        {
            skillHudUI = FindFirstObjectByType<SkillHudUI>();
        }
    }

    private void OnEnable()
    {
        StartBindLocalPlayer();
    }

    private void OnDisable()
    {
        if (bindLocalCoroutine != null)
        {
            StopCoroutine(bindLocalCoroutine);
            bindLocalCoroutine = null;
        }
    }

    private void StartBindLocalPlayer()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (bindLocalCoroutine != null)
        {
            StopCoroutine(bindLocalCoroutine);
        }

        bindLocalCoroutine = StartCoroutine(BindLocalPlayerWhenReady());
    }

    private IEnumerator BindLocalPlayerWhenReady()
    {
        while (true)
        {
            // 再生成されたローカルプレイヤーにも追従するが、観戦中にHUDを自分へ戻さない。
            if (!spectatorOverride)
            {
                PlayerHealth localPlayer = FindLocalPlayerHealth();

                if (localPlayer != null && currentHealth != localPlayer)
                {
                    BindToPlayerInternal(localPlayer);
                }
            }

            yield return null;
        }
    }

    /// <summary>
    /// 観戦による固定を解除し、ローカルプレイヤーへ接続する。未生成なら継続探索する。
    /// </summary>
    public void BindToLocalPlayer()
    {
        spectatorOverride = false;

        PlayerHealth localPlayer = FindLocalPlayerHealth();

        if (localPlayer != null)
        {
            BindToPlayerInternal(localPlayer);
        }
        else
        {
            ClearBindings();
            StartBindLocalPlayer();
        }
    }

    /// <summary>
    /// 自動のローカル接続を抑止して観戦対象へHUDを接続する。
    /// 観戦終了時はBindToLocalPlayerで固定を解除する。
    /// </summary>
    public void BindToSpectatorTarget(PlayerHealth playerHealth)
    {
        spectatorOverride = true;
        BindToPlayerInternal(playerHealth);
    }

    /// <summary>
    /// HUDの参照先だけを差し替える。自動接続を固定しないため、観戦にはBindToSpectatorTargetを使う。
    /// </summary>
    public void BindToPlayer(PlayerHealth playerHealth)
    {
        BindToPlayerInternal(playerHealth);
    }

    /// <summary>
    /// 観戦対象がいない間は購読と表示値を消去する。ローカル探索を固定し、古い対象のHPを残さない。
    /// </summary>
    public void ClearSpectatorTarget()
    {
        spectatorOverride = true;
        ClearBindings();
    }

    private void ClearBindings()
    {
        currentHealth = null;
        currentWeapon = null;
        currentSkillController = null;
        if (healthBar != null) healthBar.SetupPlayerHealth(null);
        if (ammoBar != null) ammoBar.SetupWeapon(null);
        if (skillHudUI != null) skillHudUI.SetupSkillController(null);
    }

    private void BindToPlayerInternal(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
        {
            Debug.LogWarning("[BattleHudBinder] Bind failed. playerHealth is null.");
            return;
        }

        PlayerWeapon playerWeapon = playerHealth.GetComponent<PlayerWeapon>();
        PlayerSkillController playerSkillController = playerHealth.GetComponent<PlayerSkillController>();

        currentHealth = playerHealth;
        currentWeapon = playerWeapon;
        currentSkillController = playerSkillController;

        if (healthBar != null)
        {
            healthBar.SetupPlayerHealth(currentHealth);
        }
        else
        {
            Debug.LogWarning("[BattleHudBinder] HealthBar is null.");
        }

        if (ammoBar != null && currentWeapon != null)
        {
            ammoBar.SetupWeapon(currentWeapon);
        }
        else if (ammoBar == null)
        {
            Debug.LogWarning("[BattleHudBinder] AmmoBar is null.");
        }
        else
        {
            Debug.LogWarning($"[BattleHudBinder] PlayerWeapon not found: {playerHealth.name}");
        }

        if (skillHudUI != null && currentSkillController != null)
        {
            skillHudUI.SetupSkillController(currentSkillController);
        }
        else if (skillHudUI == null)
        {
            Debug.LogWarning("[BattleHudBinder] SkillHudUI is null.");
        }
        else
        {
            Debug.LogWarning($"[BattleHudBinder] PlayerSkillController not found: {playerHealth.name}");
        }

        Debug.Log(
            $"[BattleHudBinder] Bound HUD to {playerHealth.name}. " +
            $"HP={playerHealth.CurrentHealth}/{playerHealth.MaxHealth}, " +
            $"Weapon={(playerWeapon != null ? playerWeapon.name : "null")}, " +
            $"SkillController={(playerSkillController != null ? playerSkillController.name : "null")}, " +
            $"SpectatorOverride={spectatorOverride}"
        );
    }

    private PlayerHealth FindLocalPlayerHealth()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);

        foreach (PlayerHealth player in players)
        {
            if (player == null)
            {
                continue;
            }

            NetworkObject networkObject = player.GetComponent<NetworkObject>();

            if (networkObject != null && networkObject.HasInputAuthority)
            {
                return player;
            }
        }

        return null;
    }
}