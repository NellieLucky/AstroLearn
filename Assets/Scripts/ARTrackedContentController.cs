using System.Collections.Generic;
using UnityEngine;

public class ARTrackedContentController : MonoBehaviour
{
    [SerializeField] private GameObject trackedContentRoot;
    [SerializeField] private List<GameObject> extraObjectsToToggle = new List<GameObject>();
    [SerializeField] private bool hideContentOnStart = true;
    [SerializeField] private string arCanvasObjectName = "ARCanvas";
    [SerializeField] private string instructionTextObjectName = "InstructionText";

    private ARSceneUIController arSceneUiController;
    private GameObject instructionTextObject;

    private void Awake()
    {
        CacheArSceneUiController();
        CacheInstructionTextObject();

        if (hideContentOnStart)
        {
            SetTrackedVisible(false);
        }
    }

    public void HandleTargetFound()
    {
        SetTrackedVisible(true);
        if (arSceneUiController != null)
        {
            arSceneUiController.HideInstructionText();
        }
        else if (instructionTextObject != null)
        {
            instructionTextObject.SetActive(false);
        }
    }

    public void HandleTargetLost()
    {
        SetTrackedVisible(false);
        if (arSceneUiController != null)
        {
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
}
