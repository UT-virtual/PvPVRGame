using UnityEngine;

public class OutlineDebugKeys : MonoBehaviour
{
    [SerializeField] private PlayerOutlineController target;

    void Update()
    {
        if (target == null) return;

        if (Input.GetKeyDown(KeyCode.X)) target.SetXRay(true);
        if (Input.GetKeyDown(KeyCode.C)) target.SetXRay(false);
        if (Input.GetKeyDown(KeyCode.K)) target.SetDead(true);
        if (Input.GetKeyDown(KeyCode.R)) target.SetDead(false);
    }
}