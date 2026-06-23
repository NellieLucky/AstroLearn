using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GuestQuizHistoryController : MonoBehaviour
{
    private static GuestQuizHistoryController instance;

    private static readonly string[] TopicImageNames = { "TopicImage", "ThumbnailImage", "CelestialBodyImage", "Image" };

    private GameObject quizHistoryPage;
    private GameObject historyCardTemplate;
    private RectTransform historyContent;
    private GridLayoutGroup historyGrid;
    private TextMeshProUGUI emptyHistoryText;
    private readonly List<GameObject> generatedCards = new List<GameObject>();
    private readonly Dictionary<string, Sprite> topicSprites = new Dictionary<string, Sprite>();
    private bool wasHistoryPageActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject(nameof(GuestQuizHistoryController));
        instance = controllerObject.AddComponent<GuestQuizHistoryController>();
        DontDestroyOnLoad(controllerObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        ResolveReferences();
        RefreshHistory();
    }

    private void Update()
    {
        if (quizHistoryPage == null || historyCardTemplate == null || historyContent == null)
        {
            ResolveReferences();
        }

        bool isActive = quizHistoryPage != null && quizHistoryPage.activeInHierarchy;
        if (isActive && !wasHistoryPageActive)
        {
            RefreshHistory();
        }

        wasHistoryPageActive = isActive;
    }

    public void RefreshHistory()
    {
        ResolveReferences();
        if (historyCardTemplate == null || historyContent == null)
        {
            return;
        }

        ClearGeneratedCards();

        QuizHistoryCollection history = GuestQuizStorage.LoadHistory();
        List<QuizHistoryEntry> entries = history != null && history.entries != null
            ? new List<QuizHistoryEntry>(history.entries)
            : new List<QuizHistoryEntry>();

        entries.Sort(CompareHistoryEntriesDescending);

        bool hasEntries = entries.Count > 0;

        if (emptyHistoryText != null)
        {
            emptyHistoryText.text = hasEntries
                ? string.Empty
                : "No quiz history saved on this device yet.";
            emptyHistoryText.gameObject.SetActive(!hasEntries);
        }

        historyCardTemplate.SetActive(hasEntries);
        if (!hasEntries)
        {
            ResizeContent(0);
            return;
        }

        BindCard(historyCardTemplate, entries[0]);

        for (int i = 1; i < entries.Count; i++)
        {
            GameObject clone = Instantiate(historyCardTemplate, historyContent);
            clone.name = "HistoryCard_" + i;
            clone.SetActive(true);
            BindCard(clone, entries[i]);
            generatedCards.Add(clone);
        }

        ResizeContent(entries.Count);
        LayoutRebuilder.ForceRebuildLayoutImmediate(historyContent);
        Canvas.ForceUpdateCanvases();
    }

    private void ResolveReferences()
    {
        if (quizHistoryPage == null)
        {
            quizHistoryPage = FindObjectByName("QuizHistoryPage");
        }

        if (emptyHistoryText == null)
        {
            emptyHistoryText = FindText(quizHistoryPage, "EmptyHistoryText") ?? FindTextGlobal("EmptyHistoryText");
        }

        if (historyCardTemplate == null)
        {
            historyCardTemplate = FindChildObject(quizHistoryPage, "HistoryCard") ?? FindObjectByName("HistoryCard");
        }

        if (historyCardTemplate != null)
        {
            RectTransform templateTransform = historyCardTemplate.GetComponent<RectTransform>();
            if (templateTransform != null)
            {
                historyContent = templateTransform.parent as RectTransform;
                if (historyGrid == null && historyContent != null)
                {
                    historyGrid = historyContent.GetComponent<GridLayoutGroup>();
                }
            }
        }

        if (topicSprites.Count == 0)
        {
            CacheTopicSprites();
        }
    }

    private void BindCard(GameObject card, QuizHistoryEntry entry)
    {
        if (card == null || entry == null)
        {
            return;
        }

        TextMeshProUGUI topicText = FindText(card, "TopicName");
        TextMeshProUGUI difficultyText = FindText(card, "Difficulty") ?? FindText(card, "DifficultyText");
        TextMeshProUGUI scoreText = FindText(card, "QuizScore");
        TextMeshProUGUI dateText = FindText(card, "DateTaken");

        if (topicText != null)
        {
            topicText.text = FormatTopic(entry.topic);
        }

        if (difficultyText != null)
        {
            difficultyText.text = FormatDifficulty(entry.difficulty);
        }

        if (scoreText != null)
        {
            scoreText.text = $"{entry.score} out of {Mathf.Max(1, entry.totalQuestions)}";
        }

        if (dateText != null)
        {
            dateText.text = FormatCompletedAt(entry.completedAtUtc);
        }

        Image topicImage = FindTopicImage(card);
        Sprite topicSprite = ResolveTopicSprite(entry.topic);
        if (topicImage != null && topicSprite != null)
        {
            topicImage.sprite = topicSprite;
            topicImage.preserveAspect = true;
        }

        Button cardButton = card.GetComponent<Button>();
        if (cardButton == null)
        {
            cardButton = card.AddComponent<Button>();
        }

        cardButton.transition = Selectable.Transition.ColorTint;
        cardButton.targetGraphic = card.GetComponent<Image>();
        cardButton.onClick.RemoveAllListeners();
        cardButton.onClick.AddListener(() => HandleHistoryCardSelected(entry));
    }

    private void CacheTopicSprites()
    {
        topicSprites.Clear();

        CelestialBody[] bodies = Resources.FindObjectsOfTypeAll<CelestialBody>();
        for (int i = 0; i < bodies.Length; i++)
        {
            CelestialBody body = bodies[i];
            if (body == null || string.IsNullOrWhiteSpace(body.bodyName) || body.profileImage == null)
            {
                continue;
            }

            string key = NormalizeTopicKey(body.bodyName);
            if (!topicSprites.ContainsKey(key))
            {
                topicSprites.Add(key, body.profileImage);
            }
        }
    }

    private Sprite ResolveTopicSprite(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        if (topicSprites.Count == 0)
        {
            CacheTopicSprites();
        }

        topicSprites.TryGetValue(NormalizeTopicKey(topic), out Sprite sprite);
        return sprite;
    }

    private static Image FindTopicImage(GameObject card)
    {
        if (card == null)
        {
            return null;
        }

        for (int i = 0; i < TopicImageNames.Length; i++)
        {
            Image namedImage = FindImage(card, TopicImageNames[i]);
            if (namedImage != null && namedImage.gameObject != card)
            {
                return namedImage;
            }
        }

        Image[] images = card.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image candidate = images[i];
            if (candidate != null && candidate.gameObject != card)
            {
                return candidate;
            }
        }

        return null;
    }

    private void ClearGeneratedCards()
    {
        for (int i = 0; i < generatedCards.Count; i++)
        {
            if (generatedCards[i] != null)
            {
                Destroy(generatedCards[i]);
            }
        }

        generatedCards.Clear();
    }

    private void ResizeContent(int itemCount)
    {
        if (historyContent == null || historyGrid == null)
        {
            return;
        }

        int columns = Mathf.Max(1, historyGrid.constraintCount);
        int rows = Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)columns));
        if (itemCount <= 0)
        {
            rows = 1;
        }

        float height = historyGrid.padding.top + historyGrid.padding.bottom;
        height += rows * historyGrid.cellSize.y;
        height += Mathf.Max(0, rows - 1) * historyGrid.spacing.y;

        Vector2 sizeDelta = historyContent.sizeDelta;
        sizeDelta.y = height;
        historyContent.sizeDelta = sizeDelta;
    }

    private static int CompareHistoryEntriesDescending(QuizHistoryEntry left, QuizHistoryEntry right)
    {
        DateTime leftTime = ParseDateTime(left != null ? left.completedAtUtc : null);
        DateTime rightTime = ParseDateTime(right != null ? right.completedAtUtc : null);
        return rightTime.CompareTo(leftTime);
    }

    private static string FormatTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return "Unknown Topic";
        }

        string lowered = topic.Trim().ToLowerInvariant();
        return char.ToUpper(lowered[0]) + lowered.Substring(1);
    }

    private static string FormatCompletedAt(string completedAtUtc)
    {
        DateTime parsed = ParseDateTime(completedAtUtc);
        if (parsed == DateTime.MinValue)
        {
            return "Date unavailable";
        }

        return parsed.ToLocalTime().ToString("MMMM dd, yyyy h:mm tt");
    }

    private static string FormatDifficulty(string difficulty)
    {
        if (string.IsNullOrWhiteSpace(difficulty))
        {
            return "Easy";
        }

        string lowered = difficulty.Trim().ToLowerInvariant();
        return char.ToUpper(lowered[0]) + lowered.Substring(1);
    }

    private static void HandleHistoryCardSelected(QuizHistoryEntry entry)
    {
        if (QuizFlowController.Instance == null)
        {
            Debug.LogWarning("[GuestQuizHistoryController] QuizFlowController was not ready for history review.");
            return;
        }

        bool opened = QuizFlowController.Instance.OpenHistoryAttempt(entry);
        if (!opened)
        {
            Debug.LogWarning("[GuestQuizHistoryController] This history item was saved before detailed review data was enabled.");
        }
    }

    private static DateTime ParseDateTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTime.MinValue;
        }

        return DateTime.TryParse(
            value,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out DateTime parsed)
            ? parsed
            : DateTime.MinValue;
    }

    private static GameObject FindObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];
            if (candidate != null && candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private static GameObject FindChildObject(GameObject root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == childName)
            {
                return transforms[i].gameObject;
            }
        }

        return null;
    }

    private static TextMeshProUGUI FindText(GameObject root, string textObjectName)
    {
        GameObject target = FindChildObject(root, textObjectName);
        return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
    }

    private static Image FindImage(GameObject root, string imageObjectName)
    {
        GameObject target = FindChildObject(root, imageObjectName);
        return target != null ? target.GetComponent<Image>() : null;
    }

    private static TextMeshProUGUI FindTextGlobal(string textObjectName)
    {
        GameObject target = FindObjectByName(textObjectName);
        return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
    }

    private static string NormalizeTopicKey(string topic)
    {
        return string.IsNullOrWhiteSpace(topic)
            ? string.Empty
            : topic.Trim().ToLowerInvariant();
    }
}
