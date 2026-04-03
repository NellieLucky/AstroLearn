using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(LineRenderer))]
public class OrbitSnakeTrail : MonoBehaviour
{
    [Range(0.1f, 0.98f)]
    public float orbitCoverage = 0.82f;

    [Min(8)]
    public int sampleCount = 96;

    public bool alignToOrbitPlane = true;

    [Header("Fade")]
    [Range(0f, 0.45f)]
    public float endpointFadePortion = 0.16f;
    [Range(0f, 1f)]
    public float endpointAlpha = 0f;
    [Range(0f, 1f)]
    public float bodyAlpha = 1f;
    [Range(0f, 1f)]
    public float endpointWidthMultiplier = 0.1f;

    [Header("Width")]
    public bool autoWidth = false;
    public float widthPerOrbitUnit = 0.0025f;
    public float minLineWidth = 0.006f;
    public float maxLineWidth = 0.03f;

    [Header("Material")]
    public bool forceTransparentMaterial = true;

    private LineRenderer lineRenderer;
    private OrbitAroundSun orbitAroundSun;
    private OrbitAroundPlanet orbitAroundPlanet;
    private Material runtimeLineMaterial;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        orbitAroundSun = GetComponent<OrbitAroundSun>();
        orbitAroundPlanet = GetComponent<OrbitAroundPlanet>();
    }

    private void LateUpdate()
    {
        if (lineRenderer == null)
        {
            return;
        }

        EnsureTransparentLineMaterial();

        if (!TryGetOrbitCenter(out Vector3 center))
        {
            lineRenderer.positionCount = 0;
            return;
        }

        Vector3 radiusVector = transform.position - center;
        if (radiusVector.sqrMagnitude <= Mathf.Epsilon)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        int points = Mathf.Max(2, sampleCount);
        float coveredDegrees = 360f * Mathf.Clamp01(orbitCoverage);
        float step = coveredDegrees / (points - 1);

        lineRenderer.positionCount = points;
        UpdateLineFade();
        UpdateLineWidthTaper(radiusVector.magnitude);

        for (int i = 0; i < points; i++)
        {
            float angle = -coveredDegrees + (step * i);
            Vector3 position = center + Quaternion.AngleAxis(angle, Vector3.up) * radiusVector;
            lineRenderer.SetPosition(i, position);
        }

        if (alignToOrbitPlane)
        {
            lineRenderer.alignment = LineAlignment.View;
        }
    }

    private bool TryGetOrbitCenter(out Vector3 center)
    {
        center = Vector3.zero;

        if (orbitAroundSun != null && orbitAroundSun.sun != null)
        {
            center = orbitAroundSun.sun.position;
            return true;
        }

        if (orbitAroundPlanet != null && orbitAroundPlanet.target != null)
        {
            center = orbitAroundPlanet.target.position;
            return true;
        }

        return false;
    }

    private void UpdateLineFade()
    {
        float fadePortion = Mathf.Clamp(endpointFadePortion, 0f, 0.45f);
        Color baseStartColor = lineRenderer.startColor;
        Color baseEndColor = lineRenderer.endColor;

        baseStartColor.a = 1f;
        baseEndColor.a = 1f;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(baseStartColor, 0f),
                new GradientColorKey(baseEndColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(endpointAlpha, 0f),
                new GradientAlphaKey(bodyAlpha, fadePortion),
                new GradientAlphaKey(bodyAlpha, 1f - fadePortion),
                new GradientAlphaKey(endpointAlpha, 1f)
            });

        lineRenderer.colorGradient = gradient;
    }

    private void UpdateLineWidthTaper(float orbitRadius)
    {
        float fadePortion = Mathf.Clamp(endpointFadePortion, 0f, 0.45f);
        float endWidth = Mathf.Clamp01(endpointWidthMultiplier);

        if (autoWidth)
        {
            lineRenderer.widthMultiplier = Mathf.Clamp(orbitRadius * widthPerOrbitUnit, minLineWidth, maxLineWidth);
        }

        AnimationCurve widthCurve = new AnimationCurve(
            new Keyframe(0f, endWidth),
            new Keyframe(fadePortion, 1f),
            new Keyframe(1f - fadePortion, 1f),
            new Keyframe(1f, endWidth));

        lineRenderer.widthCurve = widthCurve;
    }

    private void EnsureTransparentLineMaterial()
    {
        if (!forceTransparentMaterial || lineRenderer == null)
        {
            return;
        }

        if (runtimeLineMaterial == null)
        {
            runtimeLineMaterial = lineRenderer.material;
        }

        if (runtimeLineMaterial == null)
        {
            return;
        }

        runtimeLineMaterial.renderQueue = (int)RenderQueue.Transparent;

        if (runtimeLineMaterial.HasProperty("_Surface"))
        {
            runtimeLineMaterial.SetFloat("_Surface", 1f);
        }

        if (runtimeLineMaterial.HasProperty("_Blend"))
        {
            runtimeLineMaterial.SetFloat("_Blend", 0f);
        }

        if (runtimeLineMaterial.HasProperty("_ZWrite"))
        {
            runtimeLineMaterial.SetFloat("_ZWrite", 0f);
        }

        if (runtimeLineMaterial.HasProperty("_SrcBlend"))
        {
            runtimeLineMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (runtimeLineMaterial.HasProperty("_DstBlend"))
        {
            runtimeLineMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        runtimeLineMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        runtimeLineMaterial.EnableKeyword("_ALPHABLEND_ON");
        runtimeLineMaterial.DisableKeyword("_ALPHATEST_ON");
        runtimeLineMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }
}
