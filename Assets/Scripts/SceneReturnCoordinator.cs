using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneReturnCoordinator : MonoBehaviour
{
    private static SceneReturnCoordinator instance;
    private static string pendingSceneName;
    private static string pendingMessage;
    private static bool hasPendingReturn;
    private AuthUIManager preparedAuthUiManager;
    private bool restoredFocusedBodyView;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        EnsureInstance();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public static void RequestReturn(string sceneName, string message)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[SceneReturnCoordinator] Scene name is empty.");
            return;
        }

        EnsureInstance();
        pendingSceneName = sceneName;
        pendingMessage = string.IsNullOrWhiteSpace(message) ? "Going back" : message;
        hasPendingReturn = true;
        if (sceneName == "SolarSystemScene")
        {
            AuthUIManager.SuppressStartupFlowOnNextStart = true;
            AuthUIManager.ForceSolarSystemUiOnNextStart = !PlanetInfoUI.HasPendingExternalRestoreState();
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject coordinatorObject = new GameObject("SceneReturnCoordinator");
        instance = coordinatorObject.AddComponent<SceneReturnCoordinator>();
        DontDestroyOnLoad(coordinatorObject);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!hasPendingReturn || scene.name != pendingSceneName || instance == null)
        {
            return;
        }

        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.RestoreSolarSystemUiRoutine());
    }

    private IEnumerator RestoreSolarSystemUiRoutine()
    {
        preparedAuthUiManager = null;
        restoredFocusedBodyView = false;

        GameObject launchUiRoot = FindSceneObjectByName("LaunchUI");
        GameObject loadingPanel = FindSceneObjectByName("ARLoadingPanel");
        ARLoadingPanelAnimator loadingAnimator = loadingPanel != null ? loadingPanel.GetComponent<ARLoadingPanelAnimator>() : null;
        TMP_Text tmpText = loadingPanel != null ? FindComponentInChildrenByName<TMP_Text>(loadingPanel.transform, "LoadingText") : null;
        Text legacyText = loadingPanel != null ? FindComponentInChildrenByName<Text>(loadingPanel.transform, "LoadingText") : null;

        if (launchUiRoot != null)
        {
            launchUiRoot.SetActive(true);
            SetLaunchUiChildrenForOverlay(launchUiRoot.transform, loadingPanel);
        }

        if (loadingPanel != null && loadingPanel.transform.parent != null)
        {
            loadingPanel.SetActive(true);
            loadingPanel.transform.SetAsLastSibling();
        }

        HideSolarSystemSceneObjects();

        if (loadingAnimator != null)
        {
            loadingAnimator.SetBaseText(pendingMessage);
        }
        else
        {
            if (tmpText != null)
            {
                tmpText.text = pendingMessage;
            }

            if (legacyText != null)
            {
                legacyText.text = pendingMessage;
            }
        }

        bool restoreCompleted = false;
        try
        {
            yield return StartCoroutine(RestoreSolarSystemViewState());

            // Keep the scene hidden while the loading overlay is visible.
            float enforceUntil = Time.unscaledTime + 0.5f;
            while (Time.unscaledTime < enforceUntil)
            {
                HideSolarSystemSceneObjects();
                yield return null;
            }

            restoreCompleted = true;
        }
        finally
        {
            if (loadingAnimator != null)
            {
                loadingAnimator.ResetBaseText();
            }

            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }

            if (launchUiRoot != null)
            {
                launchUiRoot.SetActive(false);
            }

            if (restoreCompleted)
            {
                if (TryRestoreFocusedBodyView())
                {
                    EnforceFocusedBodySceneObjects();
                }
                else
                {
                    if (preparedAuthUiManager != null)
                    {
                        preparedAuthUiManager.ForceShowSolarSystemUiOnly();
                    }

                    EnforceSolarSystemSceneObjects();
                }
            }
            else
            {
                EnforceSolarSystemSceneObjects();
            }

            hasPendingReturn = false;
            pendingSceneName = null;
            pendingMessage = null;
            preparedAuthUiManager = null;
        }
    }

    private IEnumerator RestoreSolarSystemViewState()
    {
        HideSolarSystemSceneObjects();

        AuthUIManager authUiManager = null;
        const float timeoutSeconds = 2f;
        float startedAt = Time.unscaledTime;

        while (authUiManager == null && Time.unscaledTime - startedAt < timeoutSeconds)
        {
            authUiManager = Object.FindFirstObjectByType<AuthUIManager>();
            if (authUiManager == null)
            {
                yield return null;
            }
        }

        if (authUiManager == null)
        {
            GameObject managerObject = new GameObject("AuthUIManager");
            authUiManager = managerObject.AddComponent<AuthUIManager>();
            yield return null;
        }

        preparedAuthUiManager = authUiManager;

        // Let the scene's own startup complete first, then refresh bindings while the
        // loading overlay is still the only thing on screen.
        yield return null;
        yield return null;

        authUiManager.RefreshSceneBindings();
        HideSolarSystemSceneObjects();

        // Run it once more on the next frame in case another startup callback re-applies scene state.
        yield return null;
        authUiManager.RefreshSceneBindings();
        HideSolarSystemSceneObjects();
    }

    private bool TryRestoreFocusedBodyView()
    {
        if (restoredFocusedBodyView)
        {
            return true;
        }

        if (!PlanetInfoUI.HasPendingExternalRestoreState())
        {
            return false;
        }

        SetActiveIfFound("SolarSystemRoot", true);

        PlanetInfoUI planetInfoUi = Object.FindFirstObjectByType<PlanetInfoUI>();
        if (planetInfoUi == null || !planetInfoUi.RestoreFocusedBodyUiFromExternalNavigation())
        {
            PlanetInfoUI.ClearPendingExternalRestoreState();
            return false;
        }

        restoredFocusedBodyView = true;
        return true;
    }

    private static void EnforceSolarSystemSceneObjects()
    {
        SetActiveIfFound("SolarSystemUI", true);
        SetActiveIfFound("SolarSystemRoot", true);
        SetActiveIfFound("CelestialBodyUI", false);
        SetActiveIfFound("Canvas", true);
        SetActiveIfFound("Menu", false);
        SetActiveIfFound("ChatbotCanvas", false);
    }

    private static void EnforceFocusedBodySceneObjects()
    {
        SetActiveIfFound("SolarSystemUI", false);
        SetActiveIfFound("SolarSystemRoot", true);
        SetActiveIfFound("CelestialBodyUI", true);
        SetActiveIfFound("Canvas", true);
        SetActiveIfFound("Menu", false);
        SetActiveIfFound("ChatbotCanvas", false);
        SetActiveIfFound("QuizManager", false);
    }

    private static void HideSolarSystemSceneObjects()
    {
        SetActiveIfFound("SolarSystemUI", false);
        SetActiveIfFound("SolarSystemRoot", false);
        SetActiveIfFound("CelestialBodyUI", false);
        SetActiveIfFound("Menu", false);
        SetActiveIfFound("ChatbotCanvas", false);
        SetActiveIfFound("QuizManager", false);
        SetActiveIfFound("PlanetInfoCard", false);
        SetActiveIfFound("ImagesGalleryOverlay", false);
        SetActiveIfFound("ImageViewerOverlay", false);
    }

    private static void SetActiveIfFound(string objectName, bool isActive)
    {
        GameObject target = FindSceneObjectByName(objectName);
        if (target != null)
        {
            target.SetActive(isActive);
        }
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != objectName)
            {
                continue;
            }

            if (!candidate.gameObject.scene.IsValid() || candidate.gameObject.scene.name != pendingSceneName)
            {
                continue;
            }

            if (candidate.hideFlags != HideFlags.None)
            {
                continue;
            }

            return candidate.gameObject;
        }

        return null;
    }

    private static T FindComponentInChildrenByName<T>(Transform parent, string targetName) where T : Component
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == targetName)
        {
            return parent.GetComponent<T>();
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            T result = FindComponentInChildrenByName<T>(parent.GetChild(i), targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static void SetLaunchUiChildrenForOverlay(Transform launchUiRoot, GameObject loadingPanel)
    {
        if (launchUiRoot == null)
        {
            return;
        }

        for (int i = 0; i < launchUiRoot.childCount; i++)
        {
            Transform child = launchUiRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            bool shouldStayVisible = loadingPanel != null && child.gameObject == loadingPanel;
            child.gameObject.SetActive(shouldStayVisible);
        }
    }
}
