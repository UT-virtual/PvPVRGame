using UnityEngine;
using Fusion;

/// <summary>
/// StateAuthorityの移動・武器・死亡イベントをRPCで全端末へ通知する。
/// 被弾音は各端末のHP変化から再生し、装填ループは同期された装填状態でも復元する。
/// </summary>
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerSound : NetworkBehaviour
{
    [Header("サウンド設定（インスペクターで割り当ててください）")]
    [SerializeField] private AudioClip jumpSE;
    [SerializeField] private AudioClip fireSE;
    [SerializeField] private AudioClip reloadingSE;
    [SerializeField] private AudioClip reloadSE;
    [SerializeField] private AudioClip tookDamageSE;
    [SerializeField] private AudioClip diedSE;

    private PlayerMove playerMove;
    private PlayerWeapon playerWeapon;
    private PlayerHealth playerHealth;
    private AudioSource audioSource;
    private AudioSource reloadAudioSource;

    private bool hasInitializedHealth;
    private float previousHealth;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        playerMove = GetComponent<PlayerMove>();
        playerWeapon = GetComponent<PlayerWeapon>();
        playerHealth = GetComponent<PlayerHealth>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
        }

        // 装填のループだけを停止できるよう、単発効果音とはAudioSourceを分ける。
        reloadAudioSource = gameObject.AddComponent<AudioSource>();
        reloadAudioSource.playOnAwake = false;
        // メインのAudioSourceと同じ3D設定（空間音響）を引き継ぐ
        reloadAudioSource.spatialBlend = audioSource != null ? audioSource.spatialBlend : 1f;
    }

    public override void Spawned()
    {
        hasInitializedHealth = true;

        if (playerHealth != null)
        {
            previousHealth = playerHealth.CurrentHealth;
        }

        if (playerMove != null)
        {
            playerMove.OnJumped += NotifyJumpSound;
        }

        if (playerWeapon != null)
        {
            playerWeapon.OnShot += NotifyFireSound;
            playerWeapon.OnReloadStarted += NotifyReloadStartedSound;
            playerWeapon.OnReloadCanceled += NotifyReloadStopped;
            playerWeapon.OnReloaded += NotifyReloadStopped;
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += PlayDamageSound;
            playerHealth.OnDied += NotifyDiedSound;
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
            playerWeapon.OnReloadCanceled -= NotifyReloadStopped;
            playerWeapon.OnReloaded -= NotifyReloadStopped;
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= PlayDamageSound;
            playerHealth.OnDied -= NotifyDiedSound;
        }
    }

    private enum SoundEvent : byte
    {
        Jump,
        Fire,
        ReloadStarted,
        ReloadStopped,
        Died
    }

    private void NotifyJumpSound() => NotifySound(SoundEvent.Jump);
    private void NotifyFireSound() => NotifySound(SoundEvent.Fire);
    private void NotifyReloadStartedSound() => NotifySound(SoundEvent.ReloadStarted);
    private void NotifyReloadStopped() => NotifySound(SoundEvent.ReloadStopped);
    private void NotifyDiedSound(PlayerHealth target) => NotifySound(SoundEvent.Died);

    private void NotifySound(SoundEvent soundEvent)
    {
        if (Object != null && Object.IsValid && Object.HasStateAuthority)
        {
            // InputAuthority側では重ねて再生せず、送信元を含む全端末でRPCを1回再生する。
            RPC_PlaySound(soundEvent);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlaySound(SoundEvent soundEvent)
    {
        switch (soundEvent)
        {
            case SoundEvent.Jump: PlayJumpSound(); break;
            case SoundEvent.Fire: PlayFireSound(); break;
            case SoundEvent.ReloadStarted: PlayReloadStartedSound(); break;
            case SoundEvent.ReloadStopped: StopReloadingSound(); break;
            case SoundEvent.Died:
                StopReloadingSound();
                PlayDiedSound(playerHealth);
                break;
        }
    }

    public override void Render()
    {
        if (playerWeapon == null || reloadAudioSource == null)
        {
            return;
        }

        // 途中参加では過去の開始RPCが届かないため、ループだけを同期状態から復元する。
        if (playerWeapon.IsReloading && playerHealth != null && !playerHealth.IsDead)
        {
            if (!reloadAudioSource.isPlaying)
            {
                PlayReloadingSound();
            }
        }
        else
        {
            StopReloadingSound();
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
        if(reloadingSE != null && audioSource != null)
        {
            reloadAudioSource.pitch = Random.Range(0.9f, 1.1f); // 0.9倍 〜 1.1倍
            reloadAudioSource.volume = Random.Range(1.2f, 1.3f); // 120% 〜 130%の音量

            reloadAudioSource.clip = reloadingSE;
            reloadAudioSource.loop = true;

            // 装填音の反復感を減らすため、ループの開始位置を毎回変える。
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

    private void PlayReloadStartedSound()
    {
        StopReloadingSound();

        if (reloadSE != null && audioSource != null)
        {
            audioSource.PlayOneShot(reloadSE);
        }

        PlayReloadingSound();
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
        if (diedSE != null && audioSource != null)
        {
            audioSource.PlayOneShot(diedSE);
        }
    }
}
