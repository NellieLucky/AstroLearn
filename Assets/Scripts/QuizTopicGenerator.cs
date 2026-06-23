using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

[System.Serializable]
public class QuizTopicData
{
    public string topicName;
    public Sprite topicSprite;
}

public class QuizTopicGenerator : MonoBehaviour
{
    [Header("Data Setup")]
    public List<QuizTopicData> quizTopics = new List<QuizTopicData>();

    [Header("UI References")]
    public Transform topicContainer;        // The layout group (e.g. Horizontal/Grid Layout) that will hold the buttons
    public GameObject topicButtonPrefab;    // The prefab of the Sun button
    
    [Header("Bottom Panel Selection UI")]
    public TextMeshProUGUI selectedTopicLabel; // The text that currently says "SUN QUIZ"
    public Button easyModeButton;
    public Button hardModeButton;
    public Color selectedOutlineColor = new Color(0.45f, 0.8f, 1f, 1f);
    public Color unselectedOutlineColor = new Color(0.15f, 0.3f, 0.5f, 1f);

    private string currentSelectedTopic;
    private GameObject quizTopicPage;
    private GameObject quizIntroPage;
    private GameObject quizQuestionPage;
    private GameObject quizHomePage;
    private GameObject quizResultPage;
    private GameObject quizHistoryPage;
    private GameObject quizBreakdownPage;
    private Button quizHomeEasyModeButton;
    private Button quizHomeHardModeButton;
    private TextMeshProUGUI quizHomeTopicLabel;
    private TextMeshProUGUI introTextLabel;
    private Button introBackButton;
    private Button introStartQuizButton;
    private string currentDifficulty = "Easy";
    private bool introBackReturnsToQuizHome;
    private readonly Dictionary<string, Button> topicButtonsByName = new Dictionary<string, Button>();
    private Coroutine introLoadingAnimationCoroutine;
    private MonoBehaviour introLoadingAnimationHost;

    private void Start()
    {
        InitializeQuizUi();
    }

    public void RefreshForOpen()
    {
        introBackReturnsToQuizHome = false;
        InitializeQuizUi();
        ShowTopicSelectionPage();
    }

    public void OpenQuizHomeForTopic(string topicName)
    {
        introBackReturnsToQuizHome = true;
        InitializeQuizUi();

        string resolvedTopicName = ResolveAvailableTopicName(topicName);
        if (string.IsNullOrWhiteSpace(resolvedTopicName) && quizTopics.Count > 0)
        {
            resolvedTopicName = quizTopics[0].topicName;
        }

        if (!string.IsNullOrWhiteSpace(resolvedTopicName))
        {
            SelectTopic(resolvedTopicName);
        }

        SetQuizPageState(quizHomePage);
    }

    private void InitializeQuizUi()
    {
        ResolveQuizPageReferences();
        EnsureMoonTopicExists();
        ConfigureSelectedTopicLabel();
        ConfigureIntroTextLabel();
        if (topicContainer != null && topicButtonPrefab != null)
        {
            GenerateTopicButtons();
        }

        // Optional: Pre-select the first topic if the list isn't empty
        if (quizTopics.Count > 0)
        {
            string topicToSelect = currentSelectedTopic;
            if (string.IsNullOrWhiteSpace(topicToSelect) || !quizTopics.Exists(topic => string.Equals(topic.topicName, topicToSelect, System.StringComparison.OrdinalIgnoreCase)))
            {
                topicToSelect = quizTopics[0].topicName;
            }

            SelectTopic(topicToSelect);
        }

        // Hook up the difficulty buttons
        BindDifficultyButton(easyModeButton, "Easy");
        BindDifficultyButton(hardModeButton, "Hard");
        BindDifficultyButton(quizHomeEasyModeButton, "Easy");
        BindDifficultyButton(quizHomeHardModeButton, "Hard");
        if (introBackButton != null)
        {
            introBackButton.onClick.RemoveAllListeners();
            introBackButton.onClick.AddListener(HandleIntroBackPressed);
        }
        if (introStartQuizButton != null)
        {
            introStartQuizButton.onClick.RemoveAllListeners();
            introStartQuizButton.onClick.AddListener(HandleStartQuizPressed);
        }

        string resolvedTopicName = GetResolvedSelectedTopic();
        if (!string.IsNullOrWhiteSpace(resolvedTopicName))
        {
            RestoreIntroPrompt(resolvedTopicName);
        }

        UpdateDifficultyButtonVisuals();
    }

    private void EnsureMoonTopicExists()
    {
        if (quizTopics == null)
        {
            quizTopics = new List<QuizTopicData>();
        }

        if (!quizTopics.Exists(t => string.Equals(t.topicName, "Moon", System.StringComparison.OrdinalIgnoreCase)))
        {
            Sprite moonSprite = null;
            CelestialBody[] bodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
            foreach (CelestialBody body in bodies)
            {
                if (body != null && string.Equals(body.bodyName, "Moon", System.StringComparison.OrdinalIgnoreCase))
                {
                    moonSprite = body.profileImage;
                    break;
                }
            }

            if (moonSprite != null)
            {
                QuizTopicData moonTopic = new QuizTopicData
                {
                    topicName = "Moon",
                    topicSprite = moonSprite
                };

                int earthIndex = quizTopics.FindIndex(t => string.Equals(t.topicName, "Earth", System.StringComparison.OrdinalIgnoreCase));
                if (earthIndex >= 0)
                {
                    quizTopics.Insert(earthIndex + 1, moonTopic);
                }
                else
                {
                    quizTopics.Add(moonTopic);
                }
            }
        }
    }

    private void GenerateTopicButtons()
    {
        // Optional: clear existing children (if you have placeholders)
        foreach (Transform child in topicContainer)
        {
            Destroy(child.gameObject);
        }

        topicButtonsByName.Clear();

        // Loop through the list of topics and generate a button for each
        foreach (QuizTopicData topic in quizTopics)
        {
            // Create a new button instance
            GameObject newButtonObj = Instantiate(topicButtonPrefab, topicContainer);
            newButtonObj.SetActive(true); // Make sure it's active
            
            // You might need to change the component types here depending on your prefab setup.
            // Your prefab has an image child named "TopicImage" for the planet sprite
            Image iconImage = newButtonObj.transform.Find("TopicImage")?.GetComponent<Image>();
            
            // Fallback in case the structure changes
            if (iconImage == null)
            {
                // Find all images, skip the root background image if possible
                Image[] images = newButtonObj.GetComponentsInChildren<Image>();
                if (images.Length > 1) iconImage = images[1];
                else iconImage = newButtonObj.GetComponentInChildren<Image>();
            }
            
            if (iconImage != null && topic.topicSprite != null)
            {
                iconImage.sprite = topic.topicSprite;
            }

            // Find the Text component. In your prefab it is deeply nested under TopicLabelPanel -> TopicLabel
            TextMeshProUGUI titleText = FindText(newButtonObj, "TopicLabel") ?? newButtonObj.GetComponentInChildren<TextMeshProUGUI>();
            
            if (titleText != null)
            {
                titleText.text = topic.topicName.ToUpper();
            }

            // Hook up the click event dynamically!
            Button buttonComponent = newButtonObj.GetComponent<Button>() ?? newButtonObj.GetComponentInChildren<Button>();
            if (buttonComponent != null)
            {
                // We capture the current topic name in a local variable to avoid closure issues
                string topicName = topic.topicName;
                topicButtonsByName[topicName] = buttonComponent;
                buttonComponent.onClick.RemoveAllListeners();
                buttonComponent.onClick.AddListener(() => SelectTopic(topicName));
            }
        }
    }

    private void SelectTopic(string topicName)
    {
        currentSelectedTopic = topicName;

        UpdateSelectedTopicLabels(topicName);

        UpdateTopicButtonVisuals();
        
        Debug.Log($"[QuizTopicGenerator] Selected topic changed to: {topicName}");
    }

    private void UpdateSelectedTopicLabels(string topicName)
    {
        if (selectedTopicLabel != null)
        {
            ConfigureSelectedTopicLabel();
            selectedTopicLabel.text = topicName.ToUpper();
        }

        if (quizHomeTopicLabel != null)
        {
            quizHomeTopicLabel.text = topicName;
        }
    }

    private string ResolveAvailableTopicName(string topicName)
    {
        if (string.IsNullOrWhiteSpace(topicName))
        {
            return currentSelectedTopic;
        }

        string normalizedTopicName = topicName.Trim();
        for (int i = 0; i < quizTopics.Count; i++)
        {
            QuizTopicData topic = quizTopics[i];
            if (topic != null && string.Equals(topic.topicName, normalizedTopicName, System.StringComparison.OrdinalIgnoreCase))
            {
                return topic.topicName;
            }
        }

        return normalizedTopicName;
    }

    private void StartQuiz(string topicName, string difficulty)
    {
        string resolvedTopicName = GetResolvedSelectedTopic(topicName);

        Debug.Log($"[QuizTopicGenerator] Starting {difficulty} mode for {resolvedTopicName}!");

        if (string.IsNullOrWhiteSpace(resolvedTopicName))
        {
            return;
        }

        currentDifficulty = string.IsNullOrWhiteSpace(difficulty) ? "Easy" : difficulty;
        UpdateDifficultyButtonVisuals();

        if (introTextLabel != null)
        {
            introTextLabel.text = BuildIntroPrompt(resolvedTopicName, currentDifficulty);
        }

        ShowIntroPage();
    }

    private void HandleStartQuizPressed()
    {
        string resolvedTopicName = GetResolvedSelectedTopic();
        if (string.IsNullOrWhiteSpace(resolvedTopicName))
        {
            return;
        }

        if (introStartQuizButton != null)
        {
            introStartQuizButton.interactable = false;
        }

        if (introTextLabel != null)
        {
            StartIntroLoadingAnimation(resolvedTopicName);
        }

        if (QuizFlowController.Instance == null)
        {
            Debug.LogWarning("[QuizTopicGenerator] QuizFlowController was not found. Falling back to direct page navigation.");
            ShowQuestionPage();
            RestoreIntroPrompt(resolvedTopicName);
            return;
        }

        QuizFlowController.Instance.BeginQuiz(resolvedTopicName, currentDifficulty, (success, message) => RestoreIntroPrompt(resolvedTopicName));
    }

    private void RestoreIntroPrompt(string topicName)
    {
        StopIntroLoadingAnimation();

        if (introStartQuizButton != null)
        {
            introStartQuizButton.interactable = true;
        }

        if (introTextLabel != null)
        {
            introTextLabel.text = BuildIntroPrompt(topicName, currentDifficulty);
        }
    }

    private void StartIntroLoadingAnimation(string topicName)
    {
        StopIntroLoadingAnimation();

        string resolvedTopicName = GetResolvedSelectedTopic(topicName);
        string resolvedDifficulty = NormalizeDifficulty(currentDifficulty);
        MonoBehaviour coroutineHost = ResolveIntroLoadingAnimationHost();
        if (coroutineHost == null)
        {
            if (introTextLabel != null)
            {
                introTextLabel.text = BuildGeneratingPrompt(resolvedTopicName, resolvedDifficulty, "...");
            }

            return;
        }

        introLoadingAnimationHost = coroutineHost;
        introLoadingAnimationCoroutine = coroutineHost.StartCoroutine(AnimateIntroLoadingText(resolvedTopicName, resolvedDifficulty));
    }

    private void StopIntroLoadingAnimation()
    {
        if (introLoadingAnimationCoroutine != null && introLoadingAnimationHost != null)
        {
            introLoadingAnimationHost.StopCoroutine(introLoadingAnimationCoroutine);
            introLoadingAnimationCoroutine = null;
        }

        introLoadingAnimationHost = null;
    }

    private IEnumerator AnimateIntroLoadingText(string topicName, string difficulty)
    {
        int dotCount = 0;

        while (true)
        {
            if (introTextLabel != null)
            {
                string ellipsis = new string('.', dotCount + 1);
                introTextLabel.text = BuildGeneratingPrompt(topicName, difficulty, ellipsis);
            }

            dotCount = (dotCount + 1) % 3;
            yield return new WaitForSecondsRealtime(0.45f);
        }
    }

    private void ConfigureSelectedTopicLabel()
    {
        if (selectedTopicLabel == null)
        {
            return;
        }

        selectedTopicLabel.enableAutoSizing = true;
        selectedTopicLabel.fontSizeMin = 24f;
        selectedTopicLabel.fontSizeMax = 60f;
        selectedTopicLabel.textWrappingMode = TextWrappingModes.NoWrap;
        selectedTopicLabel.overflowMode = TextOverflowModes.Ellipsis;
        selectedTopicLabel.alignment = TextAlignmentOptions.Center;
    }

    private void ConfigureIntroTextLabel()
    {
        if (introTextLabel == null)
        {
            return;
        }

        introTextLabel.enableAutoSizing = true;
        introTextLabel.fontSizeMin = 24f;
        introTextLabel.fontSizeMax = 60f;
        introTextLabel.textWrappingMode = TextWrappingModes.Normal;
        introTextLabel.overflowMode = TextOverflowModes.Overflow;
        introTextLabel.alignment = TextAlignmentOptions.Center;
    }

    private void ResolveQuizPageReferences()
    {
        quizTopicPage = FindObjectByName("QuizTopicPage");
        quizIntroPage = FindObjectByName("QuizIntroPage");
        quizQuestionPage = FindObjectByName("QuizQuestionPage");
        quizHomePage = FindObjectByName("QuizHomePage");
        quizResultPage = FindObjectByName("QuizResultPage");
        quizHistoryPage = FindObjectByName("QuizHistoryPage");
        quizBreakdownPage = FindObjectByName("QuizBreakdownPage");
        selectedTopicLabel = FindText(quizTopicPage, "SelectedTopicText") ?? selectedTopicLabel;
        easyModeButton = FindButton(quizTopicPage, "EasyButton") ?? easyModeButton;
        hardModeButton = FindButton(quizTopicPage, "HardButton") ?? hardModeButton;
        quizHomeEasyModeButton = FindButton(quizHomePage, "EasyButton") ?? quizHomeEasyModeButton;
        quizHomeHardModeButton = FindButton(quizHomePage, "HardButton") ?? quizHomeHardModeButton;
        quizHomeTopicLabel = FindText(quizHomePage, "TopicText") ?? FindText(quizHomePage, "TopicName");
        introTextLabel = FindText(quizIntroPage, "PromptText") ?? FindText(quizIntroPage, "IntroText");
        introBackButton = FindButton(quizIntroPage, "BackButton");
        introStartQuizButton = FindButton(quizIntroPage, "StartQuizButton") ?? FindButtonByChildText(quizIntroPage, "START QUIZ");
    }

    private void ShowIntroPage()
    {
        SetQuizPageState(quizIntroPage);
    }

    private void ShowTopicSelectionPage()
    {
        SetQuizPageState(quizTopicPage);
    }

    private void ShowQuizHomePage()
    {
        SetQuizPageState(quizHomePage);
    }

    private void ShowQuestionPage()
    {
        Debug.Log($"[QuizTopicGenerator] Opening QuizQuestionPage. FoundPage={quizQuestionPage != null}");
        SetQuizPageState(quizQuestionPage);
    }

    private void SetQuizPageState(GameObject activePage)
    {
        SetPageActive(quizTopicPage, activePage == quizTopicPage);
        SetPageActive(quizIntroPage, activePage == quizIntroPage);
        SetPageActive(quizQuestionPage, activePage == quizQuestionPage);
        SetPageActive(quizHomePage, activePage == quizHomePage);
        SetPageActive(quizResultPage, activePage == quizResultPage);
        SetPageActive(quizHistoryPage, activePage == quizHistoryPage);
        SetPageActive(quizBreakdownPage, activePage == quizBreakdownPage);
    }

    private string GetResolvedSelectedTopic(string fallbackTopicName = null)
    {
        if (selectedTopicLabel != null)
        {
            string labelTopic = selectedTopicLabel.text != null ? selectedTopicLabel.text.Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(labelTopic) && !string.Equals(labelTopic, "QUIZ", System.StringComparison.OrdinalIgnoreCase))
            {
                return labelTopic;
            }
        }

        if (quizHomeTopicLabel != null)
        {
            string labelTopic = quizHomeTopicLabel.text != null ? quizHomeTopicLabel.text.Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(labelTopic) && !string.Equals(labelTopic, "TOPIC", System.StringComparison.OrdinalIgnoreCase))
            {
                return labelTopic;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentSelectedTopic))
        {
            return currentSelectedTopic;
        }

        return fallbackTopicName;
    }

    private void HandleIntroBackPressed()
    {
        if (introBackReturnsToQuizHome)
        {
            ShowQuizHomePage();
            return;
        }

        ShowTopicSelectionPage();
    }

    private static string BuildIntroPrompt(string topicName, string difficulty)
    {
        string promptTone = string.Equals(difficulty, "Hard", System.StringComparison.OrdinalIgnoreCase)
            ? "challenging"
            : "easy";

        return $"Can you answer {promptTone} questions about {topicName}?";
    }

    private static string BuildGeneratingPrompt(string topicName, string difficulty, string ellipsis)
    {
        string resolvedTopicName = string.IsNullOrWhiteSpace(topicName) ? "this topic" : topicName.Trim();
        string resolvedDifficulty = NormalizeDifficulty(difficulty);
        return $"Generating {resolvedDifficulty} questions about {resolvedTopicName}{ellipsis}";
    }

    private static string NormalizeDifficulty(string difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
        {
            return "Easy";
        }

        string lowered = difficulty.Trim().ToLowerInvariant();
        return char.ToUpper(lowered[0]) + lowered.Substring(1);
    }

    private MonoBehaviour ResolveIntroLoadingAnimationHost()
    {
        if (QuizFlowController.Instance != null && QuizFlowController.Instance.isActiveAndEnabled)
        {
            return QuizFlowController.Instance;
        }

        if (introTextLabel != null)
        {
            MonoBehaviour[] hosts = introTextLabel.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < hosts.Length; i++)
            {
                MonoBehaviour host = hosts[i];
                if (host != null && host.isActiveAndEnabled)
                {
                    return host;
                }
            }
        }

        return isActiveAndEnabled ? this : null;
    }

    private void UpdateTopicButtonVisuals()
    {
        foreach (KeyValuePair<string, Button> entry in topicButtonsByName)
        {
            bool isSelected = string.Equals(entry.Key, currentSelectedTopic, System.StringComparison.OrdinalIgnoreCase);
            SetButtonOutlineState(entry.Value, isSelected);
        }
    }

    private void UpdateDifficultyButtonVisuals()
    {
        SetButtonOutlineState(easyModeButton, string.Equals(currentDifficulty, "Easy", System.StringComparison.OrdinalIgnoreCase));
        SetButtonOutlineState(hardModeButton, string.Equals(currentDifficulty, "Hard", System.StringComparison.OrdinalIgnoreCase));
        SetButtonOutlineState(quizHomeEasyModeButton, string.Equals(currentDifficulty, "Easy", System.StringComparison.OrdinalIgnoreCase));
        SetButtonOutlineState(quizHomeHardModeButton, string.Equals(currentDifficulty, "Hard", System.StringComparison.OrdinalIgnoreCase));
    }

    private void BindDifficultyButton(Button button, string difficulty)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => StartQuiz(currentSelectedTopic, difficulty));
    }

    private void SetButtonOutlineState(Button button, bool isSelected)
    {
        if (button == null)
        {
            return;
        }

        Outline outline = button.GetComponent<Outline>();
        if (outline == null)
        {
            outline = button.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(3f, 3f);
            outline.useGraphicAlpha = true;
        }

        outline.enabled = true;
        outline.effectColor = isSelected ? selectedOutlineColor : unselectedOutlineColor;
    }

    private static TextMeshProUGUI FindText(GameObject root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        TextMeshProUGUI[] textComponents = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI textComponent in textComponents)
        {
            if (textComponent != null && textComponent.gameObject.name == objectName)
            {
                return textComponent;
            }
        }

        return null;
    }

    private static Button FindButton(GameObject root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button != null && button.gameObject.name == objectName)
            {
                return button;
            }
        }

        return null;
    }

    private static void SetPageActive(GameObject page, bool isActive)
    {
        if (page != null)
        {
            page.SetActive(isActive);
        }
    }

    private static Button FindButtonByChildText(GameObject root, string childText)
    {
        if (root == null || string.IsNullOrWhiteSpace(childText))
        {
            return null;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            TextMeshProUGUI textComponent = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (textComponent != null && string.Equals(textComponent.text.Trim(), childText, System.StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }

    private static GameObject FindObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject rootObject in objects)
        {
            if (rootObject == null || rootObject.hideFlags != HideFlags.None)
            {
                continue;
            }

            if (!rootObject.scene.IsValid())
            {
                continue;
            }

            if (rootObject.name == objectName)
            {
                return rootObject;
            }
        }

        return null;
    }
}
