using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class NetworkConnectionUIController
{
    private readonly GameObject connectionMenuRoot;
    private readonly TMP_Text statusTextLabel;
    private readonly Button hostButton;
    private readonly Button clientButton;

    private Action onHostClicked;
    private Action onClientClicked;

    private bool connectionButtonLocked;
    private string statusText = "ホスト・クライアント選択";

    public NetworkConnectionUIController(
        GameObject connectionMenuRoot,
        TMP_Text statusTextLabel,
        Button hostButton,
        Button clientButton
    )
    {
        this.connectionMenuRoot = connectionMenuRoot;
        this.statusTextLabel = statusTextLabel;
        this.hostButton = hostButton;
        this.clientButton = clientButton;
    }

    public void InitializeButtons(
        Action onHostClicked,
        Action onClientClicked
    )
    {
        this.onHostClicked = onHostClicked;
        this.onClientClicked = onClientClicked;

        if (hostButton != null)
        {
            hostButton.onClick.RemoveListener(HandleHostButtonClicked);
            hostButton.onClick.AddListener(HandleHostButtonClicked);
        }
        else
        {
            Debug.LogWarning("[NetworkConnectionUIController] HostButton is not assigned.");
        }

        if (clientButton != null)
        {
            clientButton.onClick.RemoveListener(HandleClientButtonClicked);
            clientButton.onClick.AddListener(HandleClientButtonClicked);
        }
        else
        {
            Debug.LogWarning("[NetworkConnectionUIController] ClientButton is not assigned.");
        }
    }

    public void DisposeButtons()
    {
        if (hostButton != null)
        {
            hostButton.onClick.RemoveListener(HandleHostButtonClicked);
        }

        if (clientButton != null)
        {
            clientButton.onClick.RemoveListener(HandleClientButtonClicked);
        }
    }

    public void LockButtons()
    {
        connectionButtonLocked = true;
        SetConnectionButtonsInteractable(false);
    }

    public void UnlockButtons()
    {
        connectionButtonLocked = false;
        SetConnectionButtonsInteractable(true);
    }

    public void ShowConnectionMenuAndUnlockButtons()
    {
        SetConnectionMenuVisible(true);
        UnlockButtons();
    }

    public void SetConnectionButtonsInteractable(bool interactable)
    {
        if (hostButton != null)
        {
            hostButton.interactable = interactable;
        }

        if (clientButton != null)
        {
            clientButton.interactable = interactable;
        }
    }

    public void SetConnectionMenuVisible(bool visible)
    {
        if (connectionMenuRoot != null)
        {
            connectionMenuRoot.SetActive(visible);
        }
    }

    public void SetStatusTextVisible(bool visible)
    {
        if (statusTextLabel != null)
        {
            statusTextLabel.gameObject.SetActive(visible);
        }
    }

    public void SetStatusText(string text)
    {
        statusText = text;
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (statusTextLabel != null)
        {
            statusTextLabel.text = statusText;
        }
    }

    private void HandleHostButtonClicked()
    {
        Debug.Log("[NetworkConnectionUIController] Host button clicked.");

        if (connectionButtonLocked)
        {
            Debug.LogWarning("[NetworkConnectionUIController] Host click ignored because button is locked.");
            return;
        }

        LockButtons();
        onHostClicked?.Invoke();
    }

    private void HandleClientButtonClicked()
    {
        Debug.Log("[NetworkConnectionUIController] Client button clicked.");

        if (connectionButtonLocked)
        {
            Debug.LogWarning("[NetworkConnectionUIController] Client click ignored because button is locked.");
            return;
        }

        LockButtons();
        onClientClicked?.Invoke();
    }
}
