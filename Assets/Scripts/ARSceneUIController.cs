using System;
using System.Collections.Generic;
using TMPro;
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
    [SerializeField] private TMP_Text instructionTextTmp;
    [SerializeField] private Text instructionTextLegacy;
    [SerializeField] private TMP_Text titleTextTmp;
    [SerializeField] private Text titleTextLegacy;
    [SerializeField] private TMP_Text infoTextTmp;
    [SerializeField] private Text infoTextLegacy;

    private readonly Dictionary<string, GameObject> specificImageTargets = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
    private readonly List<GameObject> genericImageTargets = new List<GameObject>();
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
        CacheSceneTargets();
        InitializeLaunchContextIfNeeded();
        HideInfoPanel();
        ShowInstructionText();
    }

    public void HandleTrackedBodyFound(string bodyName)
    {
        string normalizedBodyName = ARBodySelectionContext.NormalizeBodyKey(bodyName);
        activeTrackedBodyName = normalizedBodyName;

        if (useSpecificSelectedBody)
        {
            ApplyBodyTexts(selectedBodyTitle, selectedBodyDescription);
            return;
        }

        if (string.IsNullOrWhiteSpace(normalizedBodyName))
        {
            ApplyGenericTexts();
            return;
        }

        string displayName = ARBodySelectionContext.FormatDisplayName(normalizedBodyName);
        ApplyBodyTexts(displayName, GetFallbackDescription(displayName));
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

        if (useSpecificSelectedBody)
        {
            ApplyBodyTexts(selectedBodyTitle, selectedBodyDescription);
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

        if (instructionTextTmp == null && instructionTextObject != null)
        {
            instructionTextTmp = instructionTextObject.GetComponent<TMP_Text>();
        }

        if (instructionTextLegacy == null && instructionTextObject != null)
        {
            instructionTextLegacy = instructionTextObject.GetComponent<Text>();
        }

        if (titleTextTmp == null)
        {
            Transform title = FindChildRecursive(canvasRoot, "TitleText");
            if (title != null)
            {
                titleTextTmp = title.GetComponent<TMP_Text>();
                titleTextLegacy = titleTextLegacy == null ? title.GetComponent<Text>() : titleTextLegacy;
            }
        }

        if (titleTextLegacy == null)
        {
            Transform title = FindChildRecursive(canvasRoot, "TitleText");
            if (title != null)
            {
                titleTextLegacy = title.GetComponent<Text>();
            }
        }

        if (infoTextTmp == null)
        {
            Transform info = FindChildRecursive(canvasRoot, "InfoText");
            if (info != null)
            {
                infoTextTmp = info.GetComponent<TMP_Text>();
                infoTextLegacy = infoTextLegacy == null ? info.GetComponent<Text>() : infoTextLegacy;
            }
        }

        if (infoTextLegacy == null)
        {
            Transform info = FindChildRecursive(canvasRoot, "InfoText");
            if (info != null)
            {
                infoTextLegacy = info.GetComponent<Text>();
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
            ApplyBodyTexts(selectedBodyTitle, selectedBodyDescription);
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
        ApplyBodyTexts(GenericTitle, GenericInfo);
    }

    private void ApplyBodyTexts(string title, string description)
    {
        SetTitleText(string.IsNullOrWhiteSpace(title) ? GenericTitle : title);
        SetInfoText(string.IsNullOrWhiteSpace(description) ? GenericInfo : description);
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

    private void SetTitleText(string value)
    {
        if (titleTextTmp != null)
        {
            titleTextTmp.text = value;
        }

        if (titleTextLegacy != null)
        {
            titleTextLegacy.text = value;
        }
    }

    private void SetInfoText(string value)
    {
        if (infoTextTmp != null)
        {
            infoTextTmp.text = value;
        }

        if (infoTextLegacy != null)
        {
            infoTextLegacy.text = value;
        }
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
}
