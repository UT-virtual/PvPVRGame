using Fusion;
using UnityEngine;

public class LocalPlayerDamageVfxController : NetworkBehaviour
{
    [Header("Local Damage VFX")]
    [Tooltip("被弾時に自分の周りに発生させるエフェクトのPrefab")]
    [SerializeField] private GameObject damageVfxPrefab;
    [Tooltip("追従先（未指定の場合はプレイヤー自身。カメラに追従させたい場合は下のオプションをON）")]
    [SerializeField] private Transform customFollowTarget;
    [Tooltip("ONにするとプレイヤー本体ではなくメインカメラに追従します")]
    [SerializeField] private bool followMainCamera = false;
    [Tooltip("追従基準点からのローカル位置オフセット")]
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [Tooltip("エフェクトを自動破棄するまでの秒数")]
    [SerializeField] private float vfxLifeTime = 0.8f;
    [Tooltip("連続被弾時に前のエフェクトを消してから新しく出し直すか")]
    [SerializeField] private bool restartOnContinuousHit = true;
    [Tooltip("HPが0になった瞬間（死亡時）にも被弾エフェクトを出すか")]
    [SerializeField] private bool playOnFatalHit = false;

    private PlayerHealth playerHealth;
    private float previousHealth;
    private bool isSubscribed;
    private GameObject activeVfxInstance;

    private void Awake()
    {
        playerHealth = GetComponentInParent<PlayerHealth>();
    }

    public override void Spawned()
    {
        if (playerHealth == null || Object == null)
        {
            return;
        }

        // 操作権限を持つローカルプレイヤー本人の端末でのみ購読する
        if (!Object.HasInputAuthority)
        {
            return;
        }

        previousHealth = playerHealth.CurrentHealth;
        playerHealth.OnHealthChanged += HandleHealthChanged;
        isSubscribed = true;
    }

    private void OnDestroy()
    {
        if (isSubscribed && playerHealth != null)
        {
            playerHealth.OnHealthChanged -= HandleHealthChanged;
            isSubscribed = false;
        }

        if (activeVfxInstance != null)
        {
            Destroy(activeVfxInstance);
        }
    }

    private void HandleHealthChanged(float current, float max)
    {
        bool tookDamage = current < previousHealth;
        previousHealth = current;

        if (!tookDamage)
        {
            return;
        }

        if (!playOnFatalHit && current <= 0.0f)
        {
            return;
        }

        SpawnLocalDamageVfx();
    }

    private void SpawnLocalDamageVfx()
    {
        if (damageVfxPrefab == null)
        {
            return;
        }

        if (restartOnContinuousHit && activeVfxInstance != null)
        {
            Destroy(activeVfxInstance);
            activeVfxInstance = null;
        }

        Transform target = ResolveFollowTarget();
        GameObject instance = Instantiate(damageVfxPrefab, target);
        instance.transform.localPosition = localOffset;
        instance.transform.localRotation = Quaternion.identity;

        activeVfxInstance = instance;

        if (vfxLifeTime > 0.0f)
        {
            Destroy(instance, vfxLifeTime);
        }
    }

    private Transform ResolveFollowTarget()
    {
        if (customFollowTarget != null)
        {
            return customFollowTarget;
        }

        if (followMainCamera && Camera.main != null)
        {
            return Camera.main.transform;
        }

        return playerHealth != null ? playerHealth.transform : transform;
    }
}