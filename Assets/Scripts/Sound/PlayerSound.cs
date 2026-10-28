using Fusion;
using UnityEngine;

/// <summary>
/// 移動・武器・死亡の効果音を全端末で再生し、着弾・スキルの効果音は本人だけに通知する。
/// 装填ループとラウンド撃破通知は、同期状態の変化からも復元する。
/// </summary>
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

    private PlayerMove playerMove;
    private PlayerWeapon playerWeapon;
    private PlayerHealth playerHealth;
    private PlayerSkillController playerSkillController;

    private AudioSource audioSource;
    private AudioSource reloadAudioSource;

    private bool hasInitializedHealth;
    private float previousHealth;
    private bool wasDead;

    private enum SoundEvent : byte
    {
        Jump,
        Fire,
        ReloadStarted,
        ReloadCanceled,
        ReloadCompleted,
        Died
    }

    private void Awake()
    {
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

        // 装填のループだけを止められるよう、単発効果音とAudioSourceを分ける。
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
            playerMove.OnJumped += NotifyJumpSound;
        }

        if (playerWeapon != null)
        {
            playerWeapon.OnShot += NotifyFireSound;
            playerWeapon.OnReloadStarted += NotifyReloadStartedSound;
            playerWeapon.OnReloadCanceled += NotifyReloadCanceledSound;
            playerWeapon.OnReloaded += NotifyReloadCompletedSound;
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += PlayDamageSound;
            playerHealth.OnDied += NotifyDiedSound;
        }

        if (playerSkillController != null)
        {
            playerSkillController.OnSkillActivated += HandleServerSkillActivated;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        StopReloadingSound();

        if (playerMove != null)
        {
            playerMove.OnJumped -= NotifyJumpSound;
        }

        if (playerWeapon != null)
        {
            playerWeapon.OnShot -= NotifyFireSound;
            playerWeapon.OnReloadStarted -= NotifyReloadStartedSound;
            playerWeapon.OnReloadCanceled -= NotifyReloadCanceledSound;
            playerWeapon.OnReloaded -= NotifyReloadCompletedSound;
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= PlayDamageSound;
            playerHealth.OnDied -= NotifyDiedSound;
        }

        if (playerSkillController != null)
        {
            playerSkillController.OnSkillActivated -= HandleServerSkillActivated;
        }
    }

    public override void Render()
    {
        UpdateReloadLoop();
        NotifyRoundSeOnDeath();
    }

    private void UpdateReloadLoop()
    {
        if (playerWeapon == null || reloadAudioSource == null)
        {
            return;
        }

        bool isPlayerAlive = playerHealth != null && !playerHealth.IsDead;
        if (playerWeapon.IsReloading && isPlayerAlive)
        {
            // 途中参加では過去のRPCを受信できないため、同期状態から装填ループを復元する。
            if (!reloadAudioSource.isPlaying)
            {
                PlayReloadingSound();
            }
            return;
        }

        StopReloadingSound();
    }

    private void NotifyRoundSeOnDeath()
    {
        if (playerHealth == null || Object == null || !Object.IsValid)
        {
            return;
        }

        bool isCurrentlyDead = playerHealth.IsDead;
        if (isCurrentlyDead == wasDead)
        {
            return;
        }

        wasDead = isCurrentlyDead;
        if (!isCurrentlyDead)
        {
            return;
        }

        // 個別の死亡SEはStateAuthorityからのRPCで再生するため、ここでは重複再生しない。
        if (RoundSeManager.Instance != null)
        {
            RoundSeManager.Instance.NotifyPlayerDefeated(playerHealth);
        }
    }

    private void NotifyJumpSound() => NotifySound(SoundEvent.Jump);
    private void NotifyFireSound() => NotifySound(SoundEvent.Fire);
    private void NotifyReloadStartedSound() => NotifySound(SoundEvent.ReloadStarted);
    private void NotifyReloadCanceledSound() => NotifySound(SoundEvent.ReloadCanceled);
    private void NotifyReloadCompletedSound() => NotifySound(SoundEvent.ReloadCompleted);
    private void NotifyDiedSound(PlayerHealth deadPlayer) => NotifySound(SoundEvent.Died);

    private void NotifySound(SoundEvent soundEvent)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority)
        {
            return;
        }

        RPC_PlaySound(soundEvent);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlaySound(SoundEvent soundEvent)
    {
        switch (soundEvent)
        {
            case SoundEvent.Jump:
                PlayJumpSound();
                break;

            case SoundEvent.Fire:
                PlayFireSound();
                break;

            case SoundEvent.ReloadStarted:
                PlayReloadStartSound();
                PlayReloadingSound();
                break;

            case SoundEvent.ReloadCanceled:
                StopReloadingSound();
                break;

            case SoundEvent.ReloadCompleted:
                StopReloadingSound();
                break;

            case SoundEvent.Died:
                PlayDiedSound();
                break;
        }
    }

    // ProjectileからStateAuthority上で呼ばれ、着弾した本人だけへ送信する。
    public void NotifyProjectileHitFromServer(bool hitEnemy, Vector3 hitPosition)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority)
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

    private void HandleServerSkillActivated(PlayerSkillType skillType)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority)
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
        if (reloadingSE == null || reloadAudioSource == null)
        {
            return;
        }

        reloadAudioSource.pitch = Random.Range(0.9f, 1.1f);
        reloadAudioSource.volume = Random.Range(1.2f, 1.3f);
        reloadAudioSource.clip = reloadingSE;
        reloadAudioSource.loop = true;
        reloadAudioSource.time = Random.Range(0f, reloadingSE.length);
        reloadAudioSource.Play();
    }

    private void StopReloadingSound()
    {
        if (reloadAudioSource != null && reloadAudioSource.isPlaying)
        {
            reloadAudioSource.Stop();
        }
    }

    private void PlayReloadStartSound()
    {
        // 既存の単発SEを開始通知に使い、逐次補充の完了時にはループ停止だけを行う。
        StopReloadingSound();

        if (reloadSE != null && audioSource != null)
        {
            audioSource.PlayOneShot(reloadSE);
        }
    }

    private void PlayDamageSound(float currentHealth, float maxHealth)
    {
        if (!hasInitializedHealth)
        {
            previousHealth = currentHealth;
            hasInitializedHealth = true;
            return;
        }

        bool hasTakenDamage = currentHealth < previousHealth;
        previousHealth = currentHealth;

        if (hasTakenDamage && tookDamageSE != null && audioSource != null)
        {
            audioSource.PlayOneShot(tookDamageSE);
        }
    }

    private void PlayDiedSound()
    {
        StopReloadingSound();

        if (diedSE != null && audioSource != null)
        {
            audioSource.PlayOneShot(diedSE);
        }
    }
}
