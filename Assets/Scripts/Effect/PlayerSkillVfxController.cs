using Fusion;
using UnityEngine;

public class PlayerSkillVfxController : NetworkBehaviour
{
    [Header("Skill Activation VFX (発動瞬間)")]
    [Tooltip("スキルを発動した瞬間に1回再生するエフェクトのPrefab")]
    [SerializeField] private GameObject activationVfxPrefab;
    [Tooltip("発動エフェクトのローカルオフセット")]
    [SerializeField] private Vector3 activationLocalOffset = Vector3.zero;
    [Tooltip("発動エフェクトを自動破棄するまでの秒数")]
    [SerializeField] private float activationVfxLifeTime = 2.5f;

    [Header("Skill Active Foot Circle VFX (効果中の足元サークル)")]
    [Tooltip("スキル継続中に足元へ表示し続けるサークルエフェクトのPrefab")]
    [SerializeField] private GameObject footCircleVfxPrefab;
    [Tooltip("足元サークルの追従先（未指定ならプレイヤー本体）")]
    [SerializeField] private Transform footCircleAttachPoint;
    [Tooltip("足元サークルのローカルオフセット（地面に埋まる場合は Y = 0.05 など）")]
    [SerializeField] private Vector3 footCircleLocalOffset = new Vector3(0.0f, 0.05f, 0.0f);
    [Tooltip("停止後、残存パーティクルが消えるのを待って破棄する秒数")]
    [SerializeField] private float footCircleStopDestroyDelay = 1.0f;

    private PlayerSkillController skillController;
    private PlayerHealth playerHealth;

    private PlayerSkillType previousSkill1 = PlayerSkillType.None;
    private PlayerSkillType previousSkill2 = PlayerSkillType.None;

    private GameObject activeFootCircleInstance;
    private ParticleSystem[] activeFootCircleParticles;

    private void Awake()
    {
        skillController = GetComponentInParent<PlayerSkillController>();
        playerHealth = GetComponentInParent<PlayerHealth>();
    }

    public override void Spawned()
    {
        if (skillController != null)
        {
            previousSkill1 = skillController.ActiveSkill;
            previousSkill2 = skillController.ActiveSecondSkill;
        }
    }

    public override void Render()
    {
        if (skillController == null)
        {
            return;
        }

        PlayerSkillType currentSkill1 = skillController.ActiveSkill;
        PlayerSkillType currentSkill2 = skillController.ActiveSecondSkill;
        bool isDead = playerHealth != null && playerHealth.IsDead;

        // 1. スキル発動の瞬間を検知（None以外へ切り替わったタイミング）
        bool activatedSkill1 = currentSkill1 != PlayerSkillType.None && currentSkill1 != previousSkill1;
        bool activatedSkill2 = currentSkill2 != PlayerSkillType.None && currentSkill2 != previousSkill2;

        if (!isDead && (activatedSkill1 || activatedSkill2))
        {
            SpawnActivationVfx();
        }

        previousSkill1 = currentSkill1;
        previousSkill2 = currentSkill2;

        // 2. スキル継続中の足元サークル表示を更新
        bool shouldShowFootCircle =
            !isDead &&
            (currentSkill1 != PlayerSkillType.None || currentSkill2 != PlayerSkillType.None);

        UpdateFootCircleVfx(shouldShowFootCircle);
    }

    private void SpawnActivationVfx()
    {
        if (activationVfxPrefab == null)
        {
            return;
        }

        Transform parent = ResolveAttachPoint();
        GameObject instance = Instantiate(activationVfxPrefab, parent);
        instance.transform.localPosition = activationLocalOffset;
        instance.transform.localRotation = Quaternion.identity;

        if (activationVfxLifeTime > 0.0f)
        {
            Destroy(instance, activationVfxLifeTime);
        }
    }

    private void UpdateFootCircleVfx(bool shouldShow)
    {
        if (shouldShow)
        {
            if (activeFootCircleInstance == null && footCircleVfxPrefab != null)
            {
                Transform parent = ResolveAttachPoint();
                activeFootCircleInstance = Instantiate(footCircleVfxPrefab, parent);
                activeFootCircleInstance.transform.localPosition = footCircleLocalOffset;
                activeFootCircleInstance.transform.localRotation = Quaternion.identity;
                activeFootCircleParticles = activeFootCircleInstance.GetComponentsInChildren<ParticleSystem>(true);
            }

            if (activeFootCircleParticles != null)
            {
                foreach (ParticleSystem ps in activeFootCircleParticles)
                {
                    if (ps != null && !ps.isPlaying)
                    {
                        ps.Play();
                    }
                }
            }
        }
        else if (activeFootCircleInstance != null)
        {
            StopAndReleaseFootCircle();
        }
    }

    private void StopAndReleaseFootCircle()
    {
        if (activeFootCircleParticles != null)
        {
            foreach (ParticleSystem ps in activeFootCircleParticles)
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        Destroy(activeFootCircleInstance, footCircleStopDestroyDelay);
        activeFootCircleInstance = null;
        activeFootCircleParticles = null;
    }

    private Transform ResolveAttachPoint()
    {
        if (footCircleAttachPoint != null)
        {
            return footCircleAttachPoint;
        }

        return skillController != null ? skillController.transform : transform;
    }

    private void OnDisable()
    {
        if (activeFootCircleInstance != null)
        {
            Destroy(activeFootCircleInstance);
            activeFootCircleInstance = null;
            activeFootCircleParticles = null;
        }
    }
}