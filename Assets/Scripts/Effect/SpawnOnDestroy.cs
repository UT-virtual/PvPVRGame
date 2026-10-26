using UnityEngine;
/// <summary>
/// ロード済みのシーンでオブジェクトが破棄されたとき、その位置にエフェクトを生成する。
/// 命中判定ではなくOnDestroyを契機とする。
/// </summary>
public class SpawnOnDestroy : MonoBehaviour
{
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private bool matchRotation = false;

    private void OnDestroy()
    {
        // シーンのアンロード中にはエフェクトを追加生成しない。
        if (!gameObject.scene.isLoaded) return;

        if (hitEffectPrefab != null)
        {
            Quaternion rot = matchRotation ? transform.rotation : Quaternion.identity;
            Instantiate(hitEffectPrefab, transform.position, rot);
        }
    }
}