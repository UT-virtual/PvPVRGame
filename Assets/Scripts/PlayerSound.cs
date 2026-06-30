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
    [SerializeField] private AudioClip reloadSE;
    [SerializeField] private AudioClip tookDamageSE;
    [SerializeField] private AudioClip diedSE;

    private PlayerController playerController;
    private PlayerMove playerMove;
    private PlayerWeapon playerWeapon;
    private PlayerHealth playerHealth;
    private AudioSource audioSource;

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

    private void PlayReloadSound()
    {
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