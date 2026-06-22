using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ARLoadingPanelAnimator : MonoBehaviour
{
    private const string DefaultBaseText = "Now Loading";

    [Header("References")]
    [SerializeField] private RectTransform loadingBarFill;
    [SerializeField] private RectTransform loadingBarBackground;
    [SerializeField] private TMP_Text loadingTextTmp;
    [SerializeField] private Text loadingTextLegacy;
    [SerializeField] private RectTransform loadingImage;

    [Header("Text")]
    [SerializeField] private string baseText = "Now Loading";
    [SerializeField] private float textAnimationSpeed = 2.5f;

    [Header("Bar")]
    [SerializeField] private float barAnimationSpeed = 1.25f;
    [SerializeField] private float minFillPercent = 0.08f;
    [SerializeField] private float maxFillPercent = 1f;

    private string cachedBaseText;

    private void Awake()
    {
        AutoAssignReferences();
        cachedBaseText = string.IsNullOrWhiteSpace(baseText) ? DefaultBaseText : baseText;
    }

    private void OnEnable()
    {
        UpdateLoadingText(0);
        UpdateLoadingBar(0f);

    }

    private void Update()
    {
        AnimateText();
        AnimateBar();
    }

    private void AnimateText()
    {
        int dotCount = Mathf.FloorToInt(Time.unscaledTime * textAnimationSpeed) % 4;
        UpdateLoadingText(dotCount);
    }

    private void AnimateBar()
    {
        float wave = Mathf.PingPong(Time.unscaledTime * barAnimationSpeed, 1f);
        float fillPercent = Mathf.Lerp(minFillPercent, maxFillPercent, wave);
        UpdateLoadingBar(fillPercent);
    }

    private void UpdateLoadingText(int dotCount)
    {
        string value = cachedBaseText + new string('.', dotCount);

        if (loadingTextTmp != null)
        {
            loadingTextTmp.text = value;
        }

        if (loadingTextLegacy != null)
        {
            loadingTextLegacy.text = value;
        }
    }

    public void SetBaseText(string value)
    {
        cachedBaseText = string.IsNullOrWhiteSpace(value) ? DefaultBaseText : value;
        UpdateLoadingText(0);
    }

    public void ResetBaseText()
    {
        SetBaseText(baseText);
    }

    private void UpdateLoadingBar(float fillPercent)
    {
        if (loadingBarFill == null || loadingBarBackground == null)
        {
            return;
        }

        float clampedFill = Mathf.Clamp01(fillPercent);
        float targetWidth = loadingBarBackground.rect.width * clampedFill;
        loadingBarFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
    }

    private void AutoAssignReferences()
    {
        if (loadingBarFill == null)
        {
            Transform target = transform.Find("LoadingBarFill");
            if (target != null)
            {
                loadingBarFill = target as RectTransform;
            }
        }

        if (loadingBarBackground == null)
        {
            Transform target = transform.Find("LoadingBarBG");
            if (target != null)
            {
                loadingBarBackground = target as RectTransform;
            }
        }

        if (loadingTextTmp == null)
        {
            Transform target = transform.Find("LoadingText");
            if (target != null)
            {
                loadingTextTmp = target.GetComponent<TMP_Text>();
                loadingTextLegacy = loadingTextLegacy == null ? target.GetComponent<Text>() : loadingTextLegacy;
            }
        }

        if (loadingTextLegacy == null)
        {
            Transform target = transform.Find("LoadingText");
            if (target != null)
            {
                loadingTextLegacy = target.GetComponent<Text>();
            }
        }

        if (loadingImage == null)
        {
            Transform target = transform.Find("LoadingImage");
            if (target != null)
            {
                loadingImage = target as RectTransform;
            }
        }
    }
}
