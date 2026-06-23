using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ARSceneUIController : MonoBehaviour
{
    [SerializeField] private string fallbackReturnSceneName = "SolarSystemScene";
    [SerializeField] private GameObject solarSystemInfoPanel;
    [SerializeField] private GameObject celestialBodyInfoPanel;
    [SerializeField] private Button infoButton;
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject instructionTextObject;
    [SerializeField] private TMP_Text instructionTextTmp;
    [SerializeField] private Text instructionTextLegacy;
    [SerializeField] private string titleTextObjectName = "TitleText";
    [SerializeField] private string subtitleTextObjectName = "Subtitle";
    [SerializeField] private string infoTextObjectName = "InfoText";

    private readonly Dictionary<string, GameObject> specificImageTargets = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
    private readonly List<GameObject> genericImageTargets = new List<GameObject>();
    private readonly List<Button> closeInfoButtons = new List<Button>();
    private bool launchContextInitialized;
    private bool useSpecificSelectedBody;
    private string selectedBodyName;
    private string selectedBodyTitle;
    private string selectedBodyDescription;
    private string activeTrackedBodyName;
    private const string GenericTitle = "The Solar System";
    private const string GenericInfo =
        "The Solar System is made up of the Sun and all the objects that orbit around it, including planets, moons, asteroids, comets, and other space materials.";
    private static readonly string[] GenericTargetNames =
    {
        "ImageTarget",
        "ImageTarget_SolarSystem"
    };

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

        for (int i = 0; i < closeInfoButtons.Count; i++)
        {
            Button closeButton = closeInfoButtons[i];
            if (closeButton == null)
            {
                continue;
            }

            closeButton.onClick.RemoveListener(HideInfoPanel);
            closeButton.onClick.AddListener(HideInfoPanel);
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

        for (int i = 0; i < closeInfoButtons.Count; i++)
        {
            Button closeButton = closeInfoButtons[i];
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HideInfoPanel);
            }
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(ReturnFromAr);
        }
    }

    public void ShowInfoPanel()
    {
        if (activeTrackedBodyName == null)
        {
            return;
        }

        if (!useSpecificSelectedBody && string.IsNullOrWhiteSpace(activeTrackedBodyName))
        {
            ShowSolarSystemInfoPanel();
            return;
        }

        ShowCelestialBodyInfoPanel();
    }

    public void HideInfoPanel()
    {
        HideAllInfoPanels();
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
        CacheSceneTargets();
        InitializeLaunchContextIfNeeded();
        HideAllInfoPanels();
        ShowInstructionText();
    }

    public void HandleTrackedBodyFound(string bodyName)
    {
        string normalizedBodyName = ARBodySelectionContext.NormalizeBodyKey(bodyName);
        activeTrackedBodyName = normalizedBodyName;

        if (useSpecificSelectedBody)
        {
            ApplyBodyTexts(selectedBodyTitle, selectedBodyName, selectedBodyDescription);
            ShowCelestialBodyInfoPanel();
            return;
        }

        if (string.IsNullOrWhiteSpace(normalizedBodyName))
        {
            ApplyGenericTexts();
            ShowSolarSystemInfoPanel();
            return;
        }

        string displayName = ARBodySelectionContext.FormatDisplayName(normalizedBodyName);
        ApplyBodyTexts(displayName, normalizedBodyName, GetFallbackDescription(normalizedBodyName));
        ShowCelestialBodyInfoPanel();
    }

    public void HandleTrackedBodyLost(string bodyName)
    {
        string normalizedBodyName = ARBodySelectionContext.NormalizeBodyKey(bodyName);
        if (!string.IsNullOrWhiteSpace(activeTrackedBodyName) &&
            !string.Equals(activeTrackedBodyName, normalizedBodyName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        activeTrackedBodyName = null;
        HideAllInfoPanels();

        if (useSpecificSelectedBody)
        {
            ApplyBodyTexts(selectedBodyTitle, selectedBodyName, selectedBodyDescription);
            string instruction = string.IsNullOrWhiteSpace(selectedBodyTitle)
                ? "Point your camera at the marker"
                : $"Point your camera at the {selectedBodyTitle} marker";
            SetInstructionText(instruction);
            return;
        }

        ApplyGenericTexts();
        SetInstructionText("Point your camera at the marker");
    }

    private void AutoAssignReferences()
    {
        Transform canvasRoot = transform;

        if (solarSystemInfoPanel == null)
        {
            Transform panel = FindChildRecursive(canvasRoot, "SolarSystemInfoPanel");
            if (panel != null)
            {
                solarSystemInfoPanel = panel.gameObject;
            }
        }

        if (solarSystemInfoPanel == null)
        {
            Transform legacyPanel = FindChildRecursive(canvasRoot, "InfoPanel");
            if (legacyPanel != null)
            {
                solarSystemInfoPanel = legacyPanel.gameObject;
            }
        }

        if (celestialBodyInfoPanel == null)
        {
            Transform panel = FindChildRecursive(canvasRoot, "CelestialBodyInfoPanel");
            if (panel != null)
            {
                celestialBodyInfoPanel = panel.gameObject;
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

        if (instructionTextTmp == null && instructionTextObject != null)
        {
            instructionTextTmp = instructionTextObject.GetComponent<TMP_Text>();
        }

        if (instructionTextLegacy == null && instructionTextObject != null)
        {
            instructionTextLegacy = instructionTextObject.GetComponent<Text>();
        }

        CacheCloseButtons();
        PreparePanelAutoSizing();
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

    private void CacheSceneTargets()
    {
        specificImageTargets.Clear();
        genericImageTargets.Clear();

        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject sceneObject = sceneObjects[i];
            if (sceneObject == null || !sceneObject.scene.IsValid() || sceneObject.scene != gameObject.scene)
            {
                continue;
            }

            if (IsGenericTargetName(sceneObject.name))
            {
                genericImageTargets.Add(sceneObject);
                continue;
            }

            if (!sceneObject.name.StartsWith("ImageTarget_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string bodyName = ResolveBodyNameFromTargetName(sceneObject.name);
            if (string.IsNullOrWhiteSpace(bodyName))
            {
                continue;
            }

            specificImageTargets[bodyName] = sceneObject;
        }
    }

    private void InitializeLaunchContextIfNeeded()
    {
        if (launchContextInitialized)
        {
            return;
        }

        launchContextInitialized = true;

        ARBodySelectionContext.SelectionSnapshot selection = ARBodySelectionContext.Capture(true);
        string normalizedSelectedBody = ARBodySelectionContext.NormalizeBodyKey(selection.BodyName);
        bool hasSpecificTarget = selection.HasSelection && specificImageTargets.ContainsKey(normalizedSelectedBody);

        if (hasSpecificTarget)
        {
            useSpecificSelectedBody = true;
            selectedBodyName = normalizedSelectedBody;
            selectedBodyTitle = string.IsNullOrWhiteSpace(selection.Title)
                ? ARBodySelectionContext.FormatDisplayName(normalizedSelectedBody)
                : selection.Title;
            selectedBodyDescription = string.IsNullOrWhiteSpace(selection.Description)
                ? GetFallbackDescription(selectedBodyTitle)
                : selection.Description;

            SetTargetVisibility(selectedBodyName);
            ApplyBodyTexts(selectedBodyTitle, selectedBodyName, selectedBodyDescription);
            SetInstructionText($"Point your camera at the {selectedBodyTitle} marker");
            return;
        }

        if (selection.HasSelection && !string.IsNullOrWhiteSpace(normalizedSelectedBody))
        {
            Debug.LogWarning($"[ARSceneUIController] No matching image target was found for '{selection.BodyName}'. Falling back to generic AR mode.");
        }

        useSpecificSelectedBody = false;
        selectedBodyName = null;
        selectedBodyTitle = null;
        selectedBodyDescription = null;
        SetTargetVisibility(null);
        ApplyGenericTexts();
        SetInstructionText("Point your camera at the marker");
    }

    private void SetTargetVisibility(string requiredBodyName)
    {
        bool isGenericMode = string.IsNullOrWhiteSpace(requiredBodyName);

        for (int i = 0; i < genericImageTargets.Count; i++)
        {
            GameObject genericTarget = genericImageTargets[i];
            if (genericTarget != null)
            {
                genericTarget.SetActive(isGenericMode);
            }
        }

        foreach (KeyValuePair<string, GameObject> entry in specificImageTargets)
        {
            if (entry.Value == null)
            {
                continue;
            }

            bool shouldBeActive =
                string.IsNullOrWhiteSpace(requiredBodyName) ||
                string.Equals(entry.Key, requiredBodyName, StringComparison.OrdinalIgnoreCase);

            entry.Value.SetActive(shouldBeActive);
        }
    }

    private void ApplyGenericTexts()
    {
        SetPanelTexts(solarSystemInfoPanel, GenericTitle, string.Empty, GenericInfo);
    }

    private void ApplyBodyTexts(string title, string bodyName, string description)
    {
        SetPanelTexts(
            celestialBodyInfoPanel,
            string.IsNullOrWhiteSpace(title) ? GenericTitle : title,
            GetBodySubtitle(bodyName),
            string.IsNullOrWhiteSpace(description) ? GenericInfo : description);
    }

    private void SetInstructionText(string value)
    {
        if (instructionTextTmp != null)
        {
            instructionTextTmp.text = value;
        }

        if (instructionTextLegacy != null)
        {
            instructionTextLegacy.text = value;
        }
    }

    private void SetPanelTexts(GameObject panelRoot, string titleValue, string subtitleValue, string infoValue)
    {
        SetNamedText(panelRoot, titleTextObjectName, titleValue);
        SetNamedText(panelRoot, subtitleTextObjectName, subtitleValue);
        SetNamedText(panelRoot, infoTextObjectName, infoValue);

        ARInfoPanelAutoSizer autoSizer = EnsurePanelAutoSizer(panelRoot);
        if (autoSizer != null)
        {
            autoSizer.QueueRefresh();
        }
    }

    private void SetNamedText(GameObject panelRoot, string objectName, string value)
    {
        if (panelRoot == null || string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        Transform[] transforms = panelRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform child = transforms[i];
            if (child == null || child.name != objectName)
            {
                continue;
            }

            TMP_Text tmpText = child.GetComponent<TMP_Text>();
            if (tmpText != null)
            {
                tmpText.text = value;
            }

            Text legacyText = child.GetComponent<Text>();
            if (legacyText != null)
            {
                legacyText.text = value;
            }
        }
    }

    private void CacheCloseButtons()
    {
        closeInfoButtons.Clear();
        CollectCloseButtonsFromPanel(solarSystemInfoPanel);
        CollectCloseButtonsFromPanel(celestialBodyInfoPanel);
    }

    private void CollectCloseButtonsFromPanel(GameObject panelRoot)
    {
        if (panelRoot == null)
        {
            return;
        }

        Transform[] transforms = panelRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform child = transforms[i];
            if (child == null || child.name != "CloseInfoButton")
            {
                continue;
            }

            Button button = child.GetComponent<Button>();
            if (button != null && !closeInfoButtons.Contains(button))
            {
                closeInfoButtons.Add(button);
            }
        }
    }

    private void ShowSolarSystemInfoPanel()
    {
        if (solarSystemInfoPanel != null)
        {
            solarSystemInfoPanel.SetActive(true);
        }

        if (celestialBodyInfoPanel != null)
        {
            celestialBodyInfoPanel.SetActive(false);
        }
    }

    private void ShowCelestialBodyInfoPanel()
    {
        if (celestialBodyInfoPanel != null)
        {
            celestialBodyInfoPanel.SetActive(true);
        }

        if (solarSystemInfoPanel != null)
        {
            solarSystemInfoPanel.SetActive(false);
        }
    }

    private void HideAllInfoPanels()
    {
        if (solarSystemInfoPanel != null)
        {
            solarSystemInfoPanel.SetActive(false);
        }

        if (celestialBodyInfoPanel != null)
        {
            celestialBodyInfoPanel.SetActive(false);
        }
    }

    private void PreparePanelAutoSizing()
    {
        EnsurePanelAutoSizer(solarSystemInfoPanel);
        EnsurePanelAutoSizer(celestialBodyInfoPanel);
    }

    private ARInfoPanelAutoSizer EnsurePanelAutoSizer(GameObject panelRoot)
    {
        if (panelRoot == null)
        {
            return null;
        }

        ARInfoPanelAutoSizer autoSizer = panelRoot.GetComponent<ARInfoPanelAutoSizer>();
        if (autoSizer == null)
        {
            autoSizer = panelRoot.AddComponent<ARInfoPanelAutoSizer>();
        }

        bool preserveTopEdge = panelRoot == celestialBodyInfoPanel;
        autoSizer.Configure(preserveTopEdge);
        return autoSizer;
    }

    private static string ResolveBodyNameFromTargetName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName) ||
            IsGenericTargetName(targetName) ||
            !targetName.StartsWith("ImageTarget_", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return ARBodySelectionContext.NormalizeBodyKey(targetName.Substring("ImageTarget_".Length));
    }

    private static bool IsGenericTargetName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return false;
        }

        for (int i = 0; i < GenericTargetNames.Length; i++)
        {
            if (string.Equals(targetName, GenericTargetNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetFallbackDescription(string bodyName)
    {
        switch (ARBodySelectionContext.NormalizeBodyKey(bodyName))
        {
            case "sun":
                return "The Sun is the star at the center of the Solar System and the source of light and heat for the planets.";
            case "mercury":
                return "Mercury is the smallest planet and the closest planet to the Sun, with a rocky surface and extreme temperatures.";
            case "venus":
                return "Venus is the hottest planet in the Solar System because of its dense atmosphere and powerful greenhouse effect.";
            case "earth":
                return "Earth is the only known planet that supports life, with liquid water, a protective atmosphere, and diverse ecosystems.";
            case "moon":
                return "The Moon is Earth's natural satellite and affects ocean tides while reflecting sunlight into our night sky.";
            case "mars":
                return "Mars is known as the Red Planet and is a major focus of exploration because of evidence of ancient water.";
            case "jupiter":
                return "Jupiter is the largest planet in the Solar System, famous for its Great Red Spot and many moons.";
            case "saturn":
                return "Saturn is a gas giant best known for its bright ring system made of ice, dust, and rocky material.";
            case "uranus":
                return "Uranus is an ice giant that rotates on its side, giving it one of the most unusual spins in the Solar System.";
            case "neptune":
                return "Neptune is a distant ice giant with strong winds, deep blue color, and an orbit far from the Sun.";
            default:
                return GenericInfo;
        }
    }

    private static string GetBodySubtitle(string bodyName)
    {
        switch (ARBodySelectionContext.NormalizeBodyKey(bodyName))
        {
            case "sun":
                return "The Star of Our Solar System";
            case "mercury":
                return "The Swiftest Planet";
            case "venus":
                return "The Hottest Planet";
            case "earth":
                return "Our Home Planet";
            case "moon":
                return "Earth's Natural Satellite";
            case "mars":
                return "The Red Planet";
            case "jupiter":
                return "The Largest Planet";
            case "saturn":
                return "The Ringed Giant";
            case "uranus":
                return "The Tilted Ice Giant";
            case "neptune":
                return "The Windy Ice Giant";
            default:
                return string.Empty;
        }
    }
}
