using System.Collections.Generic;
using UnityEngine;
using Vuforia;

public class ARTrackedContentController : MonoBehaviour
{
    [SerializeField] private GameObject trackedContentRoot;
    [SerializeField] private List<GameObject> extraObjectsToToggle = new List<GameObject>();
    [SerializeField] private bool hideContentOnStart = true;
    [SerializeField] private bool requireStrictTracking = true;
    [SerializeField] private bool enableTrackedManipulation = true;
    [SerializeField] private string arCanvasObjectName = "ARCanvas";
    [SerializeField] private string instructionTextObjectName = "InstructionText";
    [SerializeField] private string bodyNameOverride;

    private ARSceneUIController arSceneUiController;
    private GameObject instructionTextObject;
    private ObserverBehaviour observerBehaviour;
    private string resolvedBodyName;
    private bool hasAppliedTrackingState;
    private bool lastAppliedTrackingState;

    private void Awake()
    {
        CacheArSceneUiController();
        CacheInstructionTextObject();
        CacheObserverBehaviour();
        EnsureTrackedManipulator();
        resolvedBodyName = ResolveBodyName();

        if (hideContentOnStart)
        {
            ApplyTrackingState(false, true);
            return;
        }

        RefreshTrackingState(true);
    }

    private void OnEnable()
    {
        CacheObserverBehaviour();
        RefreshTrackingState(true);
    }

    private void Update()
    {
        RefreshTrackingState(false);
    }

    private void OnDisable()
    {
        ApplyTrackingState(false, true);
    }

    public void HandleTargetFound()
    {
        ApplyTrackingState(true, false);
    }

    public void HandleTargetLost()
    {
        ApplyTrackingState(false, false);
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

    private void RefreshTrackingState(bool forceApply)
    {
        CacheObserverBehaviour();
        if (observerBehaviour == null)
        {
            return;
        }

        bool isTracked = IsTrackedNow();
        ApplyTrackingState(isTracked, forceApply);
    }

    private void ApplyTrackingState(bool isTracked, bool forceApply)
    {
        CacheArSceneUiController();

        if (!forceApply && hasAppliedTrackingState && lastAppliedTrackingState == isTracked)
        {
            return;
        }

        hasAppliedTrackingState = true;
        lastAppliedTrackingState = isTracked;

        SetTrackedVisible(isTracked);

        if (arSceneUiController != null)
        {
            if (isTracked)
            {
                arSceneUiController.HandleTrackedBodyFound(resolvedBodyName);
                arSceneUiController.HideInstructionText();
            }
            else
            {
                arSceneUiController.HandleTrackedBodyLost(resolvedBodyName);
                arSceneUiController.ShowInstructionText();
            }
        }
        else if (instructionTextObject != null && instructionTextObject.activeSelf == isTracked)
        {
            instructionTextObject.SetActive(!isTracked);
        }
    }

    private bool IsTrackedNow()
    {
        if (observerBehaviour == null)
        {
            return false;
        }

        Status currentStatus = observerBehaviour.TargetStatus.Status;
        if (!requireStrictTracking)
        {
            return currentStatus == Status.TRACKED || currentStatus == Status.EXTENDED_TRACKED;
        }

        return currentStatus == Status.TRACKED;
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

    private void CacheObserverBehaviour()
    {
        if (observerBehaviour != null)
        {
            return;
        }

        observerBehaviour = GetComponent<ObserverBehaviour>();
    }

    private void EnsureTrackedManipulator()
    {
        if (!enableTrackedManipulation || trackedContentRoot == null)
        {
            return;
        }

        if (trackedContentRoot.GetComponent<ARTrackedObjectManipulator>() == null)
        {
            trackedContentRoot.AddComponent<ARTrackedObjectManipulator>();
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
