using UnityEngine;
using Fusion;

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
            reloadAudioSource.pitch = Random.Range(0.9f, 1.1f); // 0.95倍 〜 1.05倍
            reloadAudioSource.volume = Random.Range(1.2f, 1.3f); // 85% 〜 100%の音量

            // 2. 音声をセットし、ループ再生を有効にする
            reloadAudioSource.clip = reloadingSE;
            reloadAudioSource.loop = true;

            // 3. 【今回の工夫】再生開始位置をランダムに変更する
            // クリップ全体の長さ（秒）の範囲内で、ランダムなスタート地点を決定
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