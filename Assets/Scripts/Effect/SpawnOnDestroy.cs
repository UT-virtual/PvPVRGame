using UnityEngine;
//壊れた時にヒットエフェクトを出すスクリプトです。
public class SpawnOnDestroy : MonoBehaviour
{
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private bool matchRotation = false;

    private void OnDestroy()
    {
        // アプリ終了時・シーン遷移時などの誤爆を防ぐ
        if (!gameObject.scene.isLoaded) return;

        if (hitEffectPrefab != null)
        {
            Quaternion rot = matchRotation ? transform.rotation : Quaternion.identity;
            Instantiate(hitEffectPrefab, transform.position, rot);
        }
    }
}