using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Effects/UI Graphic Gradient")]
[RequireComponent(typeof(Graphic))]
public class UIGraphicGradient : BaseMeshEffect
{
    public enum GradientMode
    {
        Vertical,
        Horizontal,
        FourCorners
    }

    [SerializeField] private GradientMode gradientMode = GradientMode.Vertical;
    [SerializeField] private Color topColor = new Color(0.38f, 0.18f, 0.95f, 1f);
    [SerializeField] private Color bottomColor = new Color(0.09f, 0.08f, 0.28f, 1f);
    [SerializeField] private Color leftColor = new Color(0.19f, 0.14f, 0.68f, 1f);
    [SerializeField] private Color rightColor = new Color(0.49f, 0.24f, 1f, 1f);
    [SerializeField] private Color topLeftColor = new Color(0.30f, 0.18f, 0.98f, 1f);
    [SerializeField] private Color topRightColor = new Color(0.55f, 0.24f, 1f, 1f);
    [SerializeField] private Color bottomLeftColor = new Color(0.11f, 0.10f, 0.33f, 1f);
    [SerializeField] private Color bottomRightColor = new Color(0.19f, 0.12f, 0.46f, 1f);
    [SerializeField] private bool preserveGraphicAlpha = true;

    public override void ModifyMesh(VertexHelper vertexHelper)
    {
        if (!IsActive() || vertexHelper.currentVertCount == 0)
        {
            return;
        }

        List<UIVertex> vertices = new List<UIVertex>();
        vertexHelper.GetUIVertexStream(vertices);
        if (vertices.Count == 0)
        {
            return;
        }

        Vector2 min = vertices[0].position;
        Vector2 max = vertices[0].position;

        for (int i = 1; i < vertices.Count; i++)
        {
            Vector3 position = vertices[i].position;
            min.x = Mathf.Min(min.x, position.x);
            min.y = Mathf.Min(min.y, position.y);
            max.x = Mathf.Max(max.x, position.x);
            max.y = Mathf.Max(max.y, position.y);
        }

        float width = Mathf.Max(0.0001f, max.x - min.x);
        float height = Mathf.Max(0.0001f, max.y - min.y);

        for (int i = 0; i < vertices.Count; i++)
        {
            UIVertex vertex = vertices[i];
            Vector3 position = vertex.position;
            float normalizedX = Mathf.InverseLerp(min.x, max.x, position.x);
            float normalizedY = Mathf.InverseLerp(min.y, max.y, position.y);

            Color gradientColor = EvaluateGradient(normalizedX, normalizedY);
            if (preserveGraphicAlpha)
            {
                gradientColor.a *= vertex.color.a;
            }

            vertex.color = MultiplyColors(vertex.color, gradientColor);
            vertices[i] = vertex;
        }

        vertexHelper.Clear();
        vertexHelper.AddUIVertexTriangleStream(vertices);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        if (graphic != null)
        {
            graphic.SetVerticesDirty();
        }
    }

    private Color EvaluateGradient(float normalizedX, float normalizedY)
    {
        switch (gradientMode)
        {
            case GradientMode.Horizontal:
                return Color.Lerp(leftColor, rightColor, normalizedX);
            case GradientMode.FourCorners:
                Color topBlend = Color.Lerp(topLeftColor, topRightColor, normalizedX);
                Color bottomBlend = Color.Lerp(bottomLeftColor, bottomRightColor, normalizedX);
                return Color.Lerp(bottomBlend, topBlend, normalizedY);
            default:
                return Color.Lerp(bottomColor, topColor, normalizedY);
        }
    }

    private static Color MultiplyColors(Color baseColor, Color gradientColor)
    {
        return new Color(
            baseColor.r * gradientColor.r,
            baseColor.g * gradientColor.g,
            baseColor.b * gradientColor.b,
            baseColor.a * gradientColor.a);
    }
}
