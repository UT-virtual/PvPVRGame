using System.Collections.Generic;
using UnityEngine;

/// <summary>射撃演出を端末内だけで再利用する。通信オブジェクトやPrefab本体は変更しない。</summary>
public sealed class LocalParticleEffectPool : MonoBehaviour
{
    private const int MaxIdlePerPrefab = 64;
    private static LocalParticleEffectPool instance;

    private sealed class Bucket
    {
        public readonly Stack<Effect> Idle = new();
        public bool Supported;
    }

    private sealed class Effect
    {
        public GameObject Root;
        public ParticleSystem Particles;
        public Bucket Bucket;
    }

    private readonly Dictionary<(GameObject prefab, bool muzzle), Bucket> buckets = new();
    private readonly List<Effect> active = new();
    private Transform idleRoot;

    // 対象外の演出は呼び出し元の従来処理へ戻し、独自スクリプトの寿命管理を妨げない。
    public static bool TryPlay(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, bool muzzle)
    {
        if (prefab == null) return false;
        if (instance == null)
        {
            GameObject root = new("Local Shot Effects");
            instance = root.AddComponent<LocalParticleEffectPool>();
            GameObject idle = new("Inactive Effects");
            idle.transform.SetParent(root.transform, false);
            idle.SetActive(false);
            instance.idleRoot = idle.transform;
        }
        return instance.Play(prefab, position, rotation, scale, muzzle);
    }

    private bool Play(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, bool muzzle)
    {
        var key = (prefab, muzzle);
        if (!buckets.TryGetValue(key, out Bucket bucket))
        {
            bucket = new Bucket { Supported = CanPool(prefab, muzzle) };
            buckets.Add(key, bucket);
        }
        if (!bucket.Supported) return false;

        Effect effect = null;
        while (bucket.Idle.Count > 0 && effect == null)
        {
            Effect candidate = bucket.Idle.Pop();
            if (candidate.Root != null) effect = candidate;
        }
        if (effect == null)
        {
            // 非表示の親の下で初期化し、playOnAwakeによる再生・自己破棄より先に設定する。
            GameObject clone = Instantiate(prefab, idleRoot, false);
            ParticleSystem[] particles = clone.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in particles)
            {
                ParticleSystem.MainModule main = particle.main;
                main.stopAction = ParticleSystemStopAction.None;
                main.playOnAwake = false;
            }
            effect = new Effect { Root = clone, Particles = clone.GetComponent<ParticleSystem>(), Bucket = bucket };
            if (muzzle)
            {
                ParticleSystem.MainModule main = effect.Particles.main;
                main.loop = false;
            }
        }

        effect.Root.SetActive(false);
        effect.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.Root.transform.SetParent(transform, false);
        effect.Root.transform.SetPositionAndRotation(position, rotation);
        effect.Root.transform.localScale = scale;
        effect.Root.SetActive(true);
        effect.Particles.Play(true);
        active.Add(effect);
        return true;
    }

    private static bool CanPool(GameObject prefab, bool muzzle)
    {
        ParticleSystem root = prefab.GetComponent<ParticleSystem>();
        if (!prefab.activeSelf || root == null || prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0) return false;
        foreach (ParticleSystem particle in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            // 発射光のルートだけは、元の射撃処理と同じく非ループ化できる。
            if (particle.main.loop && !(muzzle && particle == root)) return false;
        }
        return true;
    }

    private void Update()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Effect effect = active[i];
            if (effect.Root != null && effect.Particles != null && effect.Particles.IsAlive(true)) continue;
            active.RemoveAt(i);
            if (effect.Root == null) continue;

            effect.Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Root.SetActive(false);
            effect.Root.transform.SetParent(idleRoot, false);
            if (effect.Bucket.Idle.Count < MaxIdlePerPrefab) effect.Bucket.Idle.Push(effect);
            else Destroy(effect.Root);
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
