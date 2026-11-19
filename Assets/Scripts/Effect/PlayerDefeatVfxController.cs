using Fusion;
using UnityEngine;

public class PlayerDefeatVfxController : NetworkBehaviour
{
    [Header("Defeat Warp VFX Settings")]
    [Tooltip("倒された時に生成されるワープエフェクトのPrefab")]
    [SerializeField] private GameObject defeatWarpVfxPrefab;
    [Tooltip("生成位置のローカルオフセット（足元基準なら Y = 0）")]
    [SerializeField] private Vector3 localSpawnOffset = Vector3.zero;
    [Tooltip("生成したエフェクトを自動破棄するまでの秒数")]
    [SerializeField] private float vfxLifeTime = 3.0f;

    private PlayerHealth playerHealth;
    private bool wasDead;

    private void Awake()
    {
        playerHealth = GetComponentInParent<PlayerHealth>();
    }

    public override void Spawned()
    {
        wasDead = playerHealth != null && playerHealth.IsDead;
    }

    public override void Render()
    {
        if (playerHealth == null)
        {
            return;
        }

        bool isDeadNow = playerHealth.IsDead;

        // 生存 → 死亡 に切り替わった瞬間を全クライアントで検知
        if (!wasDead && isDeadNow)
        {
            PlayDefeatWarpVfx();
        }

        wasDead = isDeadNow;
    }

    private void PlayDefeatWarpVfx()
    {
        if (defeatWarpVfxPrefab == null)
        {
            return;
        }

        Transform playerRoot = playerHealth.transform;
        Vector3 spawnPosition = playerRoot.TransformPoint(localSpawnOffset);
        Quaternion spawnRotation = playerRoot.rotation;

        GameObject vfxInstance = Instantiate(defeatWarpVfxPrefab, spawnPosition, spawnRotation);
        if (vfxLifeTime > 0.0f)
        {
            Destroy(vfxInstance, vfxLifeTime);
        }
    }
}
