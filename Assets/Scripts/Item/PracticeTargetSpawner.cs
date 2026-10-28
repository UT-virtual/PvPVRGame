using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class PracticeTargetSpawner : MonoBehaviour
{
    [Header("Target Prefab")]
    [SerializeField] private NetworkPrefabRef practiceTargetPrefab;

    [Header("Count & Ratio (個数と動くデコイの割合)")]
    [Tooltip("惑星全体に配置するデコイの総数")]
    [SerializeField] private int totalTargetCount = 12;
    [Tooltip("動くデコイの割合 (0.25 = 全体の25%が動くデコイになる)")]
    [SerializeField, Range(0.0f, 1.0f)] private float movingTargetRatio = 0.25f;

    [Header("Planet Surface Settings (惑星の接地設定)")]
    [SerializeField] private Transform planetCenter;
    [SerializeField] private LayerMask planetLayer = ~0;
    [Tooltip("レイキャストを開始する惑星中心からの上空距離")]
    [SerializeField] private float raycastStartRadius = 50.0f;
    [SerializeField] private float sphereCastRadius = 0.3f;
    [Tooltip("レイキャストがヒットしなかった場合のデフォルト惑星半径")]
    [SerializeField] private float fallbackPlanetRadius = 15.0f;
    [Tooltip("地表からの高さオフセット")]
    [SerializeField] private float surfaceHeightOffset = 0.0f;

    [Header("Distribution Settings (均等＋ランダム調整)")]
    [Tooltip("均等配置に加えるランダムな角度揺らぎ（度）")]
    [SerializeField, Range(0.0f, 25.0f)] private float jitterAngleDegrees = 10.0f;
    [Tooltip("デコイ同士の最小距離（重なり防止）")]
    [SerializeField] private float minDistanceBetweenTargets = 2.5f;

    [Header("Moving Target Parameters (動くデコイの挙動)")]
    [SerializeField] private float moveDistance = 2.5f;
    [SerializeField] private float moveSpeed = 2.0f;

    private readonly List<NetworkObject> spawnedTargets = new();
    private bool hasSpawnedTargets = false;

    private void Update()
    {
        if (hasSpawnedTargets)
        {
            return;
        }

        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();
        if (runner == null || !runner.IsRunning)
        {
            return;
        }

        if (!runner.IsServer)
        {
            hasSpawnedTargets = true;
            return;
        }

        SpawnAllTargets(runner);
        hasSpawnedTargets = true;
    }

    public void SpawnAllTargets(NetworkRunner runner)
    {
        ClearSpawnedTargets(runner);

        if (!practiceTargetPrefab.IsValid)
        {
            Debug.LogWarning("[PracticeTargetSpawner] practiceTargetPrefab is not assigned.");
            return;
        }

        if (totalTargetCount <= 0)
        {
            return;
        }

        Vector3 center = planetCenter != null ? planetCenter.position : transform.position;

        // 1. 動くデコイ・動かないデコイの内訳を作成してシャッフル（少なめの割合を動くデコイにする）
        int movingCount = Mathf.RoundToInt(totalTargetCount * movingTargetRatio);
        movingCount = Mathf.Clamp(movingCount, 0, totalTargetCount);

        List<bool> movementFlags = new(totalTargetCount);
        for (int i = 0; i < totalTargetCount; i++)
        {
            movementFlags.Add(i < movingCount);
        }
        ShuffleList(movementFlags);

        // 2. フィボナッチ球面分布＋ランダム回転で惑星全体の方向ベクトルを均等生成
        Quaternion randomGlobalRotation = Random.rotationUniform;
        List<Vector3> placedPositions = new(totalTargetCount);

        for (int i = 0; i < totalTargetCount; i++)
        {
            Vector3 baseDir = GetFibonacciSphereDirection(i, totalTargetCount);
            baseDir = randomGlobalRotation * baseDir;

            Vector3 spawnPos = Vector3.zero;
            Vector3 surfaceUp = baseDir;
            bool foundValidSpot = false;

            // 重ならない位置を探す（最大8回リトライ）
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector3 candidateDir = ApplyAngularJitter(baseDir, jitterAngleDegrees);
                if (TryFindSurfacePoint(center, candidateDir, out Vector3 hitPos, out Vector3 hitNormal))
                {
                    spawnPos = hitPos + hitNormal * surfaceHeightOffset;
                    surfaceUp = hitNormal;
                }
                else
                {
                    spawnPos = center + candidateDir * (fallbackPlanetRadius + surfaceHeightOffset);
                    surfaceUp = candidateDir;
                }

                if (!IsTooCloseToExisting(spawnPos, placedPositions, minDistanceBetweenTargets))
                {
                    foundValidSpot = true;
                    break;
                }
            }

            if (!foundValidSpot && placedPositions.Count > 0)
            {
                // 万が一近い場合でも最後の計算位置を採用
            }

            placedPositions.Add(spawnPos);

            // 接平面上でランダムな向き（360度）を向かせる
            Quaternion spawnRot = GetRandomSurfaceRotation(surfaceUp);

            NetworkObject targetObj = runner.Spawn(
                practiceTargetPrefab,
                spawnPos,
                spawnRot,
                PlayerRef.None
            );

            if (targetObj == null)
            {
                continue;
            }

            spawnedTargets.Add(targetObj);

            PracticeTarget practiceTarget = targetObj.GetComponent<PracticeTarget>();
            if (practiceTarget != null)
            {
                bool isMoving = movementFlags[i];
                float randomPhase = Random.Range(0.0f, Mathf.PI * 2.0f);

                practiceTarget.InitializeFromSpawner(
                    planetCenter != null ? planetCenter : transform,
                    isMoving,
                    moveDistance,
                    moveSpeed,
                    Vector3.right,
                    randomPhase
                );
            }
        }

        Debug.Log(
            $"[PracticeTargetSpawner] Spawned {spawnedTargets.Count} targets " +
            $"(Moving: {movingCount}, Static: {totalTargetCount - movingCount})."
        );
    }

    public void ClearSpawnedTargets(NetworkRunner runner)
    {
        if (runner != null && runner.IsServer)
        {
            foreach (NetworkObject obj in spawnedTargets)
            {
                if (obj != null)
                {
                    runner.Despawn(obj);
                }
            }
        }
        spawnedTargets.Clear();
    }

    private Vector3 GetFibonacciSphereDirection(int index, int totalCount)
    {
        if (totalCount <= 1)
        {
            return Vector3.up;
        }

        // 黄金角を用いた球面均等分布
        float goldenRatio = (1.0f + Mathf.Sqrt(5.0f)) * 0.5f;
        float goldenAngle = 2.0f * Mathf.PI * (1.0f - 1.0f / goldenRatio);

        float y = 1.0f - ((index + 0.5f) / totalCount) * 2.0f;
        float radiusAtY = Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - y * y));
        float theta = goldenAngle * index;

        float x = Mathf.Cos(theta) * radiusAtY;
        float z = Mathf.Sin(theta) * radiusAtY;

        return new Vector3(x, y, z).normalized;
    }

    private Vector3 ApplyAngularJitter(Vector3 direction, float maxAngleDegrees)
    {
        if (maxAngleDegrees <= 0.001f)
        {
            return direction;
        }

        Vector3 randomAxis = Random.onUnitSphere;
        float angle = Random.Range(-maxAngleDegrees, maxAngleDegrees);
        return (Quaternion.AngleAxis(angle, randomAxis) * direction).normalized;
    }

    private bool TryFindSurfacePoint(
        Vector3 center,
        Vector3 outwardDir,
        out Vector3 surfacePoint,
        out Vector3 surfaceNormal
    )
    {
        Vector3 rayOrigin = center + outwardDir * raycastStartRadius;
        Vector3 rayDir = -outwardDir;
        float maxDistance = raycastStartRadius * 1.5f;

        if (Physics.SphereCast(
            rayOrigin,
            sphereCastRadius,
            rayDir,
            out RaycastHit hit,
            maxDistance,
            planetLayer,
            QueryTriggerInteraction.Ignore
        ))
        {
            surfacePoint = hit.point;
            surfaceNormal = hit.normal.sqrMagnitude > 0.001f ? hit.normal.normalized : outwardDir;
            return true;
        }

        surfacePoint = Vector3.zero;
        surfaceNormal = outwardDir;
        return false;
    }

    private Quaternion GetRandomSurfaceRotation(Vector3 surfaceUp)
    {
        Vector3 tangent = Vector3.Cross(surfaceUp, Vector3.up);
        if (tangent.sqrMagnitude < 0.001f)
        {
            tangent = Vector3.Cross(surfaceUp, Vector3.right);
        }
        tangent.Normalize();

        float randomYaw = Random.Range(0.0f, 360.0f);
        Vector3 forward = Quaternion.AngleAxis(randomYaw, surfaceUp) * tangent;
        return Quaternion.LookRotation(forward.normalized, surfaceUp);
    }

    private bool IsTooCloseToExisting(Vector3 candidate, List<Vector3> existing, float minDistance)
    {
        float minSqr = minDistance * minDistance;
        foreach (Vector3 pos in existing)
        {
            if ((candidate - pos).sqrMagnitude < minSqr)
            {
                return true;
            }
        }
        return false;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}