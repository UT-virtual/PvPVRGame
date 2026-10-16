using Fusion;
using UnityEngine;

public sealed class RoundProjectileCleaner
{
    public void DespawnProjectiles()
    {
        Projectile[] projectiles = UnityEngine.Object.FindObjectsByType<Projectile>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Projectile projectile in projectiles)
        {
            if (projectile == null)
            {
                continue;
            }

            NetworkObject networkObject = projectile.Object;

            if (networkObject == null)
            {
                continue;
            }

            if (!networkObject.HasStateAuthority)
            {
                continue;
            }

            projectile.Runner.Despawn(networkObject);
        }
    }
}