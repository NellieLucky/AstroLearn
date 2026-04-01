using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class CelestialStatEntry
{
    public string label;
    public string value;
}

public class CelestialBody : MonoBehaviour
{
    public string bodyName;

    [Header("Planetary Profile")]
    public Sprite profileImage;
    [TextArea(5, 20)]
    public string profileDescription;

    [Header("Stats")]
    public List<CelestialStatEntry> stats = new List<CelestialStatEntry>();

    [Header("Focus Camera")]
    public float focusDistanceOverride;
    public float minFocusDistanceOverride;
    public float maxFocusDistanceOverride = 30f;

    private Quaternion defaultWorldRotation;

    private void Awake()
    {
        defaultWorldRotation = transform.rotation;
    }

    public void ResetToDefaultOrientation()
    {
        transform.rotation = defaultWorldRotation;
    }

    public Quaternion GetDefaultWorldRotation()
    {
        return defaultWorldRotation;
    }

    public void GetFocusDistances(float autoFocusDistance, float autoMinFocusDistance, float autoMaxFocusDistance,
        out float focusDistance, out float minFocusDistance, out float maxFocusDistance)
    {
        focusDistance = focusDistanceOverride > 0f ? focusDistanceOverride : autoFocusDistance;
        minFocusDistance = minFocusDistanceOverride > 0f ? minFocusDistanceOverride : autoMinFocusDistance;
        maxFocusDistance = maxFocusDistanceOverride > 0f ? maxFocusDistanceOverride : autoMaxFocusDistance;

        if (minFocusDistance > focusDistance)
        {
            minFocusDistance = focusDistance;
        }

        if (maxFocusDistance < focusDistance)
        {
            maxFocusDistance = focusDistance;
        }
    }
}
