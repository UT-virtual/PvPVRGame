using System.Collections;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayer : NetworkBehaviour
{
    [Header("Local Only Components")]
    [SerializeField] private PlayerCamera playerCamera;

    [Header("Local Player Visibility")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private bool hideLocalVisual = true;

    [Header("Local Only Objects")]
    [SerializeField] private GameObject[] localOnlyObjects;

    public override void Spawned()
    {
        bool isLocalPlayer = Object.HasInputAuthority;

        Debug.Log(
            $"NetworkPlayer Spawned. " +
            $"Name={gameObject.name}, " +
            $"InputAuthority={Object.InputAuthority}, " +
            $"RunnerLocalPlayer={Runner.LocalPlayer}, " +
            $"HasInputAuthority={Object.HasInputAuthority}, " +
            $"HasStateAuthority={Object.HasStateAuthority}"
        );

        if (playerCamera != null)
        {
            playerCamera.enabled = isLocalPlayer;
        }

        SetLocalOnlyObjects(isLocalPlayer);
        SetLocalVisual(isLocalPlayer);

        if (isLocalPlayer)
        {
            NetworkLauncher launcher = FindFirstObjectByType<NetworkLauncher>();
            PlayerController playerController = GetComponent<PlayerController>();

            if (launcher != null && playerController != null)
            {
                launcher.RegisterLocalPlayer(playerController);
            }

            StartCoroutine(SetupLocalPlayerAfterSpawn());
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (!Object.HasInputAuthority)
        {
            return;
        }

        NetworkLauncher launcher = FindFirstObjectByType<NetworkLauncher>();
        PlayerController playerController = GetComponent<PlayerController>();

        if (launcher != null && playerController != null)
        {
            launcher.UnregisterLocalPlayer(playerController);
        }
    }

    private IEnumerator SetupLocalPlayerAfterSpawn()
    {
        yield return null;

        if (playerCamera == null)
        {
            Debug.LogError($"{name}: Cannot setup camera because PlayerCamera is null.");
        }
        else
        {
            playerCamera.SetupLocalCamera();
            playerCamera.UpdateCameraTarget();
        }

        yield return null;

        NetworkLauncher launcher = FindFirstObjectByType<NetworkLauncher>();

        if (launcher != null)
        {
            launcher.NotifyLocalPlayerSpawnedOnWaitingPlanet();
        }
        else
        {
            Debug.LogWarning("[NetworkPlayer] NetworkLauncher was not found.");
        }
    }

    private void SetLocalOnlyObjects(bool isLocalPlayer)
    {
        foreach (GameObject obj in localOnlyObjects)
        {
            if (obj != null)
            {
                obj.SetActive(isLocalPlayer);
            }
        }
    }

    private void SetLocalVisual(bool isLocalPlayer)
    {
        if (!hideLocalVisual)
        {
            return;
        }

        if (visualRoot == null)
        {
            Debug.LogWarning($"{name}: Visual Root is not assigned.");
            return;
        }

        bool shouldShowVisual = !isLocalPlayer;

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            renderer.enabled = shouldShowVisual;
        }
    }
}