using Fusion;
using UnityEngine;

/// <summary>
/// ラウンドの切り替え時に残弾を探し、その弾のStateAuthorityを持つ場合だけDespawnする。
/// </summary>
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