using System.Collections;
using Fusion;
using UnityEngine;

public class BattleHudBinder : MonoBehaviour
{
    [SerializeField] private HealthBar healthBar;
    [SerializeField] private AmmoBar ammoBar;

    private PlayerHealth currentHealth;
    private PlayerWeapon currentWeapon;

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
            StartBindLocalPlayer();
        }
    }

    public void BindToSpectatorTarget(PlayerHealth playerHealth)
    {
        spectatorOverride = true;
        BindToPlayerInternal(playerHealth);
    }

    public void BindToPlayer(PlayerHealth playerHealth)
    {
        BindToPlayerInternal(playerHealth);
    }

    private void BindToPlayerInternal(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
        {
            Debug.LogWarning("[BattleHudBinder] Bind failed. playerHealth is null.");
            return;
        }

        PlayerWeapon playerWeapon = playerHealth.GetComponent<PlayerWeapon>();

        currentHealth = playerHealth;
        currentWeapon = playerWeapon;

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

        Debug.Log(
            $"[BattleHudBinder] Bound HUD to {playerHealth.name}. " +
            $"HP={playerHealth.CurrentHealth}/{playerHealth.MaxHealth}, " +
            $"Weapon={(playerWeapon != null ? playerWeapon.name : "null")}, " +
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