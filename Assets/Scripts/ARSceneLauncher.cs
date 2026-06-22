using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Serialization;

public class ARSceneLauncher : MonoBehaviour
{
    public static string LastNonArSceneName { get; private set; }

    [SerializeField] private string arSceneName = "SolarSystemARScene";
    [SerializeField] private LoadSceneMode loadSceneMode = LoadSceneMode.Single;
    [SerializeField] private GameObject loadingPanel;
    [FormerlySerializedAs("loadingDelaySeconds")]
    [SerializeField] private float minimumLoadingScreenSeconds = 0.1f;

    private bool isLoading;
    private readonly List<GameObject> hiddenUiObjects = new List<GameObject>();

    public void OpenArScene()
    {
        if (isLoading)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(arSceneName))
        {
            Debug.LogWarning("[ARSceneLauncher] AR scene name is empty.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(arSceneName))
        {
            Debug.LogWarning($"[ARSceneLauncher] Scene '{arSceneName}' is not in Build Settings.");
            return;
        }

        if (loadingPanel == null)
        {
            loadingPanel = FindLoadingPanel();
        }

        LastNonArSceneName = SceneManager.GetActiveScene().name;
        StartCoroutine(OpenArSceneRoutine());
    }

    public void CopyConfigurationFrom(ARSceneLauncher source)
    {
        if (source == null || source == this)
        {
            return;
        }

        arSceneName = source.arSceneName;
        loadSceneMode = source.loadSceneMode;
        minimumLoadingScreenSeconds = source.minimumLoadingScreenSeconds;

        if (source.loadingPanel != null)
        {
            loadingPanel = source.loadingPanel;
        }
    }

    private IEnumerator OpenArSceneRoutine()
    {
        isLoading = true;

        if (loadingPanel != null)
        {
            PrepareLoadingPanelForDisplay();
            loadingPanel.SetActive(true);
            loadingPanel.transform.SetAsLastSibling();
        }

        // Start the async scene load before hiding the launching UI branch.
        // The ExploreAR menu card can be deactivated by HideOtherUi(), and if that
        // happens before LoadSceneAsync runs, the coroutine is stopped and the
        // loading panel appears to hang forever.
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(arSceneName, loadSceneMode);
        if (loadOperation == null)
        {
            Debug.LogWarning($"[ARSceneLauncher] Failed to start async load for scene '{arSceneName}'.");
            isLoading = false;
            yield break;
        }

        if (loadingPanel != null)
        {
            HideOtherUi();
        }

        float minimumDelay = Mathf.Max(0f, minimumLoadingScreenSeconds);
        if (minimumDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(minimumDelay);
        }

        while (!loadOperation.isDone)
        {
            yield return null;
        }
    }

    private void HideOtherUi()
    {
        hiddenUiObjects.Clear();

        if (loadingPanel == null)
        {
            return;
        }

        HideUiInCanvas();
        HideUiInLauncherBranch();
        HideOtherSceneUiRoots();
    }

    private void HideUiInCanvas()
    {
        Canvas parentCanvas = loadingPanel.GetComponentInParent<Canvas>(true);
        if (parentCanvas == null)
        {
            return;
        }

        Transform protectedBranch = loadingPanel.transform;
        for (int i = 0; i < parentCanvas.transform.childCount; i++)
        {
            Transform child = parentCanvas.transform.GetChild(i);
            if (child == null || IsSameOrAncestor(child, protectedBranch) || !child.gameObject.activeSelf)
            {
                continue;
            }

            hiddenUiObjects.Add(child.gameObject);
            child.gameObject.SetActive(false);
        }
    }

    private void HideOtherSceneUiRoots()
    {
        GameObject[] rootObjects = gameObject.scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            GameObject rootObject = rootObjects[i];
            if (rootObject == null || !rootObject.activeSelf)
            {
                continue;
            }

            if (loadingPanel.transform.IsChildOf(rootObject.transform))
            {
                continue;
            }

            if (!LooksLikeUiRoot(rootObject))
            {
                continue;
            }

            hiddenUiObjects.Add(rootObject);
            rootObject.SetActive(false);
        }
    }

    private void HideUiInLauncherBranch()
    {
        Transform branchRoot = transform.root;
        if (branchRoot == null)
        {
            return;
        }

        HideOffPathChildren(branchRoot, transform);
    }

    private void HideOffPathChildren(Transform current, Transform target)
    {
        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);
            if (child == null || !child.gameObject.activeSelf)
            {
                continue;
            }

            if (child == target || target.IsChildOf(child))
            {
                HideOffPathChildren(child, target);
                continue;
            }

            if (!LooksLikeUiRoot(child.gameObject))
            {
                continue;
            }

            hiddenUiObjects.Add(child.gameObject);
            child.gameObject.SetActive(false);
        }
    }

    private static bool LooksLikeUiRoot(GameObject rootObject)
    {
        if (rootObject.GetComponentInChildren<Canvas>(true) != null)
        {
            return true;
        }

        if (rootObject.GetComponentInChildren<GraphicRaycaster>(true) != null)
        {
            return true;
        }

        if (rootObject.GetComponentInChildren<Graphic>(true) != null)
        {
            return true;
        }

        if (rootObject.GetComponentInChildren<TMP_Text>(true) != null)
        {
            return true;
        }

        string name = rootObject.name;
        return name.Contains("UI") || name.Contains("Menu") || name.Contains("Canvas");
    }

    private static bool IsSameOrAncestor(Transform candidate, Transform target)
    {
        return candidate == target || target.IsChildOf(candidate);
    }

    private void PrepareLoadingPanelForDisplay()
    {
        if (loadingPanel == null)
        {
            return;
        }

        Canvas parentCanvas = loadingPanel.GetComponentInParent<Canvas>(true);
        if (parentCanvas == null)
        {
            return;
        }

        parentCanvas.gameObject.SetActive(true);

        RectTransform canvasRect = parentCanvas.transform as RectTransform;
        if (canvasRect != null && canvasRect.localScale.sqrMagnitude < 0.01f)
        {
            canvasRect.localScale = Vector3.one;
        }
    }

    private GameObject FindLoadingPanel()
    {
        Transform directChild = transform.root != null ? transform.root.Find("ARLoadingPanel") : null;
        if (directChild != null)
        {
            return directChild.gameObject;
        }

        GameObject sceneObject = GameObject.Find("ARLoadingPanel");
        if (sceneObject != null)
        {
            return sceneObject;
        }

        Transform[] sceneTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < sceneTransforms.Length; i++)
        {
            Transform candidate = sceneTransforms[i];
            if (candidate == null || candidate.name != "ARLoadingPanel")
            {
                continue;
            }

            GameObject candidateObject = candidate.gameObject;
            if (!candidateObject.scene.IsValid() || candidateObject.scene != gameObject.scene)
            {
                continue;
            }

            return candidateObject;
        }

        Debug.LogWarning("[ARSceneLauncher] Could not find 'ARLoadingPanel'. Assign it in the Inspector.");
        return null;
    }
}
