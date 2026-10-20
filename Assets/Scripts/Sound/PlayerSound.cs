using UnityEngine;
using Fusion;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerSound : NetworkBehaviour
{
    [Header("効果音設定 (3D / 既存互換)")]
    [SerializeField] private AudioClip jumpSE;
    [SerializeField] private AudioClip fireSE;
    [SerializeField] private AudioClip reloadingSE;
    [SerializeField] private AudioClip reloadSE;
    [SerializeField] private AudioClip tookDamageSE;
    [SerializeField] private AudioClip diedSE;

    [Header("自分専用SEコンポーネント (未設定でも同オブジェクトから自動取得)")]
    [SerializeField] private PlayerLocalSound playerLocalSound;

    private PlayerController playerController;
    private PlayerMove playerMove;
    private PlayerWeapon playerWeapon;
    private PlayerHealth playerHealth;
    private PlayerSkillController playerSkillController;

    private AudioSource audioSource;
    private AudioSource reloadAudioSource;

    private bool hasInitializedHealth;
    private float previousHealth;
    private bool wasDead;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        audioSource = GetComponent<AudioSource>();
        playerMove = GetComponent<PlayerMove>();
        playerWeapon = GetComponent<PlayerWeapon>();
        playerHealth = GetComponent<PlayerHealth>();
        playerSkillController = GetComponent<PlayerSkillController>();

        if (playerLocalSound == null)
        {
            playerLocalSound = GetComponent<PlayerLocalSound>();
            if (playerLocalSound == null)
            {
                playerLocalSound = gameObject.AddComponent<PlayerLocalSound>();
            }
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
        }

        reloadAudioSource = gameObject.AddComponent<AudioSource>();
        reloadAudioSource.playOnAwake = false;
        reloadAudioSource.spatialBlend = audioSource != null ? audioSource.spatialBlend : 1f;
    }

    public override void Spawned()
    {
        hasInitializedHealth = true;
        if (playerHealth != null)
        {
            previousHealth = playerHealth.CurrentHealth;
            wasDead = playerHealth.IsDead;
        }

        if (playerMove != null)
        {
            playerMove.OnJumped += HandleServerJumped;
        }

        if (playerWeapon != null)
        {
            playerWeapon.OnShot += HandleServerShot;
            playerWeapon.OnReloadStarted += HandleServerReloadStarted;
            playerWeapon.OnReloadCanceled += HandleServerReloadCanceled;
            playerWeapon.OnReloaded += HandleServerReloaded;
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += PlayDamageSound;
        }

        if (playerSkillController != null)
        {
            playerSkillController.OnSkillActivated += HandleServerSkillActivated;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (playerMove != null)
        {
            playerMove.OnJumped -= HandleServerJumped;
        }

        if (playerWeapon != null)
        {
            playerWeapon.OnShot -= HandleServerShot;
            playerWeapon.OnReloadStarted -= HandleServerReloadStarted;
            playerWeapon.OnReloadCanceled -= HandleServerReloadCanceled;
            playerWeapon.OnReloaded -= HandleServerReloaded;
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= PlayDamageSound;
        }

        if (playerSkillController != null)
        {
            playerSkillController.OnSkillActivated -= HandleServerSkillActivated;
        }
    }

    public override void Render()
    {
        if (playerHealth == null || Object == null)
        {
            return;
        }

        // 全クライアントでNetworkedIsDeadの変化を監視し、撃破SE（全体＆個別）を確実に鳴らす
        bool isCurrentlyDead = playerHealth.IsDead;
        if (isCurrentlyDead && !wasDead)
        {
            wasDead = true;
            PlayDiedSound(playerHealth);

            if (RoundSeManager.Instance != null)
            {
                RoundSeManager.Instance.NotifyPlayerDefeated(playerHealth);
            }
        }
        else if (!isCurrentlyDead && wasDead)
        {
            wasDead = false;
        }
    }

    // --- Projectile（弾）からの着弾通知窓口（サーバー側で呼ばれる） ---
    public void NotifyProjectileHitFromServer(bool hitEnemy, Vector3 hitPosition)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        RPC_NotifyProjectileHitToOwner(hitEnemy, hitPosition);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_NotifyProjectileHitToOwner(NetworkBool hitEnemy, Vector3 hitPosition)
    {
        if (playerLocalSound != null)
        {
            playerLocalSound.PlayProjectileHitSe(hitEnemy, hitPosition);
        }
    }

    // --- スキル発動の通知窓口（サーバー側で呼ばれる -> 自分のみにRPC送信） ---
    private void HandleServerSkillActivated(PlayerSkillType skillType)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        RPC_NotifySkillActivatedToOwner(skillType);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_NotifySkillActivatedToOwner(PlayerSkillType skillType)
    {
        if (playerLocalSound != null)
        {
            playerLocalSound.PlaySkillActivatedSe(skillType);
        }
    }

    // --- 既存アクションSE（マルチプレイ同期対応） ---
    private void HandleServerJumped()
    {
        if (Object != null && Object.HasStateAuthority)
        {
            RPC_PlayJumpSound();
        }
        else
        {
            PlayJumpSound();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayJumpSound()
    {
        PlayJumpSound();
    }

    private void HandleServerShot()
    {
        if (Object != null && Object.HasStateAuthority)
        {
            RPC_PlayFireSound();
        }
        else
        {
            PlayFireSound();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayFireSound()
    {
        PlayFireSound();
    }

    private void HandleServerReloadStarted()
    {
        if (Object != null && Object.HasStateAuthority)
        {
            RPC_PlayReloadingSound();
        }
        else
        {
            PlayReloadingSound();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayReloadingSound()
    {
        PlayReloadingSound();
    }

    private void HandleServerReloadCanceled()
    {
        if (Object != null && Object.HasStateAuthority)
        {
            RPC_StopReloadingSound();
        }
        else
        {
            StopReloadingSound();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_StopReloadingSound()
    {
        StopReloadingSound();
    }

    private void HandleServerReloaded()
    {
        if (Object != null && Object.HasStateAuthority)
        {
            RPC_PlayReloadSound();
        }
        else
        {
            PlayReloadSound();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayReloadSound()
    {
        PlayReloadSound();
    }

    private void PlayJumpSound()
    {
        if (jumpSE != null && audioSource != null)
        {
            audioSource.PlayOneShot(jumpSE);
        }
    }

    private void PlayFireSound()
    {
        if (fireSE != null && audioSource != null)
        {
            audioSource.PlayOneShot(fireSE);
        }
    }

    private void PlayReloadingSound()
    {
        if (reloadingSE != null && reloadAudioSource != null)
        {
            reloadAudioSource.pitch = Random.Range(0.9f, 1.1f);
            reloadAudioSource.volume = Random.Range(1.2f, 1.3f);
            reloadAudioSource.clip = reloadingSE;
            reloadAudioSource.loop = true;
            reloadAudioSource.time = Random.Range(0f, reloadingSE.length);
            reloadAudioSource.Play();
        }
    }

    private void StopReloadingSound()
    {
        if (reloadAudioSource != null && reloadAudioSource.isPlaying)
        {
            reloadAudioSource.Stop();
        }
    }

    private void PlayReloadSound()
    {
        StopReloadingSound();

        if (reloadSE != null && audioSource != null)
        {
            audioSource.PlayOneShot(reloadSE);
        }
    }

    private void PlayDamageSound(float current, float max)
    {
        if (!hasInitializedHealth)
        {
            previousHealth = current;
            hasInitializedHealth = true;
            return;
        }

        bool tookDamage = current < previousHealth;
        previousHealth = current;

        if (!tookDamage)
        {
            return;
        }

        if (tookDamageSE != null && audioSource != null)
        {
            audioSource.PlayOneShot(tookDamageSE);
        }
    }

    private void PlayDiedSound(PlayerHealth target)
    {
        StopReloadingSound();

        if (diedSE != null && audioSource != null)
        {
            audioSource.PlayOneShot(diedSE);
        }
    }
}