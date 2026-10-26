using System;
using UnityEngine;

public class PlayerLocalSound : MonoBehaviour
{
    [Serializable]
    public struct SkillSoundOverride
    {
        public PlayerSkillType skillType;
        public SoundCue sound;
    }

    [Header("Skill Activation SE (自分のみ)")]
    [Tooltip("スキル発動時のデフォルトSE（自分のみ）")]
    [SerializeField] private SoundCue defaultSkillActivateSe;
    [Tooltip("特定のスキルごとに個別SEを鳴らしたい場合のオーバーライド設定（任意）")]
    [SerializeField] private SkillSoundOverride[] skillSpecificSeOverrides;

    [Header("Projectile Hit SE (自分のみ)")]
    [Tooltip("自分が発射した弾が『敵』に着弾した時のSE（自分のみ）")]
    [SerializeField] private SoundCue hitEnemySe;
    [Tooltip("自分が発射した弾が『敵以外（壁や床など）』に着弾した時のSE（自分のみ）")]
    [SerializeField] private SoundCue hitEnvironmentSe;
    [Tooltip("ONにすると『敵以外の着弾SE』を着弾座標から3Dサウンドとして再生します（聞こえるのは自分のみ）")]
    [SerializeField] private bool playEnvironmentHitAtImpactPosition = false;
    [SerializeField, Range(0.0f, 1.0f)] private float environmentHitSpatialBlend = 0.8f;

    private AudioSource local2DAudioSource;

    private void Awake()
    {
        // 自分にだけクリアに聞こえる2D専用AudioSourceを生成
        local2DAudioSource = gameObject.AddComponent<AudioSource>();
        local2DAudioSource.playOnAwake = false;
        local2DAudioSource.spatialBlend = 0.0f;
    }

    public void PlaySkillActivatedSe(PlayerSkillType skillType)
    {
        if (skillSpecificSeOverrides != null)
        {
            foreach (SkillSoundOverride entry in skillSpecificSeOverrides)
            {
                if (entry.skillType == skillType && entry.sound != null && entry.sound.IsValid)
                {
                    entry.sound.PlayOneShot(local2DAudioSource);
                    return;
                }
            }
        }

        defaultSkillActivateSe?.PlayOneShot(local2DAudioSource);
    }

    public void PlayProjectileHitSe(bool isEnemyHit, Vector3 hitPosition)
    {
        if (isEnemyHit)
        {
            hitEnemySe?.PlayOneShot(local2DAudioSource);
            return;
        }

        if (hitEnvironmentSe == null || !hitEnvironmentSe.IsValid)
        {
            return;
        }

        if (playEnvironmentHitAtImpactPosition)
        {
            PlayOneShotAtPointForLocalOnly(hitEnvironmentSe, hitPosition, environmentHitSpatialBlend);
        }
        else
        {
            hitEnvironmentSe.PlayOneShot(local2DAudioSource);
        }
    }

    private void PlayOneShotAtPointForLocalOnly(SoundCue cue, Vector3 position, float spatialBlend)
    {
        if (cue == null || !cue.IsValid)
        {
            return;
        }

        GameObject tempAudioObj = new GameObject("LocalHitAudio_Temp");
        tempAudioObj.transform.position = position;

        AudioSource tempSource = tempAudioObj.AddComponent<AudioSource>();
        tempSource.clip = cue.Clip;
        tempSource.volume = cue.Volume;
        tempSource.pitch = cue.GetPitch();
        tempSource.spatialBlend = spatialBlend;
        tempSource.Play();

        Destroy(tempAudioObj, (cue.Clip.length / Mathf.Max(0.1f, tempSource.pitch)) + 0.1f);
    }
}