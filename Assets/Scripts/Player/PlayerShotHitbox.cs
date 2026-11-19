using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>移動用Colliderとは独立した、PC/VR共通の射撃専用カプセル。</summary>
[RequireComponent(typeof(Hitbox))]
public sealed class PlayerShotHitbox : MonoBehaviour
{
    private static readonly List<PlayerShotHitbox> active = new();
    public static IReadOnlyList<PlayerShotHitbox> Active => active;

    [SerializeField] private Transform scalePivot;
    [SerializeField] private Vector3 normalCenter = new(-0.018f, -0.2f, -0.24f);
    [SerializeField] private float normalHeight = 5.5f;
    [SerializeField] private float normalRadius = 1.1f;

    public PlayerHealth Owner { get; private set; }
    private Hitbox hitbox;
    private HitboxRoot root;
    private Vector3 pivotPosition;
    private float appliedSize = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => active.Clear();

    private void Awake()
    {
        Owner = GetComponentInParent<PlayerHealth>();
        hitbox = GetComponent<Hitbox>();
        root = GetComponentInParent<HitboxRoot>();
        pivotPosition = scalePivot != null ? scalePivot.localPosition : Vector3.zero;
        SetSizeMultiplier(1f);
    }

    private void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
    }

    private void OnDisable() => active.Remove(this);

    public void SetSizeMultiplier(float multiplier)
    {
        if (hitbox == null || root == null || Mathf.Approximately(appliedSize, multiplier)) return;
        appliedSize = multiplier;
        // 見た目の縮小pivotはプレイヤー原点とは異なる。中心位置にも同じ変換を適用する。
        Vector3 center = pivotPosition + (normalCenter - pivotPosition) * multiplier;
        transform.localPosition = center;
        // Fusionの履歴形状はTransform.scaleではなく寸法フィールドから記録される。
        hitbox.CapsuleRadius = normalRadius * multiplier;
        hitbox.CapsuleExtents = normalHeight * multiplier;
        // 現在縮小中でも通常サイズだった履歴を照会する。許可サイズ0.2～2.0の全域を包む球を保つ。
        root.Offset = normalCenter;
        root.BroadRadius = normalHeight + (normalCenter - pivotPosition).magnitude + 0.1f;
    }

    public void SetAlive(bool alive)
    {
        if (root != null && root.Object != null && root.Object.IsValid && root.Object.HasStateAuthority)
            root.HitboxRootActive = alive;
    }

    /// <summary>照準ガイド用の現在位置の判定。PhysX Colliderを増やさず移動・アイテム回収へ干渉しない。</summary>
    public bool RaycastCurrent(Ray ray, float maxDistance, out float distance)
    {
        distance = float.PositiveInfinity;
        if (hitbox == null) return false;
        Vector3 origin = transform.InverseTransformPoint(ray.origin);
        Vector3 direction = transform.InverseTransformVector(ray.direction);
        float radius = hitbox.CapsuleRadius;
        float halfSegment = Mathf.Max(0f, hitbox.CapsuleExtents * 0.5f - radius);
        Vector3 closestAxis = new(0f, Mathf.Clamp(origin.y, -halfSegment, halfSegment), 0f);
        if ((origin - closestAxis).sqrMagnitude <= radius * radius)
        {
            distance = 0f;
            return true;
        }

        float a = direction.x * direction.x + direction.z * direction.z;
        float b = origin.x * direction.x + origin.z * direction.z;
        float c = origin.x * origin.x + origin.z * origin.z - radius * radius;
        float discriminant = b * b - a * c;
        if (a > 0.000001f && discriminant >= 0f)
        {
            float t = (-b - Mathf.Sqrt(discriminant)) / a;
            float y = origin.y + direction.y * t;
            if (t >= 0f && Mathf.Abs(y) <= halfSegment) distance = t;
        }
        TestSphere(origin - Vector3.up * halfSegment, direction, radius, ref distance);
        TestSphere(origin + Vector3.up * halfSegment, direction, radius, ref distance);
        return distance <= maxDistance;
    }

    private static void TestSphere(Vector3 origin, Vector3 direction, float radius, ref float nearest)
    {
        float a = Vector3.Dot(direction, direction);
        if (a < 0.000001f) return;
        float b = Vector3.Dot(origin, direction);
        float discriminant = b * b - a * (Vector3.Dot(origin, origin) - radius * radius);
        if (discriminant < 0f) return;
        float t = (-b - Mathf.Sqrt(discriminant)) / a;
        if (t >= 0f && t < nearest) nearest = t;
    }
}
