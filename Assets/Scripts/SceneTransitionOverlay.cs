using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionOverlay : MonoBehaviour
{
    private static SceneTransitionOverlay instance;
    private const string BuiltInLegacyFontName = "LegacyRuntime.ttf";

    private CanvasGroup canvasGroup;
    private Text messageText;
    private Image progressFill;

    private string baseMessage = "Loading";
    private float minimumDisplaySeconds = 0.1f;

    public static void LoadScene(string sceneName, string message)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneTransitionOverlay] Scene name is empty.");
            return;
        }

        if (instance == null)
        {
            GameObject overlayObject = new GameObject("SceneTransitionOverlay");
            instance = overlayObject.AddComponent<SceneTransitionOverlay>();
            DontDestroyOnLoad(overlayObject);
        }

        instance.BeginTransition(sceneName, message);
    }

    private void Awake()
    {
        BuildOverlay();
        HideImmediate();
    }

    private void Update()
    {
        if (canvasGroup == null || canvasGroup.alpha <= 0f)
        {
            return;
        }

        int dotCount = Mathf.FloorToInt(Time.unscaledTime * 2.5f) % 4;
        if (messageText != null)
        {
            messageText.text = baseMessage + new string('.', dotCount);
        }

        if (progressFill != null)
        {
            float width = Mathf.Lerp(0.1f, 1f, Mathf.PingPong(Time.unscaledTime * 1.25f, 1f));
            progressFill.rectTransform.anchorMax = new Vector2(width, 1f);
        }
    }

    private void BeginTransition(string sceneName, string message)
    {
        StopAllCoroutines();
        baseMessage = string.IsNullOrWhiteSpace(message) ? "Loading" : message;
        Show();
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        Time.timeScale = 1f;

        yield return null;

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            Debug.LogWarning($"[SceneTransitionOverlay] Failed to start loading scene '{sceneName}'.");
            HideImmediate();
            yield break;
        }

        float shownAt = Time.unscaledTime;

        while (!loadOperation.isDone || Time.unscaledTime - shownAt < minimumDisplaySeconds)
        {
            yield return null;
        }

        RestoreKnownUiRoots(sceneName);
        HideImmediate();
        Destroy(gameObject);
    }

    private void RestoreKnownUiRoots(string sceneName)
    {
        if (sceneName != "SolarSystemScene")
        {
            return;
        }

        SetActiveIfFound("SolarSystemUI", true);
        SetActiveIfFound("Menu", true);
        SetActiveIfFound("Canvas", true);
    }

    private static void SetActiveIfFound(string objectName, bool isActive)
    {
        GameObject target = GameObject.Find(objectName);
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }

    private void BuildOverlay()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000;

        gameObject.AddComponent<GraphicRaycaster>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        GameObject panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(transform, false);

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.01f, 0.06f, 1f);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject messageObject = new GameObject("Message");
        messageObject.transform.SetParent(panelObject.transform, false);

        messageText = messageObject.AddComponent<Text>();
        messageText.font = Resources.GetBuiltinResource<Font>(BuiltInLegacyFontName);
        messageText.fontSize = 30;
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.color = Color.white;

        RectTransform messageRect = messageObject.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0.2f, 0.48f);
        messageRect.anchorMax = new Vector2(0.8f, 0.58f);
        messageRect.offsetMin = Vector2.zero;
        messageRect.offsetMax = Vector2.zero;

        GameObject barBackgroundObject = new GameObject("BarBackground");
        barBackgroundObject.transform.SetParent(panelObject.transform, false);

        Image barBackground = barBackgroundObject.AddComponent<Image>();
        barBackground.color = new Color(0.33f, 0.82f, 1f, 0.22f);

        RectTransform barBackgroundRect = barBackgroundObject.GetComponent<RectTransform>();
        barBackgroundRect.anchorMin = new Vector2(0.25f, 0.41f);
        barBackgroundRect.anchorMax = new Vector2(0.75f, 0.42f);
        barBackgroundRect.offsetMin = Vector2.zero;
        barBackgroundRect.offsetMax = Vector2.zero;

        GameObject barFillObject = new GameObject("BarFill");
        barFillObject.transform.SetParent(barBackgroundObject.transform, false);

        progressFill = barFillObject.AddComponent<Image>();
        progressFill.color = new Color(0.55f, 0.9f, 1f, 1f);

        RectTransform fillRect = barFillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0.1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    private void Show()
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
    }

    private void HideImmediate()
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
