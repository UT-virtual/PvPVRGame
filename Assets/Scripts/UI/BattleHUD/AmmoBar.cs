using Fusion;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class AmmoBar : MonoBehaviour
{
    [SerializeField] private PlayerWeapon weapon;
    [SerializeField] private GameObject cellPrefab;

    private readonly List<Image> cells = new();
    private Coroutine findWeaponCoroutine;

    private void OnEnable()
    {
        StartFindLocalWeapon();
    }

    private void OnDisable()
    {
        if (findWeaponCoroutine != null)
        {
            StopCoroutine(findWeaponCoroutine);
            findWeaponCoroutine = null;
        }

        if (weapon != null)
        {
            weapon.OnAmmoChanged -= UpdateCells;
        }
    }

    private void OnDestroy()
    {
        if (weapon != null)
        {
            weapon.OnAmmoChanged -= UpdateCells;
        }
    }

    private void StartFindLocalWeapon()
    {
        if (findWeaponCoroutine != null)
        {
            StopCoroutine(findWeaponCoroutine);
        }

        if (weapon != null)
        {
            weapon.OnAmmoChanged -= UpdateCells;
            weapon = null;
        }

        ClearCells();

        findWeaponCoroutine = StartCoroutine(FindLocalWeapon());
    }

    private IEnumerator FindLocalWeapon()
    {
        while (weapon == null)
        {
            PlayerWeapon[] weapons = FindObjectsByType<PlayerWeapon>(
                FindObjectsSortMode.None
            );

            Debug.Log($"[AmmoBar] Searching local weapon. Count={weapons.Length}");

            foreach (PlayerWeapon candidate in weapons)
            {
                if (candidate == null)
                {
                    continue;
                }

                NetworkObject networkObject = candidate.GetComponent<NetworkObject>();

                Debug.Log(
                    $"[AmmoBar] Candidate={candidate.name}, " +
                    $"HasNetworkObject={networkObject != null}, " +
                    $"HasInputAuthority={(networkObject != null && networkObject.HasInputAuthority)}"
                );

                if (networkObject != null && networkObject.HasInputAuthority)
                {
                    Debug.Log($"[AmmoBar] Local weapon found: {candidate.name}");

                    SetupWeapon(candidate);
                    findWeaponCoroutine = null;
                    yield break;
                }
            }

            yield return null;
        }

        findWeaponCoroutine = null;
    }

    public void SetupWeapon(PlayerWeapon targetWeapon)
    {
        if (targetWeapon == null)
        {
            Debug.LogWarning("[AmmoBar] SetupWeapon failed. targetWeapon is null.");
            return;
        }

        NetworkObject networkObject = targetWeapon.GetComponent<NetworkObject>();

        if (networkObject == null || !networkObject.IsValid)
        {
            StartFindLocalWeapon();
            return;
        }

        if (weapon != null)
        {
            weapon.OnAmmoChanged -= UpdateCells;
        }

        weapon = targetWeapon;

        ClearCells();
        CreateCells();

        weapon.OnAmmoChanged -= UpdateCells;
        weapon.OnAmmoChanged += UpdateCells;

        UpdateCells(weapon.CurrentAmmo, weapon.MaxAmmo);

        Debug.Log(
            $"[AmmoBar] Setup complete. " +
            $"Target={weapon.name}, " +
            $"HasInputAuthority={networkObject.HasInputAuthority}, " +
            $"InputAuthority={networkObject.InputAuthority}, " +
            $"Ammo={weapon.CurrentAmmo}/{weapon.MaxAmmo}, " +
            $"Cells={cells.Count}"
        );
    }

    private void ClearCells()
    {
        foreach (Image cell in cells)
        {
            if (cell != null)
            {
                Destroy(cell.gameObject);
            }
        }

        cells.Clear();
    }

    private void CreateCells()
    {
        if (weapon == null)
        {
            Debug.LogWarning("[AmmoBar] CreateCells failed. weapon is null.");
            return;
        }

        if (cellPrefab == null)
        {
            Debug.LogWarning("[AmmoBar] CreateCells failed. cellPrefab is null.");
            return;
        }

        Debug.Log($"[AmmoBar] CreateCells. MaxAmmo={weapon.MaxAmmo}");

        for (int i = 0; i < weapon.MaxAmmo; i++)
        {
            GameObject obj = Instantiate(cellPrefab, transform);
            Image image = obj.GetComponent<Image>();

            if (image == null)
            {
                Debug.LogWarning("[AmmoBar] Cell prefab has no Image component.");
                continue;
            }

            cells.Add(image);
        }
    }

    private void UpdateCells(int currentAmmo, int maxAmmo)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            int indexFromRight = cells.Count - 1 - i;
            bool isActive = i < currentAmmo;

            cells[indexFromRight].color = isActive
                ? Color.white
                : new Color(1, 1, 1, 0.2f);
        }
    }
}