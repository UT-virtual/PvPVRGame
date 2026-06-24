using Fusion;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class AmmoBar : MonoBehaviour
{
    public PlayerWeapon weapon;
    [SerializeField] private GameObject cellPrefab;

    private List<Image> cells = new List<Image>();

    void Start()
    {
        weapon = GetComponentInParent<PlayerWeapon>();

        CreateCells();
        UpdateCells(weapon.CurrentAmmo, weapon.MaxAmmo);

        weapon.OnAmmoChanged += UpdateCells;
    
        
    }

    void CreateCells()
    {
        for (int i = 0; i < weapon.MaxAmmo; i++)
        {
            GameObject obj = Instantiate(cellPrefab, transform);
            cells.Add(obj.GetComponent<Image>());
        }
    }

    void UpdateCells(int currentAmmo, int maxAmmo)
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
