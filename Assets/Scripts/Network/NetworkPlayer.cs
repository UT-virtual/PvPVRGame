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
            NetworkTransform networkTransform = GetComponent<NetworkTransform>();
            if (networkTransform != null)
            {
                networkTransform.DisableSharedModeInterpolation = true;
            }

            NetworkLauncher launcher = FindFirstObjectByType<NetworkLauncher>();
            PlayerController playerController = GetComponent<PlayerController>();

            if (launcher != null && playerController != null)
            {
                launcher.RegisterLocalPlayer(playerController);
                launcher.AttachXrOriginToPlayer(transform);

                VRBodyTargetSync bodySync = GetComponent<VRBodyTargetSync>();
                launcher.ConfigureLocalVrBodySync(bodySync);
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
            launcher.DetachXrOriginFromPlayer();
            launcher.UnregisterLocalPlayer(playerController);
        }
    }

    private IEnumerator SetupLocalPlayerAfterSpawn()
    {
        yield return null;

        NetworkLauncher launcher = FindFirstObjectByType<NetworkLauncher>();

        if (playerCamera == null)
        {
            Debug.LogError($"{name}: Cannot setup camera because PlayerCamera is null.");
        }
        else
        {
            if (launcher != null)
            {
                launcher.ConfigureLocalPlayerCamera(playerCamera);
            }

            playerCamera.SetupLocalCamera();
            playerCamera.UpdateCameraTarget();
        }

        yield return null;

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