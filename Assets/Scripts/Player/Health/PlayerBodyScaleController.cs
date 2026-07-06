using UnityEngine;

public sealed class PlayerBodyScaleController
{
    private readonly GameObject visualRoot;
    private readonly CharacterController characterController;

    private Vector3 originalVisualRootScale = Vector3.one;
    private bool hasOriginalVisualRootScale;

    private float originalCharacterControllerHeight;
    private float originalCharacterControllerRadius;
    private Vector3 originalCharacterControllerCenter;
    private bool hasOriginalCharacterControllerValues;

    public PlayerBodyScaleController(
        GameObject visualRoot,
        CharacterController characterController
    )
    {
        this.visualRoot = visualRoot;
        this.characterController = characterController;

        CaptureInitialValues();
    }

    public void SetBodySizeMultiplier(float multiplier)
    {
        multiplier = Mathf.Clamp(multiplier, 0.2f, 2.0f);

        if (visualRoot != null && hasOriginalVisualRootScale)
        {
            visualRoot.transform.localScale = originalVisualRootScale * multiplier;
        }

        if (characterController != null && hasOriginalCharacterControllerValues)
        {
            characterController.height = originalCharacterControllerHeight * multiplier;
            characterController.radius = originalCharacterControllerRadius * multiplier;
            characterController.center = originalCharacterControllerCenter * multiplier;
        }
    }

    private void CaptureInitialValues()
    {
        if (visualRoot != null)
        {
            originalVisualRootScale = visualRoot.transform.localScale;
            hasOriginalVisualRootScale = true;
        }

        if (characterController != null)
        {
            originalCharacterControllerHeight = characterController.height;
            originalCharacterControllerRadius = characterController.radius;
            originalCharacterControllerCenter = characterController.center;
            hasOriginalCharacterControllerValues = true;
        }
    }
}
