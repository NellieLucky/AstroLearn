using System.Collections.Generic;
using UnityEngine;

public class ARTrackedContentController : MonoBehaviour
{
    [SerializeField] private GameObject trackedContentRoot;
    [SerializeField] private List<GameObject> extraObjectsToToggle = new List<GameObject>();
    [SerializeField] private bool hideContentOnStart = true;

    private void Awake()
    {
        if (hideContentOnStart)
        {
            SetTrackedVisible(false);
        }
    }

    public void HandleTargetFound()
    {
        SetTrackedVisible(true);
    }

    public void HandleTargetLost()
    {
        SetTrackedVisible(false);
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
}
