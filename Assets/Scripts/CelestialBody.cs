using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class CelestialStatEntry
{
    public string label;
    public string value;
}

[Serializable]
public class CelestialGalleryEntry
{
    public Sprite image;
    public string title;
    [TextArea(3, 10)]
    public string description;
}

public class CelestialBody : MonoBehaviour
{
    public string bodyName;

    [Header("Solar System Label")]
    public Color labelColor = Color.white;

    [Header("Planetary Profile")]
    public Sprite profileImage;
    [TextArea(5, 20)]
    public string profileDescription;

    [Header("Stats")]
    public List<CelestialStatEntry> stats = new List<CelestialStatEntry>();

    [Header("Orbit Characteristics")]
    [TextArea(5, 20)]
    public string orbitCharacteristicsDescription;

    [Header("Solid Core")]
    [TextArea(5, 20)]
    public string structureDescription;

    [Header("Atmosphere")]
    public Sprite atmosphereImage;
    [TextArea(5, 20)]
    public string atmosphereDescription;

    [Header("Focus Camera")]
    public float focusDistanceOverride;
    public float minFocusDistanceOverride;
    public float maxFocusDistanceOverride = 30f;

    private Quaternion defaultWorldRotation;

    [Header("Gallery")]
    public List<CelestialGalleryEntry> galleryEntries = new List<CelestialGalleryEntry>();


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
