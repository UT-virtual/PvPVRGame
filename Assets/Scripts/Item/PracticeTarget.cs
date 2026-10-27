using System.Collections.Generic;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class PracticeTarget : NetworkBehaviour
{
    [Header("Target Stats")]
    [SerializeField] private float maxHealth = 10.0f;
    [SerializeField] private float respawnDelay = 3.0f;
    [SerializeField] private bool activeOnlyInWaitingRoom = true;

    [Header("Visuals & Hit Reaction")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private GameObject destroyEffectPrefab;
    [SerializeField] private AudioClip destroySe;
    [SerializeField] private float hitShakeDuration = 0.12f;
    [SerializeField] private float hitShakeMagnitude = 0.08f;
    [SerializeField] private Color hitFlashColor = new Color(1.0f, 0.4f, 0.4f, 1.0f);

    [Header("Planet Surface Alignment")]
    [SerializeField] private Transform planetCenter;
    [SerializeField] private bool alignToPlanetSurface = true;

    [Header("Movement Settings")]
    [SerializeField] private bool enableMovement = false;
    [SerializeField] private Vector3 localMoveAxis = Vector3.right;
    [SerializeField] private float moveDistance = 3.0f;
    [SerializeField] private float moveSpeed = 2.0f;

    [Networked, OnChangedRender(nameof(OnNetworkedHealthChanged))]
    public float NetworkedCurrentHealth { get; private set; }

    [Networked, OnChangedRender(nameof(OnNetworkedStateChanged))]
    public NetworkBool NetworkedIsDestroyed { get; private set; }

    [Networked, OnChangedRender(nameof(OnNetworkedStateChanged))]
    public NetworkBool NetworkedIsPhaseActive { get; private set; }

    [Networked]
    public NetworkBool NetworkedEnableMovement { get; private set; }

    [Networked]
    private Vector3 NetworkedPosition { get; set; }

    [Networked]
    private Quaternion NetworkedRotation { get; set; }

    [Networked]
    private TickTimer RespawnTimer { get; set; }

    public bool CanReceiveDamage =>
        Object != null &&
        NetworkedIsPhaseActive &&
        !NetworkedIsDestroyed &&
        NetworkedCurrentHealth > 0.0f;

    private Renderer[] renderers;
    private Collider[] colliders;
    private AudioSource audioSource;
    private readonly Dictionary<Renderer, Color[]> originalColors = new();

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 visualInitialLocalPos;
    private float hitReactionTimer;
    private float moveTime;
    private float previousHealth;

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        visualInitialLocalPos = visualRoot.localPosition;
        renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        audioSource = GetComponent<AudioSource>();

        CacheOriginalColors();
    }

    public void InitializeFromSpawner(
        Transform targetPlanetCenter,
        bool isMovingTarget,
        float targetMoveDistance,
        float targetMoveSpeed,
        Vector3 moveAxis,
        float randomPhaseOffset
    )
    {
        planetCenter = targetPlanetCenter;
        enableMovement = isMovingTarget;
        moveDistance = targetMoveDistance;
        moveSpeed = targetMoveSpeed;
        localMoveAxis = moveAxis.sqrMagnitude > 0.001f ? moveAxis.normalized : Vector3.right;
        moveTime = randomPhaseOffset;

        if (alignToPlanetSurface && planetCenter != null)
        {
            AlignToSurface();
        }

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (Object != null && Object.HasStateAuthority)
        {
            NetworkedEnableMovement = enableMovement;
            NetworkedPosition = transform.position;
            NetworkedRotation = transform.rotation;
        }
    }

    public override void Spawned()
    {
        if (alignToPlanetSurface && planetCenter != null)
        {
            AlignToSurface();
        }

        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (Object.HasStateAuthority)
        {
            NetworkedCurrentHealth = maxHealth;
            NetworkedIsDestroyed = false;
            NetworkedIsPhaseActive = ShouldBeActiveInCurrentPhase();
            NetworkedEnableMovement = enableMovement;
            NetworkedPosition = transform.position;
            NetworkedRotation = transform.rotation;
            RespawnTimer = TickTimer.None;
        }
        else
        {
            transform.SetPositionAndRotation(NetworkedPosition, NetworkedRotation);
        }

        previousHealth = NetworkedCurrentHealth;
        ApplyVisibility();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        bool shouldBeActive = ShouldBeActiveInCurrentPhase();
        if (NetworkedIsPhaseActive != shouldBeActive)
        {
            NetworkedIsPhaseActive = shouldBeActive;

            if (shouldBeActive)
            {
                ResetTargetOnServer();
            }
        }

        if (!NetworkedIsPhaseActive)
        {
            return;
        }

        if (NetworkedIsDestroyed)
        {
            if (RespawnTimer.Expired(Runner))
            {
                ResetTargetOnServer();
            }
            return;
        }

        if (NetworkedEnableMovement && moveDistance > 0.01f && moveSpeed > 0.01f)
        {
            moveTime += Runner.DeltaTime * moveSpeed;
            float offset = Mathf.Sin(moveTime) * moveDistance;

            Vector3 moveDir = (initialRotation * localMoveAxis).normalized;
            Vector3 targetPos = initialPosition + moveDir * offset;
            Quaternion targetRot = initialRotation;

            if (alignToPlanetSurface && planetCenter != null)
            {
                float radius = Vector3.Distance(initialPosition, planetCenter.position);
                Vector3 surfaceUp = (targetPos - planetCenter.position).normalized;
                targetPos = planetCenter.position + surfaceUp * radius;

                Vector3 forward = Vector3.ProjectOnPlane(initialRotation * Vector3.forward, surfaceUp).normalized;
                if (forward.sqrMagnitude > 0.001f)
                {
                    targetRot = Quaternion.LookRotation(forward, surfaceUp);
                }
            }

            transform.SetPositionAndRotation(targetPos, targetRot);
            NetworkedPosition = targetPos;
            NetworkedRotation = targetRot;
        }
    }

    public override void Render()
    {
        if (Object != null && !Object.HasStateAuthority && NetworkedEnableMovement)
        {
            transform.SetPositionAndRotation(NetworkedPosition, NetworkedRotation);
        }
    }

    private void Update()
    {
        if (hitReactionTimer > 0.0f)
        {
            hitReactionTimer -= Time.deltaTime;

            if (visualRoot != null && visualRoot != transform)
            {
                visualRoot.localPosition =
                    visualInitialLocalPos + Random.insideUnitSphere * hitShakeMagnitude;
            }

            if (hitReactionTimer <= 0.0f)
            {
                RestoreVisualReaction();
            }
        }
    }

    public void TakeDamage(float damage)
    {
        if (Object == null || !Object.HasStateAuthority)
        {
            return;
        }

        if (!CanReceiveDamage || damage <= 0.0f)
        {
            return;
        }

        NetworkedCurrentHealth = Mathf.Max(0.0f, NetworkedCurrentHealth - damage);

        if (NetworkedCurrentHealth <= 0.0f)
        {
            NetworkedIsDestroyed = true;
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);
            RPC_PlayDestroyFeedback(transform.position, transform.rotation);
        }
    }

    private void ResetTargetOnServer()
    {
        NetworkedCurrentHealth = maxHealth;
        NetworkedIsDestroyed = false;
        RespawnTimer = TickTimer.None;
    }

    private bool ShouldBeActiveInCurrentPhase()
    {
        if (!activeOnlyInWaitingRoom)
        {
            return true;
        }

        if (RoundManager.Instance == null || !RoundManager.Instance.IsNetworkReady)
        {
            return true;
        }

        return RoundManager.Instance.IsWaitingForReady;
    }

    private void OnNetworkedHealthChanged()
    {
        if (NetworkedCurrentHealth < previousHealth && NetworkedCurrentHealth > 0.0f)
        {
            TriggerHitReaction();
        }
        previousHealth = NetworkedCurrentHealth;
    }

    private void OnNetworkedStateChanged()
    {
        ApplyVisibility();
    }

    private void TriggerHitReaction()
    {
        hitReactionTimer = hitShakeDuration;
        ApplyHitFlashColor();
    }

    private void RestoreVisualReaction()
    {
        if (visualRoot != null && visualRoot != transform)
        {
            visualRoot.localPosition = visualInitialLocalPos;
        }
        RestoreOriginalColors();
    }

    private void ApplyVisibility()
    {
        bool visible = NetworkedIsPhaseActive && !NetworkedIsDestroyed;

        if (!visible)
        {
            RestoreVisualReaction();
        }

        if (renderers != null)
        {
            foreach (Renderer r in renderers)
            {
                if (r != null)
                {
                    r.enabled = visible;
                }
            }
        }

        if (colliders != null)
        {
            foreach (Collider c in colliders)
            {
                if (c != null)
                {
                    c.enabled = visible;
                }
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayDestroyFeedback(Vector3 position, Quaternion rotation)
    {
        if (destroyEffectPrefab != null)
        {
            Instantiate(destroyEffectPrefab, position, rotation);
        }

        if (destroySe != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(destroySe);
            }
            else
            {
                AudioSource.PlayClipAtPoint(destroySe, position);
            }
        }
    }

    private void AlignToSurface()
    {
        if (planetCenter == null)
        {
            return;
        }

        Vector3 surfaceUp = (transform.position - planetCenter.position);
        if (surfaceUp.sqrMagnitude < 0.001f)
        {
            return;
        }
        surfaceUp.Normalize();

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, surfaceUp);
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.ProjectOnPlane(Vector3.forward, surfaceUp);
        }
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.Cross(Vector3.right, surfaceUp);
        }
        forward.Normalize();

        transform.rotation = Quaternion.LookRotation(forward, surfaceUp);
    }

    private void CacheOriginalColors()
    {
        originalColors.Clear();
        if (renderers == null)
        {
            return;
        }

        foreach (Renderer r in renderers)
        {
            if (r == null)
            {
                continue;
            }

            Material[] mats = r.materials;
            Color[] colors = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].HasProperty("_Color"))
                {
                    colors[i] = mats[i].color;
                }
                else if (mats[i] != null && mats[i].HasProperty("_BaseColor"))
                {
                    colors[i] = mats[i].GetColor("_BaseColor");
                }
            }
            originalColors[r] = colors;
        }
    }

    private void ApplyHitFlashColor()
    {
        foreach (KeyValuePair<Renderer, Color[]> pair in originalColors)
        {
            Renderer r = pair.Key;
            if (r == null)
            {
                continue;
            }

            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null)
                {
                    continue;
                }
                if (mats[i].HasProperty("_Color"))
                {
                    mats[i].color = hitFlashColor;
                }
                if (mats[i].HasProperty("_BaseColor"))
                {
                    mats[i].SetColor("_BaseColor", hitFlashColor);
                }
            }
        }
    }

    private void RestoreOriginalColors()
    {
        foreach (KeyValuePair<Renderer, Color[]> pair in originalColors)
        {
            Renderer r = pair.Key;
            Color[] colors = pair.Value;
            if (r == null || colors == null)
            {
                continue;
            }

            Material[] mats = r.materials;
            int count = Mathf.Min(mats.Length, colors.Length);
            for (int i = 0; i < count; i++)
            {
                if (mats[i] == null)
                {
                    continue;
                }
                if (mats[i].HasProperty("_Color"))
                {
                    mats[i].color = colors[i];
                }
                if (mats[i].HasProperty("_BaseColor"))
                {
                    mats[i].SetColor("_BaseColor", colors[i]);
                }
            }
        }
    }
}