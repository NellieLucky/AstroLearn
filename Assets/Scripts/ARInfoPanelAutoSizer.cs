using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ARInfoPanelAutoSizer : MonoBehaviour
{
    [SerializeField] private string infoTextObjectName = "InfoText";
    [SerializeField] private string fillBackgroundObjectName = "FillBackground";
    [SerializeField] private string scrollViewObjectName = "InfoScrollView";
    [SerializeField] private float minimumScrollHeight = 120f;
    [SerializeField] private float maximumCanvasHeightRatio = 0.72f;
    [SerializeField] private float extraVerticalPadding = 16f;
    [SerializeField] private bool preserveTopEdge = true;

    private RectTransform panelRect;
    private RectTransform fillBackgroundRect;
    private RectTransform scrollViewRect;
    private RectTransform viewportRect;
    private RectTransform contentRect;
    private RectTransform infoTextRect;
    private ScrollRect scrollRect;
    private TMP_Text infoTmp;
    private Text infoLegacy;
    private RectInsets fillInsets;
    private RectInsets scrollInsets;
    private RectInsets infoTextInsets;
    private bool layoutInitialized;
    private bool refreshQueued;

    public void Configure(bool preserveTop)
    {
        preserveTopEdge = preserveTop;
    }

    public void QueueRefresh()
    {
        refreshQueued = true;

        if (isActiveAndEnabled)
        {
            RefreshSize();
        }
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        RefreshSize();
    }

    private void LateUpdate()
    {
        if (!refreshQueued)
        {
            return;
        }

        RefreshSize();
    }

    public void RefreshSize()
    {
        refreshQueued = false;

        if (!CacheReferences())
        {
            return;
        }

        if (!InitializeLayout())
        {
            refreshQueued = true;
            return;
        }

        Canvas.ForceUpdateCanvases();

        float availableTextWidth = ResolveAvailableTextWidth();
        if (availableTextWidth < 32f)
        {
            refreshQueued = true;
            return;
        }

        float preferredTextHeight = ResolvePreferredTextHeight(availableTextWidth);
        float preferredContentHeight = preferredTextHeight + GetTextVerticalPadding() + extraVerticalPadding;
        float maximumPanelHeight = Mathf.Max(GetMinimumPanelHeight(), ResolveCanvasHeight() * maximumCanvasHeightRatio);
        float maximumScrollHeight = Mathf.Max(minimumScrollHeight, maximumPanelHeight - scrollInsets.Top - scrollInsets.Bottom);
        float scrollHeight = Mathf.Clamp(preferredContentHeight, minimumScrollHeight, maximumScrollHeight);
        float contentHeight = Mathf.Max(preferredContentHeight, scrollHeight);
        float panelHeight = scrollHeight + scrollInsets.Top + scrollInsets.Bottom;

        ApplyPanelHeight(panelHeight);
        ApplyStretchOffsets(fillBackgroundRect, fillInsets);
        ApplyStretchOffsets(scrollViewRect, scrollInsets);
        ApplyContentHeight(contentHeight);

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        if (contentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        Canvas.ForceUpdateCanvases();
    }

    private bool CacheReferences()
    {
        panelRect = transform as RectTransform;
        if (panelRect == null)
        {
            return false;
        }

        if (fillBackgroundRect == null)
        {
            fillBackgroundRect = FindRect(fillBackgroundObjectName);
        }

        if (scrollViewRect == null)
        {
            scrollViewRect = FindRect(scrollViewObjectName);
        }

        if (infoTextRect == null)
        {
            infoTextRect = FindRect(infoTextObjectName);
        }

        if (scrollRect == null && scrollViewRect != null)
        {
            scrollRect = scrollViewRect.GetComponent<ScrollRect>();
        }

        if (viewportRect == null && scrollRect != null)
        {
            viewportRect = scrollRect.viewport;
        }

        if (contentRect == null)
        {
            if (scrollRect != null && scrollRect.content != null)
            {
                contentRect = scrollRect.content;
            }
            else if (infoTextRect != null)
            {
                contentRect = infoTextRect.parent as RectTransform;
            }
        }

        if (infoTmp == null && infoTextRect != null)
        {
            infoTmp = infoTextRect.GetComponent<TMP_Text>();
        }

        if (infoLegacy == null && infoTextRect != null)
        {
            infoLegacy = infoTextRect.GetComponent<Text>();
        }

        return scrollViewRect != null && contentRect != null && infoTextRect != null;
    }

    private bool InitializeLayout()
    {
        if (layoutInitialized)
        {
            return true;
        }

        if (!gameObject.activeInHierarchy)
        {
            return false;
        }

        Canvas.ForceUpdateCanvases();

        fillInsets = CaptureInsets(panelRect, fillBackgroundRect);
        scrollInsets = CaptureInsets(panelRect, scrollViewRect);
        infoTextInsets = CaptureInsets(contentRect, infoTextRect);

        ApplyStretchOffsets(fillBackgroundRect, fillInsets);
        ApplyStretchOffsets(scrollViewRect, scrollInsets);
        NormalizeContentRect();
        ApplyStretchOffsets(infoTextRect, infoTextInsets);

        layoutInitialized = true;
        return true;
    }

    private void NormalizeContentRect()
    {
        if (contentRect == null)
        {
            return;
        }

        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, Mathf.Max(minimumScrollHeight, viewportRect != null ? viewportRect.rect.height : minimumScrollHeight));
    }

    private void ApplyContentHeight(float contentHeight)
    {
        if (contentRect == null)
        {
            return;
        }

        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, contentHeight);
    }

    private void ApplyPanelHeight(float panelHeight)
    {
        if (panelRect == null)
        {
            return;
        }

        float preservedEdge = preserveTopEdge ? GetTopEdgeInParentSpace() : GetBottomEdgeInParentSpace();
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
        Canvas.ForceUpdateCanvases();

        float currentEdge = preserveTopEdge ? GetTopEdgeInParentSpace() : GetBottomEdgeInParentSpace();
        float verticalOffset = preservedEdge - currentEdge;
        if (Mathf.Abs(verticalOffset) > 0.01f)
        {
            panelRect.anchoredPosition += new Vector2(0f, verticalOffset);
        }
    }

    private float ResolveAvailableTextWidth()
    {
        float width = infoTextRect != null ? infoTextRect.rect.width : 0f;
        if (width > 0f)
        {
            return width;
        }

        if (contentRect == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, contentRect.rect.width - GetTextHorizontalPadding());
    }

    private float ResolvePreferredTextHeight(float width)
    {
        if (infoTmp != null)
        {
            return infoTmp.GetPreferredValues(infoTmp.text, width, Mathf.Infinity).y;
        }

        if (infoLegacy != null)
        {
            TextGenerationSettings settings = infoLegacy.GetGenerationSettings(new Vector2(width, 0f));
            return infoLegacy.cachedTextGeneratorForLayout.GetPreferredHeight(infoLegacy.text, settings) / Mathf.Max(1f, infoLegacy.pixelsPerUnit);
        }

        return minimumScrollHeight;
    }

    private float ResolveCanvasHeight()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        if (canvasRect != null && canvasRect.rect.height > 0f)
        {
            return canvasRect.rect.height;
        }

        return Screen.height;
    }

    private float GetMinimumPanelHeight()
    {
        return scrollInsets.Top + scrollInsets.Bottom + minimumScrollHeight;
    }

    private float GetTextHorizontalPadding()
    {
        return Mathf.Max(0f, infoTextInsets.Left + infoTextInsets.Right);
    }

    private float GetTextVerticalPadding()
    {
        return Mathf.Max(0f, infoTextInsets.Top + infoTextInsets.Bottom);
    }

    private float GetTopEdgeInParentSpace()
    {
        return GetEdgeInParentSpace(panelRect, 1);
    }

    private float GetBottomEdgeInParentSpace()
    {
        return GetEdgeInParentSpace(panelRect, 0);
    }

    private static float GetEdgeInParentSpace(RectTransform rect, int cornerIndex)
    {
        if (rect == null || rect.parent == null)
        {
            return 0f;
        }

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        RectTransform parentRect = rect.parent as RectTransform;
        return parentRect != null
            ? parentRect.InverseTransformPoint(corners[cornerIndex]).y
            : corners[cornerIndex].y;
    }

    private static RectInsets CaptureInsets(RectTransform parentRect, RectTransform childRect)
    {
        if (parentRect == null || childRect == null)
        {
            return RectInsets.Zero;
        }

        Vector3[] parentCorners = new Vector3[4];
        Vector3[] childCorners = new Vector3[4];
        parentRect.GetLocalCorners(parentCorners);
        childRect.GetWorldCorners(childCorners);

        for (int i = 0; i < childCorners.Length; i++)
        {
            childCorners[i] = parentRect.InverseTransformPoint(childCorners[i]);
        }

        return new RectInsets(
            childCorners[0].x - parentCorners[0].x,
            parentCorners[3].x - childCorners[3].x,
            parentCorners[1].y - childCorners[1].y,
            childCorners[0].y - parentCorners[0].y);
    }

    private static void ApplyStretchOffsets(RectTransform rect, RectInsets insets)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(insets.Left, insets.Bottom);
        rect.offsetMax = new Vector2(-insets.Right, -insets.Top);
    }

    private RectTransform FindRect(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform child = transforms[i];
            if (child != null && child.name == objectName)
            {
                return child as RectTransform;
            }
        }

        return null;
    }

    private readonly struct RectInsets
    {
        public static readonly RectInsets Zero = new RectInsets(0f, 0f, 0f, 0f);

        public RectInsets(float left, float right, float top, float bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }

        public float Left { get; }
        public float Right { get; }
        public float Top { get; }
        public float Bottom { get; }
    }
}
