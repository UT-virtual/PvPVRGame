using UnityEngine;
using Fusion;

/// <summary>
/// 移動・武器・HPのローカルイベントを購読して効果音を再生する。
/// 独自の音声通知RPCは持たず、装填中のループ音は専用AudioSourceで管理する。
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

    private PlayerController playerController;
    private PlayerMove playerMove;
    private PlayerWeapon playerWeapon;
    private PlayerHealth playerHealth;
    private AudioSource audioSource;
    private AudioSource reloadAudioSource;

    private bool hasInitializedHealth;
    private float previousHealth;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
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
            playerMove.OnJumped += PlayJumpSound;
        }

        if (playerWeapon != null)
        {
            playerWeapon.OnShot += PlayFireSound;
            playerWeapon.OnReloadStarted += PlayReloadingSound; 
            playerWeapon.OnReloadCanceled += StopReloadingSound;
            playerWeapon.OnReloaded += PlayReloadSound;
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += PlayDamageSound;
            playerHealth.OnDied += PlayDiedSound;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (playerMove != null)
        {
            playerMove.OnJumped -= PlayJumpSound;
        }

        if (playerWeapon != null)
        {
            playerWeapon.OnShot -= PlayFireSound;
            playerWeapon.OnReloadStarted -= PlayReloadingSound;
            playerWeapon.OnReloadCanceled += StopReloadingSound;
            playerWeapon.OnReloaded -= PlayReloadSound;
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= PlayDamageSound;
            playerHealth.OnDied -= PlayDiedSound;
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
        if (diedSE != null && audioSource != null)
        {
            audioSource.PlayOneShot(diedSE);
        }
    }
}