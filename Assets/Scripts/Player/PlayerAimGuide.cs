using UnityEngine;
using UnityEngine.UI;

// Read the muzzle after VRBodyTargetSync has applied this frame's controller pose.
[DefaultExecutionOrder(1000)]
public sealed class PlayerAimGuide : MonoBehaviour
{
    private const byte AimOpacity = 128;
    private static readonly Color32 DefaultColor = new Color32(127, 255, 212, AimOpacity);
    private static readonly Color32 TargetColor = new Color32(255, 32, 32, AimOpacity);

    private PlayerWeapon weapon;
    private PlayerHealth health;
    private PlayerController controller;
    private LineRenderer laser;
    private Material laserMaterial;
    private Texture2D laserEdgeTexture;
    private LineRenderer shotLaser;
    private Material shotMaterial;
    private float shotUntil;

    public void ShowShot(Vector3 origin, Vector3 endpoint)
    {
        if (IsSpectating()) return;
        if (laser == null) CreateLaser();
        if (shotLaser == null)
        {
            GameObject root = new GameObject("Shot Laser");
            root.transform.SetParent(transform, false);
            shotLaser = root.AddComponent<LineRenderer>();
            shotLaser.useWorldSpace = true;
            shotLaser.positionCount = 2;
            shotLaser.startWidth = shotLaser.endWidth = 0.025f;
            shotLaser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shotLaser.receiveShadows = false;
            shotMaterial = new Material(Graphic.defaultGraphicMaterial);
            shotMaterial.mainTexture = laserEdgeTexture;
            shotMaterial.color = new Color(0.5f, 1f, 0.85f, 0.9f);
            shotLaser.sharedMaterial = shotMaterial;
        }
        shotLaser.SetPosition(0, origin);
        shotLaser.SetPosition(1, endpoint);
        shotUntil = Time.unscaledTime + 0.08f;
        shotLaser.enabled = true;
    }

    private void Awake()
    {
        weapon = GetComponent<PlayerWeapon>();
        health = GetComponent<PlayerHealth>();
        controller = GetComponent<PlayerController>();
    }

    private void LateUpdate()
    {
        if (shotLaser != null) shotLaser.enabled = Time.unscaledTime < shotUntil && !IsSpectating();
        RoundManager round = RoundManager.Instance;
        SpectatorCameraController spectator = SpectatorCameraController.Instance;
        bool watching = spectator != null && spectator.IsSpectatingActive;
        bool isViewedPlayer = watching
            ? spectator.CurrentSpectatorTarget == health
            : health != null && health.Object != null && health.Object.IsValid && health.Object.HasInputAuthority;
        bool visible = weapon != null && health != null && health.Object != null &&
            health.Object.IsValid && isViewedPlayer && !health.IsDead &&
            (round == null || round.CanUseWeapons);
        if (!visible)
        {
            Hide();
            return;
        }

        Transform muzzle = weapon.MuzzleTransform;
        if (muzzle == null)
        {
            Hide();
            return;
        }

        if (laser == null) CreateLaser();
        // 観戦端末のモードではなく、観戦対象自身のPC／VR入力モードで照準する。
        bool vr = watching && controller != null ? (bool)controller.IsCurrentInputVR : LocalPlayModeController.IsVirtualReality;
        Vector3 endpoint = weapon.GetAimEndpoint(vr,
            out bool isTarget);
        Color32 color = isTarget ? TargetColor : DefaultColor;
        laser.SetPosition(0, muzzle.position);
        laser.SetPosition(1, endpoint);
        laserMaterial.color = color;
        laser.enabled = true;
    }

    private void CreateLaser()
    {
        GameObject root = new GameObject("Muzzle Aim Laser");
        root.transform.SetParent(transform, false);
        laser = root.AddComponent<LineRenderer>();
        laser.useWorldSpace = true;
        laser.positionCount = 2;
        laser.textureMode = LineTextureMode.Stretch;
        laser.startWidth = 0.050f;
        laser.endWidth = 0.050f;
        laser.numCapVertices = 0;
        laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        laser.receiveShadows = false;
        laserMaterial = new Material(Graphic.defaultGraphicMaterial);
        laserEdgeTexture = CreateLaserEdgeTexture();
        laserMaterial.mainTexture = laserEdgeTexture;
        laser.sharedMaterial = laserMaterial;
    }

    private static Texture2D CreateLaserEdgeTexture()
    {
        const int samples = 32;
        const float featherWidth = 0.25f;
        Texture2D texture = new Texture2D(1, samples, TextureFormat.RGBA32, false)
        {
            name = "Laser Edge Fade",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color32[] pixels = new Color32[samples];
        for (int i = 0; i < samples; i++)
        {
            // The texture's vertical coordinate runs across the width of the line.
            float across = (float)i / (samples - 1);
            float fromEdge = Mathf.Min(across, 1f - across);
            float alpha = Mathf.SmoothStep(0f, 1f, fromEdge / featherWidth);
            pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
        }
        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        return texture;
    }

    private void Hide()
    {
        if (laser != null) laser.enabled = false;
    }

    private static bool IsSpectating()
    {
        NetworkLauncher launcher = NetworkLauncher.Instance;
        SpectatorCameraController spectator = SpectatorCameraController.Instance;
        return (launcher != null && launcher.IsDedicatedSpectator) ||
            (spectator != null && spectator.IsSpectatingActive);
    }

    private void OnDisable()
    {
        Hide();
        if (shotLaser != null) shotLaser.enabled = false;
        shotUntil = 0f;
    }

    private void OnDestroy()
    {
        if (laserMaterial != null) Destroy(laserMaterial);
        if (shotMaterial != null) Destroy(shotMaterial);
        if (laserEdgeTexture != null) Destroy(laserEdgeTexture);
    }
}
