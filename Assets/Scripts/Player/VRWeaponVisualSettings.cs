using UnityEngine;

[DisallowMultipleComponent]
public class VRWeaponVisualSettings : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform weaponTransform;
    [Tooltip("Hand grip point. Move this object in the prefab to reposition the grip on the controller.")]
    [SerializeField] private Transform weaponGripParent;

    [Header("Grip Mesh Offset")]
    [Tooltip("Shifts the gun mesh so the handle aligns with Weapon Grip Parent.")]
    [SerializeField] private Vector3 weaponLocalPosition = new Vector3(-0.12f, -0.02f, 0.02f);
    [Tooltip("Rotates the gun mesh relative to the grip point.")]
    [SerializeField] private Vector3 weaponLocalEulerAngles = new Vector3(-51.6f, -363.461f, -87.357f);

    [Header("Size")]
    [SerializeField] private float weaponScale = 1.5f;

    public Transform WeaponTransform => weaponTransform;
    [Header("Desktop Presentation")]
    [SerializeField] private Vector3 desktopWeaponLocalPosition = new Vector3(0.25f, -0.25f, 0.5f);
    [Tooltip("PC表示だけを銃口の前方軸まわりに回転し、モデルの上下を補正します。")]
    [SerializeField] private float desktopWeaponRoll = 180f;
    public Vector3 DesktopWeaponLocalPosition => desktopWeaponLocalPosition;
    public float DesktopWeaponRoll => desktopWeaponRoll;
    public Transform WeaponGripParent => weaponGripParent;
    public float WeaponScale => weaponScale;
    public Vector3 WeaponLocalPosition => weaponLocalPosition;
    public Vector3 WeaponLocalEulerAngles => weaponLocalEulerAngles;
}
