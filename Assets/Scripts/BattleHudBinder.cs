using UnityEngine;

public class BattleHudBinder : MonoBehaviour
{
    [SerializeField] private HealthBar healthBar;
    [SerializeField] private AmmoBar ammoBar;

    private PlayerHealth currentHealth;
    private PlayerWeapon currentWeapon;

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

    public void BindToPlayer(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
        {
            Debug.LogWarning("[BattleHudBinder] BindToPlayer failed. playerHealth is null.");
            return;
        }

        PlayerWeapon playerWeapon = playerHealth.GetComponent<PlayerWeapon>();

        currentHealth = playerHealth;
        currentWeapon = playerWeapon;

        if (healthBar != null)
        {
            healthBar.SetupPlayerHealth(currentHealth);
        }

        if (ammoBar != null && currentWeapon != null)
        {
            ammoBar.SetupWeapon(currentWeapon);
        }

        Debug.Log(
            $"[BattleHudBinder] Bound HUD to {playerHealth.name}. " +
            $"HP={playerHealth.CurrentHealth}/{playerHealth.MaxHealth}, " +
            $"Weapon={(playerWeapon != null ? playerWeapon.name : "null")}"
        );
    }
}