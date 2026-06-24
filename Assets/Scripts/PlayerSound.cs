using UnityEngine;
using System;
using Fusion;

// アタッチし忘れを防ぐ便利な記述です
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(AudioSource))]
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
    private AudioSource audioSource;

    private void Awake()
    {
        // コンポーネントを取得
        playerController = GetComponent<PlayerController>();
        audioSource = GetComponent<AudioSource>();
        playerMove = GetComponent<PlayerMove>();
        playerWeapon = GetComponent<PlayerWeapon>();

    }


    public override void Spawned()
    {
        // イベントの登録
        if (playerController != null)
        {
            playerMove.OnJumped += PlayJumpSound;
            playerWeapon.OnShot += PlayFireSound;
            playerWeapon.OnReloaded += PlayReloadSound;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        //イベントの削除
        if (playerController != null)
        {
            playerMove.OnJumped -= PlayJumpSound;
            playerWeapon.OnShot -= PlayFireSound;
            playerWeapon.OnReloaded -= PlayReloadSound;
        }
    }

    // --- 音を鳴らす処理 ---

    private void PlayJumpSound()
    {
        if (jumpSE != null) audioSource.PlayOneShot(jumpSE);
    }

    private void PlayFireSound()
    {
        if (fireSE != null) audioSource.PlayOneShot(fireSE);
    }

    private void PlayReloadSound()
    {
        if (reloadSE != null) audioSource.PlayOneShot(reloadSE);
    }
}