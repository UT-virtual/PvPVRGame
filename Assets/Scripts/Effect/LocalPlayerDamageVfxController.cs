using Fusion;
using UnityEngine;

/// <summary>同期HPの減少を、本人または現在の観戦対象の画面演出に変換する。</summary>
public class LocalPlayerDamageVfxController : NetworkBehaviour
{
    [SerializeField, Min(0.05f)] private float damageFlashDuration = 0.3f;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.05f, 0.02f, 0.55f);

    private PlayerHealth playerHealth;
    private float previousHealth;
    private bool isSubscribed;
    private DamageScreenEffect screenEffect;

    private void Awake() => playerHealth = GetComponentInParent<PlayerHealth>();

    public override void Spawned()
    {
        if (playerHealth == null) return;
        previousHealth = playerHealth.CurrentHealth;
        screenEffect = DamageScreenEffect.Create(this);
        playerHealth.OnHealthChanged += HandleHealthChanged;
        isSubscribed = true;
    }

    public bool IsViewed
    {
        get
        {
            if (Object == null || !Object.IsValid) return false;
            SpectatorCameraController spectator = SpectatorCameraController.Instance;
            if (spectator != null && spectator.IsSpectatingActive)
                return spectator.CurrentSpectatorTarget == playerHealth;
            return Object.HasInputAuthority;
        }
    }

    private void HandleHealthChanged(float current, float max)
    {
        bool tookDamage = current < previousHealth;
        previousHealth = current;
        if (tookDamage && IsViewed && screenEffect != null)
            screenEffect.Flash(damageFlashDuration, damageFlashColor);
    }

    public override void Despawned(NetworkRunner runner, bool hasState) => Release();
    private void OnDestroy() => Release();

    private void Release()
    {
        if (isSubscribed && playerHealth != null) playerHealth.OnHealthChanged -= HandleHealthChanged;
        isSubscribed = false;
        if (screenEffect != null) Destroy(screenEffect.gameObject);
        screenEffect = null;
    }
}
