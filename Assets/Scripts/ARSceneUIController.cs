using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ARSceneUIController : MonoBehaviour
{
    [SerializeField] private string fallbackReturnSceneName = "SolarSystemScene";
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Button infoButton;
    [SerializeField] private Button closeInfoButton;
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject instructionTextObject;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "SolarSystemARScene")
        {
            return;
        }

        GameObject canvasObject = GameObject.Find("ARCanvas");
        if (canvasObject == null)
        {
            return;
        }

        ARSceneUIController controller = canvasObject.GetComponent<ARSceneUIController>();
        if (controller == null)
        {
            controller = canvasObject.AddComponent<ARSceneUIController>();
        }

        controller.Initialize();
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();

        if (infoButton != null)
        {
            infoButton.onClick.RemoveListener(ShowInfoPanel);
            infoButton.onClick.AddListener(ShowInfoPanel);
        }

        if (closeInfoButton != null)
        {
            closeInfoButton.onClick.RemoveListener(HideInfoPanel);
            closeInfoButton.onClick.AddListener(HideInfoPanel);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(ReturnFromAr);
            backButton.onClick.AddListener(ReturnFromAr);
        }
    }

    private void OnDisable()
    {
        if (infoButton != null)
        {
            infoButton.onClick.RemoveListener(ShowInfoPanel);
        }

        if (closeInfoButton != null)
        {
            closeInfoButton.onClick.RemoveListener(HideInfoPanel);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(ReturnFromAr);
        }
    }

    public void ShowInfoPanel()
    {
        if (infoPanel != null)
        {
            infoPanel.SetActive(true);
        }
    }

    public void HideInfoPanel()
    {
        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }
    }

    public void ReturnFromAr()
    {
        string sceneName = string.IsNullOrWhiteSpace(ARSceneLauncher.LastNonArSceneName)
            ? fallbackReturnSceneName
            : ARSceneLauncher.LastNonArSceneName;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"[ARSceneUIController] Scene '{sceneName}' is not in Build Settings.");
            return;
        }

        SceneReturnCoordinator.RequestReturn(sceneName, "Going back to solar system scene");
    }

    public void ShowInstructionText()
    {
        if (instructionTextObject != null)
        {
            instructionTextObject.SetActive(true);
        }
    }

    public void HideInstructionText()
    {
        if (instructionTextObject != null)
        {
            instructionTextObject.SetActive(false);
        }
    }

    public void Initialize()
    {
        AutoAssignReferences();
        HideInfoPanel();
        ShowInstructionText();
    }

    private void AutoAssignReferences()
    {
        Transform canvasRoot = transform;

        if (infoPanel == null)
        {
            Transform panel = FindChildRecursive(canvasRoot, "InfoPanel");
            if (panel != null)
            {
                infoPanel = panel.gameObject;
            }
        }

        if (infoButton == null)
        {
            Transform buttonObject = FindChildRecursive(canvasRoot, "InfoButton");
            if (buttonObject != null)
            {
                infoButton = buttonObject.GetComponent<Button>();
            }
        }

        if (closeInfoButton == null)
        {
            Transform buttonObject = FindChildRecursive(canvasRoot, "CloseInfoButton");
            if (buttonObject != null)
            {
                closeInfoButton = buttonObject.GetComponent<Button>();
            }
        }

        if (backButton == null)
        {
            Transform topBar = FindChildRecursive(canvasRoot, "TopBar");
            Transform buttonObject = topBar != null ? FindChildRecursive(topBar, "BackButton") : FindChildRecursive(canvasRoot, "BackButton");
            if (buttonObject != null)
            {
                backButton = buttonObject.GetComponent<Button>();
            }
        }

        if (instructionTextObject == null)
        {
            Transform instruction = FindChildRecursive(canvasRoot, "InstructionText");
            if (instruction != null)
            {
                instructionTextObject = instruction.gameObject;
            }
        }
    }

    private static Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == targetName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
