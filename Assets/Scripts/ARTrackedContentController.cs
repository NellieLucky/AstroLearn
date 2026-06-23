using System.Collections.Generic;
using UnityEngine;

public class ARTrackedContentController : MonoBehaviour
{
    [SerializeField] private GameObject trackedContentRoot;
    [SerializeField] private List<GameObject> extraObjectsToToggle = new List<GameObject>();
    [SerializeField] private bool hideContentOnStart = true;
    [SerializeField] private string arCanvasObjectName = "ARCanvas";
    [SerializeField] private string instructionTextObjectName = "InstructionText";
    [SerializeField] private string bodyNameOverride;

    private ARSceneUIController arSceneUiController;
    private GameObject instructionTextObject;
    private string resolvedBodyName;

    private void Awake()
    {
        CacheArSceneUiController();
        CacheInstructionTextObject();
        resolvedBodyName = ResolveBodyName();

        if (hideContentOnStart)
        {
            SetTrackedVisible(false);
        }
    }

    public void HandleTargetFound()
    {
        CacheArSceneUiController();
        SetTrackedVisible(true);
        if (arSceneUiController != null)
        {
            arSceneUiController.HandleTrackedBodyFound(resolvedBodyName);
            arSceneUiController.HideInstructionText();
        }
        else if (instructionTextObject != null)
        {
            instructionTextObject.SetActive(false);
        }
    }

    public void HandleTargetLost()
    {
        CacheArSceneUiController();
        SetTrackedVisible(false);
        if (arSceneUiController != null)
        {
            arSceneUiController.HandleTrackedBodyLost(resolvedBodyName);
            arSceneUiController.ShowInstructionText();
        }
        else if (instructionTextObject != null)
        {
            instructionTextObject.SetActive(true);
        }
    }

    public void SetTrackedVisible(bool isVisible)
    {
        if (trackedContentRoot != null)
        {
            trackedContentRoot.SetActive(isVisible);
        }

        foreach (GameObject extraObject in extraObjectsToToggle)
        {
            if (extraObject != null)
            {
                extraObject.SetActive(isVisible);
            }
        }
    }

    private void CacheArSceneUiController()
    {
        if (arSceneUiController != null)
        {
            return;
        }

        GameObject canvasObject = GameObject.Find(arCanvasObjectName);
        if (canvasObject != null)
        {
            arSceneUiController = canvasObject.GetComponent<ARSceneUIController>();
        }

        if (arSceneUiController == null)
        {
            arSceneUiController = Object.FindFirstObjectByType<ARSceneUIController>();
        }
    }

    private void CacheInstructionTextObject()
    {
        if (instructionTextObject != null)
        {
            return;
        }

        GameObject canvasObject = GameObject.Find(arCanvasObjectName);
        if (canvasObject != null)
        {
            Transform instruction = FindChildRecursive(canvasObject.transform, instructionTextObjectName);
            if (instruction != null)
            {
                instructionTextObject = instruction.gameObject;
            }
        }
    }

    private static Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == targetName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private string ResolveBodyName()
    {
        if (!string.IsNullOrWhiteSpace(bodyNameOverride))
        {
            return ARBodySelectionContext.NormalizeBodyKey(bodyNameOverride);
        }

        if (string.Equals(gameObject.name, "ImageTarget_SolarSystem", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(gameObject.name, "ImageTarget", System.StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (gameObject.name.StartsWith("ImageTarget_", System.StringComparison.OrdinalIgnoreCase))
        {
            return ARBodySelectionContext.NormalizeBodyKey(gameObject.name.Substring("ImageTarget_".Length));
        }

        if (trackedContentRoot != null && trackedContentRoot.name.StartsWith("TrackedContent_", System.StringComparison.OrdinalIgnoreCase))
        {
            return ARBodySelectionContext.NormalizeBodyKey(trackedContentRoot.name.Substring("TrackedContent_".Length));
        }

        return string.Empty;
    }
}
