using UnityEngine;

public sealed class WeaponShotState
{
    public float FireIntervalMultiplier { get; private set; } = 1.0f;
    public float ProjectileSpeedMultiplier { get; private set; } = 1.0f;
    public float DamageDealtMultiplier { get; private set; } = 1.0f;
    public bool InstantReloadEnabled { get; private set; }

    private WeaponShotKind loadedShotKind = WeaponShotKind.Normal;
    private int loadedSpecialAmmoRemaining;
    private float pendingNextShotDamageMultiplier = 1.0f;

    public float PendingNextShotDamageMultiplier => pendingNextShotDamageMultiplier;

    public void Reset()
    {
        FireIntervalMultiplier = 1.0f;
        ProjectileSpeedMultiplier = 1.0f;
        DamageDealtMultiplier = 1.0f;
        InstantReloadEnabled = false;

        ClearLoadedSpecialAmmo();
        pendingNextShotDamageMultiplier = 1.0f;
    }

    public void SetFireIntervalMultiplier(float multiplier)
    {
        FireIntervalMultiplier = Mathf.Clamp(multiplier, 0.05f, 10.0f);
    }

    public void SetProjectileSpeedMultiplier(float multiplier)
    {
        ProjectileSpeedMultiplier = Mathf.Clamp(multiplier, 0.1f, 10.0f);
    }

    public void SetDamageDealtMultiplier(float multiplier)
    {
        DamageDealtMultiplier = Mathf.Clamp(multiplier, 0.0f, 10.0f);
    }

    public void SetInstantReloadEnabled(bool enabled)
    {
        InstantReloadEnabled = enabled;
    }

    public WeaponShotKind GetCurrentShotKind()
    {
        if (loadedShotKind == WeaponShotKind.Normal || loadedSpecialAmmoRemaining <= 0)
        {
            return WeaponShotKind.Normal;
        }

        return loadedShotKind;
    }

    public int LoadSpecialAmmo(
        WeaponShotKind shotKind,
        int ammoCount,
        int maxAmmo
    )
    {
        int clampedAmmoCount = Mathf.Clamp(ammoCount, 1, maxAmmo);

        loadedShotKind = shotKind;
        loadedSpecialAmmoRemaining = clampedAmmoCount;

        return clampedAmmoCount;
    }

    public void ConsumeLoadedSpecialAmmo(int currentAmmoAfterShot)
    {
        if (loadedShotKind == WeaponShotKind.Normal || loadedSpecialAmmoRemaining <= 0)
        {
            return;
        }

        loadedSpecialAmmoRemaining = Mathf.Max(loadedSpecialAmmoRemaining - 1, 0);

        if (loadedSpecialAmmoRemaining > 0 && currentAmmoAfterShot > 0)
        {
            return;
        }

        ClearLoadedSpecialAmmo();
    }

    public void ClearLoadedSpecialAmmo()
    {
        loadedShotKind = WeaponShotKind.Normal;
        loadedSpecialAmmoRemaining = 0;
    }

    public void SetNextShotDamageMultiplier(float multiplier)
    {
        pendingNextShotDamageMultiplier = Mathf.Clamp(multiplier, 1.0f, 10.0f);
    }

    public float ConsumeNextShotDamageMultiplier()
    {
        float result = pendingNextShotDamageMultiplier;
        pendingNextShotDamageMultiplier = 1.0f;
        return result;
    }

    public void ClearNextShotDamageMultiplier()
    {
        pendingNextShotDamageMultiplier = 1.0f;
    }
}
