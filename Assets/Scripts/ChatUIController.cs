using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ChatUIController : MonoBehaviour
{
    private const int MaxHistoryMessagesToSend = 4;
    private const int MaxStoredSessions = 20;
    private const string PendingAssistantReplyText = "AstroLearn AI is thinking...";
    private const string ScopeGlossaryResourcePath = "ChatScopeGlossary";

    public static ChatUIController Instance { get; private set; }

    private GameObject chatbotRoot;
    private GameObject chatPage;

    private TMP_InputField questionInputField;
    private Button sendButton;
    private Button newChatButton;
    private Button clearHistoryButton;
    private Button exitButton;

    private ScrollRect messagesScrollRect;
    private RectTransform messagesContent;
    private GameObject userMessageTemplate;
    private GameObject botMessageTemplate;

    private ScrollRect historyScrollRect;
    private RectTransform historyContent;
    private GameObject historyItemTemplate;

    private readonly List<GameObject> spawnedMessageItems = new List<GameObject>();
    private readonly List<GameObject> spawnedHistoryItems = new List<GameObject>();

    private ChatSessionCollection sessions;
    private ChatSessionData currentSession;
    private bool isWaitingForReply;
    private bool lastCloudRequestRateLimited;
    private Coroutine thinkingAnimationCoroutine;
    private static List<string> cachedScopeKeywords;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<ChatUIController>() != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject("ChatUIController");
        controllerObject.AddComponent<ChatUIController>();
    }

    private void Awake()
    {
        ChatUIController[] controllers = FindObjectsByType<ChatUIController>(FindObjectsSortMode.None);
        if (controllers.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeController();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnregisterListeners();
    }

    private void ResolveReferences()
    {
        chatbotRoot = FindObjectByName("ChatbotCanvas") ?? FindObjectByName("Chatbot Canvas");
        chatPage = FindObjectByNameWithin(chatbotRoot, "ChatPage") ?? FindObjectByName("ChatPage");
        if (chatbotRoot == null && chatPage != null)
        {
            chatbotRoot = GetTopLevelSceneParent(chatPage);
        }

        newChatButton = FindButton(chatbotRoot, "NewChatButton");
        clearHistoryButton = FindButton(chatbotRoot, "ClearHistoryButton");
        exitButton = FindButton(chatbotRoot, "ExitButton") ?? FindButtonByChildText(chatbotRoot, "EXIT");
        questionInputField = FindInputField(chatbotRoot, "QuestionInputField");

        historyItemTemplate = FindObjectByNameWithin(chatbotRoot, "HistoryItemTemplate");
        userMessageTemplate = FindObjectByNameWithin(chatbotRoot, "UserMessageTemplate");
        botMessageTemplate = FindObjectByNameWithin(chatbotRoot, "BotMessageTemplate");

        historyScrollRect = FindScrollRectWithContentName(chatbotRoot, "HistoryContent");
        historyContent = historyScrollRect != null ? historyScrollRect.content : FindRectTransform(chatbotRoot, "HistoryContent");

        messagesScrollRect = FindScrollRectWithContentName(chatbotRoot, "MessageContent");
        if (messagesScrollRect == null && userMessageTemplate != null)
        {
            messagesScrollRect = FindScrollRectContaining(chatbotRoot, userMessageTemplate);
        }

        messagesContent = messagesScrollRect != null ? messagesScrollRect.content : FindRectTransform(chatbotRoot, "MessageContent");

        sendButton = FindButtonByChildName(chatbotRoot, "SendIcon");
        if (sendButton == null && questionInputField != null && questionInputField.transform.parent != null)
        {
            sendButton = FindSiblingButton(questionInputField.transform.parent, questionInputField.gameObject);
        }
    }

    private void InitializeController()
    {
        ResolveReferences();
        NormalizeChatRootTransform();
        RegisterListeners();
        PrepareTemplates();
        ConfigureContentLayouts();
        LoadSessions();
        RenderAll();
    }

    public void OpenChatUi()
    {
        EnsureReferencesAreAlive();
        NormalizeChatRootTransform();
        StartFreshSessionForOpen();

        if (chatbotRoot != null)
        {
            chatbotRoot.SetActive(true);
        }

        if (chatPage != null)
        {
            chatPage.SetActive(true);
        }

        RenderAll();

        if (questionInputField != null)
        {
            questionInputField.text = string.Empty;
            questionInputField.ActivateInputField();
        }
    }

    public void RefreshUi()
    {
        EnsureReferencesAreAlive();
        RenderAll();
    }

    private void EnsureReferencesAreAlive()
    {
        bool needsResolve =
            chatbotRoot == null ||
            chatPage == null ||
            questionInputField == null ||
            sendButton == null ||
            newChatButton == null ||
            clearHistoryButton == null ||
            messagesContent == null ||
            historyContent == null ||
            userMessageTemplate == null ||
            botMessageTemplate == null ||
            historyItemTemplate == null;

        if (!needsResolve)
        {
            return;
        }

        UnregisterListeners();
        ResolveReferences();
        NormalizeChatRootTransform();
        RegisterListeners();
        PrepareTemplates();
        ConfigureContentLayouts();
    }

    private void RegisterListeners()
    {
        AddListener(newChatButton, HandleNewChatPressed);
        AddListener(clearHistoryButton, HandleClearHistoryPressed);
        AddListener(sendButton, HandleSendPressed);

        if (questionInputField != null)
        {
            questionInputField.onSubmit.RemoveListener(HandleInputSubmit);
            questionInputField.onSubmit.AddListener(HandleInputSubmit);
        }
    }

    private void UnregisterListeners()
    {
        RemoveListener(newChatButton, HandleNewChatPressed);
        RemoveListener(clearHistoryButton, HandleClearHistoryPressed);
        RemoveListener(sendButton, HandleSendPressed);

        if (questionInputField != null)
        {
            questionInputField.onSubmit.RemoveListener(HandleInputSubmit);
        }
    }

    private void PrepareTemplates()
    {
        if (historyItemTemplate != null)
        {
            historyItemTemplate.SetActive(false);
        }

        if (userMessageTemplate != null)
        {
            userMessageTemplate.SetActive(false);
        }

        if (botMessageTemplate != null)
        {
            botMessageTemplate.SetActive(false);
        }
    }

    private void ConfigureContentLayouts()
    {
        ConfigureMessagesLayout();
        ConfigureHistoryLayout();
    }

    private void ConfigureHistoryLayout()
    {
        if (historyContent == null)
        {
            return;
        }

        VerticalLayoutGroup layout = historyContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = historyContent.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = Mathf.Max(layout.spacing, 14f);

        ContentSizeFitter fitter = historyContent.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = historyContent.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void ConfigureMessagesLayout()
    {
        if (messagesContent == null)
        {
            return;
        }

        VerticalLayoutGroup layout = messagesContent.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = messagesContent.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        layout.childAlignment = TextAnchor.LowerLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = Mathf.Max(layout.spacing, 20f);

        ContentSizeFitter fitter = messagesContent.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = messagesContent.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement layoutElement = messagesContent.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = messagesContent.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = 0f;
    }

    private void LoadSessions()
    {
        sessions = GuestChatStorage.LoadSessions();
        if (sessions.sessions == null)
        {
            sessions.sessions = new List<ChatSessionData>();
        }

        currentSession = FindSessionById(sessions.activeSessionId) ?? FindMostRecentSession();
        if (currentSession == null)
        {
            currentSession = CreateEmptySession();
        }
    }

    private void RenderAll()
    {
        RenderCurrentMessages();
        RenderHistoryList();
        UpdateActionStates();
    }

    private void HandleNewChatPressed()
    {
        currentSession = CreateEmptySession(GetCurrentLanguage());
        sessions.activeSessionId = currentSession.sessionId;
        SaveSessions();
        RenderAll();

        if (questionInputField != null)
        {
            questionInputField.text = string.Empty;
            questionInputField.ActivateInputField();
        }
    }

    private void StartFreshSessionForOpen()
    {
        if (isWaitingForReply)
        {
            return;
        }

        if (currentSession != null &&
            currentSession.messages != null &&
            currentSession.messages.Count == 0)
        {
            sessions.activeSessionId = currentSession.sessionId;
            return;
        }

        currentSession = CreateEmptySession(GetCurrentLanguage());
        sessions.activeSessionId = currentSession.sessionId;
    }

    private void HandleClearHistoryPressed()
    {
        sessions = new ChatSessionCollection();
        currentSession = CreateEmptySession("English");
        sessions.activeSessionId = currentSession.sessionId;
        GuestChatStorage.ClearSessions();
        SaveSessions();
        RenderAll();
    }

    private void HandleSendPressed()
    {
        if (isWaitingForReply || questionInputField == null)
        {
            return;
        }

        string message = questionInputField.text != null ? questionInputField.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        EnsureCurrentSessionTracked();
        AddMessageToCurrentSession("user", message);
        UpdateSessionTitleFromMessage(message);
        SaveSessions();
        RenderAll();

        questionInputField.text = string.Empty;
        questionInputField.ActivateInputField();

        if (TryHandleLanguagePreferenceRequest(message))
        {
            SaveSessions();
            RenderAll();
            return;
        }

        if (!IsMessageInScope(message))
        {
            AddMessageToCurrentSession("assistant", BuildOffTopicReply(GetCurrentLanguage()));
            SaveSessions();
            RenderAll();
            return;
        }

        if (IsGreetingOnlyMessage(message))
        {
            AddMessageToCurrentSession("assistant", BuildFallbackReply(message));
            SaveSessions();
            RenderAll();
            return;
        }

        StartCoroutine(RequestAssistantReplyRoutine(message));
    }

    private void HandleInputSubmit(string submittedText)
    {
        if (!string.IsNullOrWhiteSpace(submittedText))
        {
            HandleSendPressed();
        }
    }

    private IEnumerator RequestAssistantReplyRoutine(string latestMessage)
    {
        isWaitingForReply = true;
        UpdateActionStates();
        ChatMessageData pendingMessage = AddMessageToCurrentSession("assistant", PendingAssistantReplyText);
        StartThinkingAnimation(pendingMessage);
        SaveSessions();
        RenderAll();

        string reply = null;
        string endpoint = EnvFileLoader.Get("GEMINI_CHAT_ENDPOINT");
        string bearerToken = EnvFileLoader.Get("GEMINI_CHAT_BEARER_TOKEN");
        string anonKey = EnvFileLoader.Get("SUPABASE_ANON_KEY");
        string ollamaEndpoint = EnvFileLoader.Get("OLLAMA_CHAT_ENDPOINT", "http://127.0.0.1:11434/api/generate");
        string ollamaModel = EnvFileLoader.Get("OLLAMA_CHAT_MODEL", "llama3.2:3b");
        bool preferLocalChat = ParseEnvBoolean(EnvFileLoader.Get("PREFER_LOCAL_CHAT", "true"), true);

        lastCloudRequestRateLimited = false;

        bool canUseLocalLlm = !string.IsNullOrWhiteSpace(ollamaEndpoint) && !string.IsNullOrWhiteSpace(ollamaModel);
        bool canUseCloudChat = !string.IsNullOrWhiteSpace(endpoint);

        if (preferLocalChat && canUseLocalLlm)
        {
            Debug.Log("[ChatUIController] Trying local Ollama first for chat reply.");
            string localReply = null;
            yield return StartCoroutine(RequestReplyFromOllama(ollamaEndpoint.Trim(), ollamaModel.Trim(), latestMessage, result => localReply = result));
            if (!string.IsNullOrWhiteSpace(localReply))
            {
                Debug.Log("[ChatUIController] Local Ollama returned a reply.");
                reply = localReply;
            }
        }

        bool shouldTryCloudChat = string.IsNullOrWhiteSpace(reply) && canUseCloudChat;
        if (shouldTryCloudChat)
        {
            Debug.Log("[ChatUIController] Trying cloud chat endpoint.");
            yield return StartCoroutine(RequestReplyFromEndpoint(endpoint.Trim(), bearerToken, anonKey, latestMessage, result => reply = result));
        }

        bool shouldTryLocalLlmAfterCloud = string.IsNullOrWhiteSpace(reply) && !preferLocalChat && canUseLocalLlm;
        if (shouldTryLocalLlmAfterCloud)
        {
            Debug.Log("[ChatUIController] Cloud reply unavailable. Trying local Ollama fallback.");
            string localReply = null;
            yield return StartCoroutine(RequestReplyFromOllama(ollamaEndpoint.Trim(), ollamaModel.Trim(), latestMessage, result => localReply = result));
            if (!string.IsNullOrWhiteSpace(localReply))
            {
                Debug.Log("[ChatUIController] Local Ollama returned a fallback reply.");
                reply = localReply;
            }
        }

        if (ShouldUseLocalFallback(reply, latestMessage))
        {
            Debug.Log("[ChatUIController] Reply looked mismatched. Using built-in fallback instead.");
            reply = null;
        }

        if (string.IsNullOrWhiteSpace(reply))
        {
            if (lastCloudRequestRateLimited)
            {
                reply = BuildRateLimitedReply();
            }
            else
            {
                Debug.Log("[ChatUIController] No AI reply available. Using built-in fallback.");
                reply = BuildFallbackReply(latestMessage);
            }
        }

        if (pendingMessage != null)
        {
            pendingMessage.text = reply;
            pendingMessage.timestampUtc = DateTime.UtcNow.ToString("o");
        }
        else
        {
            AddMessageToCurrentSession("assistant", reply);
        }

        StopThinkingAnimation();
        SaveSessions();
        RenderAll();
        isWaitingForReply = false;
        UpdateActionStates();
    }

    private void StartThinkingAnimation(ChatMessageData pendingMessage)
    {
        StopThinkingAnimation();
        if (pendingMessage == null)
        {
            return;
        }

        thinkingAnimationCoroutine = StartCoroutine(AnimateThinkingMessage(pendingMessage));
    }

    private void StopThinkingAnimation()
    {
        if (thinkingAnimationCoroutine != null)
        {
            StopCoroutine(thinkingAnimationCoroutine);
            thinkingAnimationCoroutine = null;
        }
    }

    private IEnumerator AnimateThinkingMessage(ChatMessageData pendingMessage)
    {
        int dotCount = 0;
        while (isWaitingForReply && pendingMessage != null)
        {
            pendingMessage.text = "AstroLearn AI is thinking" + new string('.', dotCount + 1);
            RenderCurrentMessages();
            dotCount = (dotCount + 1) % 3;
            yield return new WaitForSecondsRealtime(0.45f);
        }
    }

    private bool ParseEnvBoolean(string value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized == "1" || normalized == "true" || normalized == "yes" || normalized == "on")
        {
            return true;
        }

        if (normalized == "0" || normalized == "false" || normalized == "no" || normalized == "off")
        {
            return false;
        }

        return defaultValue;
    }

    private bool ShouldUseLocalFallback(string reply, string latestMessage)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return false;
        }

        string normalizedReply = reply.Trim();
        string normalizedQuestion = string.IsNullOrWhiteSpace(latestMessage)
            ? string.Empty
            : latestMessage.Trim().ToLowerInvariant();

        bool looksLikeOldGenericFallback =
            normalizedReply.IndexOf("Please try asking your question again in a clear way.", StringComparison.OrdinalIgnoreCase) >= 0;

        bool looksLikeOffTopicMismatch =
            normalizedReply.IndexOf("Please ask something within that scope.", StringComparison.OrdinalIgnoreCase) >= 0 &&
            IsMessageInScope(latestMessage);

        bool hasKnownLocalAnswer =
            normalizedQuestion.Contains("sun") ||
            normalizedQuestion.Contains("mercury") ||
            normalizedQuestion.Contains("venus") ||
            normalizedQuestion.Contains("earth") ||
            normalizedQuestion.Contains("mars") ||
            normalizedQuestion.Contains("jupiter") ||
            normalizedQuestion.Contains("saturn") ||
            normalizedQuestion.Contains("uranus") ||
            normalizedQuestion.Contains("neptune") ||
            normalizedQuestion.Contains("pluto") ||
            normalizedQuestion.Contains("kepler") ||
            normalizedQuestion.Contains("exoplanet");

        return (looksLikeOldGenericFallback || looksLikeOffTopicMismatch) && hasKnownLocalAnswer;
    }

    private IEnumerator RequestReplyFromEndpoint(string endpoint, string bearerToken, string anonKey, string latestMessage, Action<string> onResult)
    {
        ChatRequestPayload payload = new ChatRequestPayload
        {
            message = latestMessage,
            sessionId = currentSession != null ? currentSession.sessionId : string.Empty,
            history = BuildRecentHistoryPayload()
        };

        byte[] requestBody = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

        using (UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(requestBody);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 45;
            request.SetRequestHeader("Content-Type", "application/json");

            string authToken = !string.IsNullOrWhiteSpace(bearerToken) ? bearerToken.Trim() : anonKey.Trim();
            if (!string.IsNullOrWhiteSpace(anonKey))
            {
                request.SetRequestHeader("apikey", anonKey.Trim());
            }

            if (!string.IsNullOrWhiteSpace(authToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + authToken);
            }

            yield return request.SendWebRequest();

            string responseText = request.downloadHandler != null ? request.downloadHandler.text : null;
            ChatResponsePayload response = null;
            if (!string.IsNullOrWhiteSpace(responseText))
            {
                try
                {
                    response = JsonUtility.FromJson<ChatResponsePayload>(responseText);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[ChatUIController] Chat response parse failed: " + exception.Message);
                }
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[ChatUIController] Chat request failed: " + request.error + "\n" + responseText);
                onResult?.Invoke(null);
                yield break;
            }

            if (response != null && !string.IsNullOrWhiteSpace(response.error))
            {
                string normalizedRaw = string.IsNullOrWhiteSpace(responseText) ? string.Empty : responseText.ToLowerInvariant();
                string normalizedDetails = !string.IsNullOrWhiteSpace(response.details) ? response.details.ToLowerInvariant() : string.Empty;
                lastCloudRequestRateLimited =
                    response.status == 429 ||
                    response.status == 503 ||
                    normalizedRaw.Contains("rate limit") ||
                    normalizedRaw.Contains("quota") ||
                    normalizedDetails.Contains("rate limit") ||
                    normalizedDetails.Contains("quota") ||
                    normalizedDetails.Contains("resource_exhausted");
                onResult?.Invoke(null);
                yield break;
            }

            onResult?.Invoke(response != null ? response.reply : null);
        }
    }

    private IEnumerator RequestReplyFromOllama(string endpoint, string model, string latestMessage, Action<string> onResult)
    {
        string language = GetCurrentLanguage();
        string prompt = BuildLocalLlmPrompt(latestMessage, language);
        Debug.Log("[ChatUIController] Sending request to Ollama model: " + model);
        OllamaGenerateRequestPayload payload = new OllamaGenerateRequestPayload
        {
            model = model,
            prompt = prompt,
            stream = false,
            keep_alive = "30m",
            options = new OllamaOptionsPayload
            {
                num_predict = 120,
                temperature = 0.3f
            }
        };

        byte[] requestBody = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

        using (UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(requestBody);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 180;
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[ChatUIController] Ollama request failed: " + request.error);
                onResult?.Invoke(null);
                yield break;
            }

            string responseText = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (string.IsNullOrWhiteSpace(responseText))
            {
                Debug.LogWarning("[ChatUIController] Ollama returned an empty response.");
                onResult?.Invoke(null);
                yield break;
            }

            OllamaGenerateResponsePayload response = null;
            try
            {
                response = JsonUtility.FromJson<OllamaGenerateResponsePayload>(responseText);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ChatUIController] Ollama response parse failed: " + exception.Message);
            }

            string reply = response != null ? response.response : null;
            Debug.Log("[ChatUIController] Ollama response received.");
            onResult?.Invoke(!string.IsNullOrWhiteSpace(reply) ? reply.Trim() : null);
        }
    }

    private string BuildLocalLlmPrompt(string latestMessage, string language)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("You are AstroLearn AI Chatbot.");
        builder.AppendLine("You only answer astronomy and space-related questions.");
        builder.AppendLine("If the topic is outside space science, politely refuse.");
        builder.AppendLine("Keep replies educational, clear, concise, and suitable for Grade 7 to Grade 12 students.");
        builder.AppendLine($"Reply in {language}.");
        builder.AppendLine("Reply in plain text only.");
        builder.AppendLine("In AstroLearn, interpret ambiguous words using astronomy context.");
        builder.AppendLine("Examples: if the user says 'buwan', interpret it as 'moon' unless they clearly mean a calendar month.");
        builder.AppendLine("If the user mentions planets, moons, stars, galaxies, black holes, the Solar System, exoplanets, gravity, vacuum, light-years, temperature of planets, or related science concepts, answer them as in-scope astronomy questions.");
        builder.AppendLine("If the user makes minor spelling mistakes, still answer based on the most likely astronomy meaning.");
        builder.AppendLine("Keep the answer under 80 words.");
        builder.AppendLine();
        builder.AppendLine("Conversation history:");

        List<ChatMessagePayload> history = BuildRecentHistoryPayload();
        if (history.Count == 0)
        {
            builder.AppendLine("(no prior history)");
        }
        else
        {
            foreach (ChatMessagePayload entry in history)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.text))
                {
                    continue;
                }

                string role = string.Equals(entry.role, "assistant", StringComparison.OrdinalIgnoreCase) ? "Assistant" : "User";
                builder.AppendLine(role + ": " + entry.text);
            }
        }

        builder.AppendLine();
        builder.AppendLine("Latest user message: " + latestMessage);
        return builder.ToString();
    }

    private string BuildRateLimitedReply()
    {
        return IsTagalogLanguage(GetCurrentLanguage())
            ? "Naabot na ang limit ng AI requests para sa ngayon. Pakisubukang muli mamaya o bukas kapag nag-reset na ang quota."
            : "The AI request limit has been reached for now. Please try again later or after the daily quota resets.";
    }

    private List<ChatMessagePayload> BuildRecentHistoryPayload()
    {
        List<ChatMessagePayload> history = new List<ChatMessagePayload>();
        if (currentSession == null || currentSession.messages == null)
        {
            return history;
        }

        int startIndex = Mathf.Max(0, currentSession.messages.Count - MaxHistoryMessagesToSend);
        for (int i = startIndex; i < currentSession.messages.Count; i++)
        {
            ChatMessageData message = currentSession.messages[i];
            if (message == null || string.IsNullOrWhiteSpace(message.text))
            {
                continue;
            }

            history.Add(new ChatMessagePayload
            {
                role = message.role,
                text = message.text
            });
        }

        return history;
    }

    private ChatMessageData AddMessageToCurrentSession(string role, string text)
    {
        if (currentSession == null)
        {
            currentSession = CreateEmptySession();
        }

        if (currentSession.messages == null)
        {
            currentSession.messages = new List<ChatMessageData>();
        }

        ChatMessageData chatMessage = new ChatMessageData
        {
            role = role,
            text = text,
            timestampUtc = DateTime.UtcNow.ToString("o")
        };

        currentSession.messages.Add(chatMessage);

        currentSession.updatedAtUtc = DateTime.UtcNow.ToString("o");
        sessions.activeSessionId = currentSession.sessionId;
        LimitStoredSessions();
        return chatMessage;
    }

    private void UpdateSessionTitleFromMessage(string userMessage)
    {
        if (currentSession == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(currentSession.title) &&
            !string.Equals(currentSession.title, "New Chat", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string compact = userMessage.Replace("\r", " ").Replace("\n", " ").Trim();
        if (compact.Length > 40)
        {
            compact = compact.Substring(0, 40).TrimEnd() + "...";
        }

        currentSession.title = compact;
    }

    private void EnsureCurrentSessionTracked()
    {
        if (currentSession == null)
        {
            currentSession = CreateEmptySession();
        }

        if (sessions.sessions == null)
        {
            sessions.sessions = new List<ChatSessionData>();
        }

        if (!sessions.sessions.Any(session => session != null && session.sessionId == currentSession.sessionId))
        {
            sessions.sessions.Insert(0, currentSession);
        }
    }

    private void SaveSessions()
    {
        EnsureCurrentSessionTracked();
        sessions.activeSessionId = currentSession != null ? currentSession.sessionId : string.Empty;
        sessions.sessions = sessions.sessions
            .Where(session => session != null && session.messages != null && session.messages.Count > 0)
            .OrderByDescending(GetSessionSortKey)
            .Take(MaxStoredSessions)
            .ToList();

        GuestChatStorage.SaveSessions(sessions);
    }

    private void LimitStoredSessions()
    {
        if (sessions.sessions == null)
        {
            sessions.sessions = new List<ChatSessionData>();
            return;
        }

        sessions.sessions = sessions.sessions
            .Where(session => session != null)
            .OrderByDescending(GetSessionSortKey)
            .Take(MaxStoredSessions)
            .ToList();
    }

    private void RenderCurrentMessages()
    {
        ClearSpawnedItems(spawnedMessageItems);

        if (messagesContent == null || currentSession == null || currentSession.messages == null)
        {
            return;
        }

        foreach (ChatMessageData message in currentSession.messages)
        {
            if (message == null)
            {
                continue;
            }

            GameObject template = string.Equals(message.role, "user", StringComparison.OrdinalIgnoreCase)
                ? userMessageTemplate
                : botMessageTemplate;

            if (template == null)
            {
                continue;
            }

            bool isUserMessage = string.Equals(message.role, "user", StringComparison.OrdinalIgnoreCase);
            RectTransform templateRect = template.transform as RectTransform;
            float bubbleWidth = templateRect != null && templateRect.sizeDelta.x > 0f ? templateRect.sizeDelta.x : 816.9259f;
            float bubbleHeight = templateRect != null && templateRect.sizeDelta.y > 0f ? templateRect.sizeDelta.y : 132.8784f;

            GameObject row = CreateMessageRow(isUserMessage, bubbleHeight);
            if (row == null)
            {
                continue;
            }

            GameObject item = Instantiate(template, row.transform);
            item.name = (isUserMessage ? "UserMessage_" : "BotMessage_") + spawnedMessageItems.Count;
            item.SetActive(true);
            PrepareMessageBubbleForLayout(item.transform as RectTransform, bubbleWidth, bubbleHeight);
            BindMessageItem(item, message);
            AdjustMessageBubbleHeight(row, item, bubbleWidth, bubbleHeight);
            spawnedMessageItems.Add(row);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(messagesContent);
        EnsureMessagesContentFillsViewport();
        Canvas.ForceUpdateCanvases();
        ScrollMessagesToBottom();
    }

    private void EnsureMessagesContentFillsViewport()
    {
        if (messagesContent == null || messagesScrollRect == null || messagesScrollRect.viewport == null)
        {
            return;
        }

        LayoutElement layoutElement = messagesContent.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            return;
        }

        float viewportHeight = messagesScrollRect.viewport.rect.height;
        float preferredHeight = LayoutUtility.GetPreferredHeight(messagesContent);
        bool contentOverflows = preferredHeight > viewportHeight + 4f;

        layoutElement.minHeight = Mathf.Max(viewportHeight, preferredHeight);
        messagesScrollRect.vertical = contentOverflows;
        messagesScrollRect.movementType = contentOverflows ? ScrollRect.MovementType.Elastic : ScrollRect.MovementType.Clamped;
        LayoutRebuilder.ForceRebuildLayoutImmediate(messagesContent);
    }

    private void RenderHistoryList()
    {
        ClearSpawnedItems(spawnedHistoryItems);

        if (historyContent == null || sessions == null || sessions.sessions == null)
        {
            return;
        }

        foreach (ChatSessionData session in sessions.sessions)
        {
            if (session == null)
            {
                continue;
            }

            if (historyItemTemplate == null)
            {
                continue;
            }

            GameObject item = Instantiate(historyItemTemplate, historyContent);
            item.name = "HistoryItem_" + session.sessionId;
            item.SetActive(true);
            PrepareHistoryItemForLayout(item.transform as RectTransform);
            BindHistoryItem(item, session);
            spawnedHistoryItems.Add(item);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(historyContent);
        Canvas.ForceUpdateCanvases();
    }

    private void BindMessageItem(GameObject item, ChatMessageData message)
    {
        TextMeshProUGUI timeText = FindTimeText(item);
        TextMeshProUGUI messageText = FindMessageText(item, timeText);

        if (messageText != null)
        {
            messageText.text = message.text;
            ConfigureMessageText(messageText);
        }

        if (timeText != null)
        {
            timeText.text = FormatChatTime(message.timestampUtc);
            ConfigureTimeText(timeText);
        }
    }

    private void AdjustMessageBubbleHeight(GameObject row, GameObject item, float bubbleWidth, float minimumHeight)
    {
        if (row == null || item == null)
        {
            return;
        }

        RectTransform itemRect = item.transform as RectTransform;
        RectTransform rowRect = row.transform as RectTransform;
        if (itemRect == null || rowRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        TextMeshProUGUI timeText = FindTimeText(item);
        TextMeshProUGUI messageText = FindMessageText(item, timeText);

        float preferredTextHeight = messageText != null ? messageText.GetPreferredValues(messageText.text, messageText.rectTransform.rect.width, 0f).y : 0f;
        float preferredTimeHeight = timeText != null ? timeText.GetPreferredValues(timeText.text, timeText.rectTransform.rect.width, 0f).y : 0f;

        float requiredHeight = Mathf.Max(minimumHeight, preferredTextHeight + preferredTimeHeight + 96f);

        itemRect.sizeDelta = new Vector2(bubbleWidth, requiredHeight);
        rowRect.sizeDelta = new Vector2(0f, requiredHeight);

        LayoutElement bubbleLayout = item.GetComponent<LayoutElement>();
        if (bubbleLayout != null)
        {
            bubbleLayout.preferredHeight = requiredHeight;
            bubbleLayout.minHeight = requiredHeight;
        }

        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        if (rowLayout != null)
        {
            rowLayout.preferredHeight = requiredHeight;
            rowLayout.minHeight = requiredHeight;
        }
    }

    private void BindHistoryItem(GameObject item, ChatSessionData session)
    {
        Button itemButton = item.GetComponent<Button>() ?? item.GetComponentInChildren<Button>(true);
        TextMeshProUGUI timeText = FindText(item, "TimeText");
        TextMeshProUGUI questionText = FindText(item, "QuestionText");
        if (questionText == null)
        {
            TextMeshProUGUI[] texts = item.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI candidate in texts)
            {
                if (candidate == null || candidate == timeText)
                {
                    continue;
                }

                questionText = candidate;
                break;
            }
        }

        if (questionText != null)
        {
            questionText.text = string.IsNullOrWhiteSpace(session.title) ? "New Chat" : session.title;
        }

        if (timeText != null)
        {
            timeText.text = FormatHistoryTime(session.updatedAtUtc);
        }

        if (itemButton != null)
        {
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener(() => LoadSession(session.sessionId));
        }
    }

    private void LoadSession(string sessionId)
    {
        ChatSessionData session = FindSessionById(sessionId);
        if (session == null)
        {
            return;
        }

        currentSession = session;
        sessions.activeSessionId = sessionId;
        SaveSessions();
        RenderAll();
    }

    private void UpdateActionStates()
    {
        if (sendButton != null)
        {
            sendButton.interactable = !isWaitingForReply;
        }

        if (newChatButton != null)
        {
            newChatButton.interactable = !isWaitingForReply;
        }

        if (clearHistoryButton != null)
        {
            clearHistoryButton.interactable = !isWaitingForReply;
        }
    }

    private bool IsMessageInScope(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        string normalized = message.ToLowerInvariant();

        if (IsLanguagePreferenceRequest(normalized))
        {
            return true;
        }

        string[] blockedTerms =
        {
            "sex", "porn", "nude", "kill", "murder", "bomb", "drugs", "suicide", "hate", "racist",
            "patayin", "bomba", "droga", "magpakamatay"
        };

        foreach (string blocked in blockedTerms)
        {
            if (normalized.Contains(blocked))
            {
                return false;
            }
        }

        List<string> spaceKeywords = GetScopeKeywords();

        foreach (string keyword in spaceKeywords)
        {
            if (normalized.Contains(keyword))
            {
                return true;
            }
        }

        string[] tokens = normalized
            .Split(new[] { ' ', '\t', '\r', '\n', '?', '!', '.', ',', ':', ';', '-', '_', '/', '\\', '(', ')', '[', ']', '"' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string token in tokens)
        {
            foreach (string keyword in spaceKeywords)
            {
                if (AreCloseKeywordMatch(token, keyword))
                {
                    return true;
                }
            }
        }

        string[] friendlyShortPrompts =
        {
            "hi", "hello", "hey", "help", "what can you do", "kumusta", "hello po"
        };

        return friendlyShortPrompts.Any(prompt => normalized.Contains(prompt));
    }

    private static List<string> GetScopeKeywords()
    {
        if (cachedScopeKeywords != null)
        {
            return cachedScopeKeywords;
        }

        cachedScopeKeywords = new List<string>
        {
            "space", "planet", "planets", "solar system", "sun", "moon", "moons", "earth", "mars",
            "jupiter", "saturn", "venus", "mercury", "uranus", "neptune", "pluto", "comet", "asteroid",
            "asteroids", "asteroit", "phobos", "deimos", "titan", "triton", "io", "europa", "ganymede",
            "callisto", "charon", "enceladus", "mimas", "kuiper belt", "oort cloud", "dwarf planet",
            "galaxy", "galaxies", "star", "stars", "nebula", "black hole", "orbit", "astronomy",
            "cosmos", "universe", "meteor", "meteorite", "eclipse", "constellation", "rocket", "telescope",
            "kepler", "exoplanet", "nasa", "esa", "observatory", "milky way", "andromeda", "cosmic",
            "absolute zero", "gravity", "vacuum", "light year",
            "kalawakan", "planeta", "mga planeta", "buwan", "mga buwan", "araw", "bituin", "mga bituin",
            "galaksiya", "uniberso", "kometa", "bulalakaw", "astronomiya", "grabidad", "temperatura",
            "init", "lamig", "sukat", "laki", "lawak", "eklipse", "konstelasyon", "teleskopyo"
        };

        TextAsset glossaryAsset = Resources.Load<TextAsset>(ScopeGlossaryResourcePath);
        if (glossaryAsset == null || string.IsNullOrWhiteSpace(glossaryAsset.text))
        {
            return cachedScopeKeywords;
        }

        try
        {
            ChatScopeGlossaryData glossary = JsonUtility.FromJson<ChatScopeGlossaryData>(glossaryAsset.text);
            if (glossary != null && glossary.keywords != null)
            {
                foreach (string keyword in glossary.keywords)
                {
                    if (string.IsNullOrWhiteSpace(keyword))
                    {
                        continue;
                    }

                    string normalized = keyword.Trim().ToLowerInvariant();
                    if (!cachedScopeKeywords.Contains(normalized))
                    {
                        cachedScopeKeywords.Add(normalized);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[ChatUIController] Failed to load chat scope glossary: " + exception.Message);
        }

        return cachedScopeKeywords;
    }

    private string BuildOffTopicReply(string language)
    {
        if (IsTagalogLanguage(language))
        {
            return "Makakatulong lang ako sa mga paksang may kinalaman sa kalawakan tulad ng mga planeta, buwan, bituin, galaxy, Solar System, at astronomy. Magtanong ka ng bagay na sakop ng paksang iyon.";
        }

        return "I can only help with space-related topics like planets, moons, stars, galaxies, the Solar System, and astronomy. Please ask something within that scope.";
    }

    private string BuildFallbackReply(string latestMessage)
    {
        string normalized = latestMessage.ToLowerInvariant();
        bool useTagalog = IsTagalogLanguage(GetCurrentLanguage());
        bool asksMoonInTagalog = normalized.Contains("buwan");
        bool asksSunInTagalog = normalized.Contains("araw");
        bool asksPlanetInTagalog = normalized.Contains("planeta");
        bool asksStarInTagalog = normalized.Contains("bituin");
        bool asksGalaxyInTagalog = normalized.Contains("galaksiya");
        bool asksSpaceInTagalog = normalized.Contains("kalawakan");
        bool asksUniverseInTagalog = normalized.Contains("uniberso");
        bool asksGravityInTagalog = normalized.Contains("grabidad");

        string temperatureAnswer = BuildTemperatureFallbackReply(normalized, useTagalog);
        if (!string.IsNullOrWhiteSpace(temperatureAnswer))
        {
            return temperatureAnswer;
        }

        string moonAnswer = BuildMoonCountFallbackReply(normalized);
        if (!string.IsNullOrWhiteSpace(moonAnswer))
        {
            return useTagalog ? TranslateFallbackToTagalog(moonAnswer, normalized) : moonAnswer;
        }

        if (normalized.Contains("sun") || asksSunInTagalog)
        {
            return useTagalog
                ? "Ang Araw ang bituin sa gitna ng ating Solar System. Isa itong napakainit na bola ng plasma na gumagawa ng enerhiya sa pamamagitan ng nuclear fusion."
                : "The Sun is the star at the center of our Solar System. It is a huge ball of hot plasma that produces energy through nuclear fusion, mainly by turning hydrogen into helium.";
        }

        if (normalized.Contains("mercury"))
        {
            if (normalized.Contains("moon"))
            {
                return useTagalog
                    ? "Walang natural na buwan ang Mercury. Ito ang pinakamalapit na planeta sa Araw at ang pinakamaliit sa walong pangunahing planeta."
                    : "Mercury has no natural moons. It is the closest planet to the Sun and also the smallest of the eight major planets.";
            }

            return useTagalog
                ? "Ang Mercury ang pinakamalapit na planeta sa Araw at ang pinakamaliit na pangunahing planeta sa ating Solar System. Mabato ito, maraming bunganga, at walang natural na buwan."
                : "Mercury is the closest planet to the Sun and the smallest major planet in our Solar System. It is rocky, heavily cratered, and has no natural moons.";
        }

        if (normalized.Contains("venus"))
        {
            return useTagalog
                ? "Ang Venus ang ikalawang planeta mula sa Araw. Halos kasinglaki ito ng Earth, ngunit dahil sa makapal nitong carbon dioxide atmosphere, ito ang pinakamainit na planeta sa Solar System."
                : "Venus is the second planet from the Sun. It is similar in size to Earth, but its thick carbon dioxide atmosphere makes it the hottest planet in the Solar System.";
        }

        if (normalized.Contains("earth"))
        {
            return useTagalog
                ? "Ang Earth ang ikatlong planeta mula sa Araw at ang tanging kilalang planeta na may buhay. Mayroon itong likidong tubig, atmosphere na puwedeng hingahan, at isang natural na buwan."
                : "Earth is the third planet from the Sun and the only known planet that supports life. It has liquid water, a breathable atmosphere, and one natural moon.";
        }

        if (normalized.Contains("mars"))
        {
            return useTagalog
                ? "Madalas tawaging Red Planet ang Mars dahil ang iron oxide o kalawang ang nagbibigay ng mapulang kulay sa ibabaw nito. Nandoon din ang Olympus Mons, isa sa pinakamalalaking bulkan sa Solar System."
                : "Mars is often called the Red Planet because iron oxide, or rust, gives its surface a reddish color. It also has Olympus Mons, one of the largest volcanoes in the Solar System.";
        }

        if (normalized.Contains("jupiter"))
        {
            return useTagalog
                ? "Ang Jupiter ang pinakamalaking planeta sa Solar System. Isa itong gas giant na kilala sa Great Red Spot at sa marami nitong buwan, kabilang ang Io, Europa, Ganymede, at Callisto."
                : "Jupiter is the largest planet in the Solar System. It is a gas giant known for the Great Red Spot and many moons, including Io, Europa, Ganymede, and Callisto.";
        }

        if (normalized.Contains("saturn"))
        {
            return useTagalog
                ? "Ang Saturn ay isang gas giant na tanyag dahil sa maliwanag nitong ring system. Kadalasang yelo at batong piraso ang bumubuo sa mga singsing nito, at marami itong buwan kabilang ang Titan."
                : "Saturn is a gas giant famous for its bright ring system. Its rings are mostly made of ice and rock particles, and it has many moons including Titan.";
        }

        if (normalized.Contains("uranus"))
        {
            return useTagalog
                ? "Ang Uranus ay isang ice giant na kilala sa pagkakahilig ng ikot nito sa tagiliran. Mayroon itong malamig na atmosphere, mapupusyaw na singsing, at maraming buwan."
                : "Uranus is an ice giant known for rotating on its side. It has a cold atmosphere, faint rings, and many moons.";
        }

        if (normalized.Contains("neptune"))
        {
            return useTagalog
                ? "Ang Neptune ang pinakamalayong pangunahing planeta mula sa Araw. Isa itong ice giant na may malalakas na hangin, matingkad na asul na kulay, at malaking buwang tinatawag na Triton."
                : "Neptune is the farthest major planet from the Sun. It is an ice giant with strong winds, a deep blue color, and a large moon named Triton.";
        }

        if (normalized.Contains("pluto"))
        {
            return useTagalog
                ? "Ang Pluto ay isang dwarf planet sa Kuiper Belt lampas sa Neptune. Mas maliit ito kaysa sa buwan ng Earth at kilala sa nagyeyelong ibabaw at manipis na atmosphere nito."
                : "Pluto is a dwarf planet in the Kuiper Belt beyond Neptune. It is smaller than Earth's moon and is known for its icy surface and thin atmosphere.";
        }

        if (normalized.Contains("kepler") || normalized.Contains("exoplanet"))
        {
            return useTagalog
                ? "Ang Kepler-22b ay isang exoplanet, ibig sabihin planeta ito sa labas ng ating Solar System. Natuklasan ito ng Kepler mission ng NASA at umiikot ito sa isang bituin sa constellation na Cygnus."
                : "Kepler-22b is an exoplanet, meaning it is a planet outside our Solar System. It was discovered by NASA's Kepler mission and orbits a star in the constellation Cygnus.";
        }

        if (normalized.Contains("phobos"))
        {
            return useTagalog
                ? "Ang Phobos ang mas malaki at mas malapit sa dalawang buwan ng Mars. Hindi regular ang hugis nito at napakabilis nitong umiikot sa Mars."
                : "Phobos is the larger and closer of Mars's two moons. It is irregularly shaped and orbits Mars very quickly.";
        }

        if (normalized.Contains("deimos"))
        {
            return useTagalog
                ? "Ang Deimos ang mas maliit at mas malayong buwan sa dalawang buwan ng Mars. Maliit ito, hindi regular ang hugis, at malamang na mabato ang materyal nito."
                : "Deimos is the smaller and more distant of Mars's two moons. It is small, irregular in shape, and likely made of rocky material.";
        }

        if (normalized.Contains("asteroid") || normalized.Contains("asteroit"))
        {
            return useTagalog
                ? "Ang asteroid ay maliit na mabatong bagay na umiikot sa Araw. Maraming asteroid ang matatagpuan sa asteroid belt sa pagitan ng Mars at Jupiter."
                : "An asteroid is a small rocky body that orbits the Sun. Many asteroids are found in the asteroid belt between Mars and Jupiter.";
        }

        if (normalized.Contains("absolute zero"))
        {
            return useTagalog
                ? "Ang absolute zero ang pinakamababang posibleng temperatura, na katumbas ng 0 kelvin o -273.15 degrees Celsius. Sa puntong ito, halos wala nang natitirang thermal motion ang mga particle."
                : "Absolute zero is the lowest possible temperature, equal to 0 kelvin or -273.15 degrees Celsius. At this point, particles have almost no remaining thermal motion.";
        }

        if (normalized.Contains("gravity") || asksGravityInTagalog)
        {
            return useTagalog
                ? "Ang gravity ay puwersang humihila sa mga bagay na may mass papalapit sa isa't isa. Ito ang dahilan kung bakit umiikot ang mga planeta sa Araw at ang mga buwan sa kanilang mga planeta."
                : "Gravity is the force that pulls objects with mass toward each other. It is why planets orbit the Sun and moons orbit their planets.";
        }

        if (normalized.Contains("vacuum"))
        {
            return useTagalog
                ? "Ang vacuum ay rehiyon na halos walang matter. Sa kalawakan, malapit ito sa vacuum, bagaman mayroon pa ring kaunting particle at radiation."
                : "A vacuum is a region with very little matter. Space is close to a vacuum, although it still contains small numbers of particles and radiation.";
        }

        if (normalized.Contains("light year"))
        {
            return useTagalog
                ? "Ang light-year ay yunit ng distansya, hindi ng oras. Ito ang layo na nalalakbay ng liwanag sa loob ng isang taon."
                : "A light-year is a unit of distance, not time. It is the distance that light travels in one year.";
        }

        if (normalized.Contains("galaxy") || asksGalaxyInTagalog)
        {
            return useTagalog
                ? "Ang galaxy ay napakalaking pangkat ng mga bituin, gas, alikabok, at dark matter na pinagbubuklod ng gravity. Bahagi ng Milky Way galaxy ang ating Solar System."
                : "A galaxy is a huge collection of stars, gas, dust, and dark matter held together by gravity. Our Solar System is part of the Milky Way galaxy.";
        }

        if (normalized.Contains("black hole"))
        {
            return useTagalog
                ? "Ang black hole ay bahagi ng kalawakan kung saan napakalakas ng gravity kaya kahit liwanag ay hindi makatakas. Madalas itong nabubuo kapag bumagsak ang napakalalaking bituin."
                : "A black hole is a region in space where gravity is so strong that not even light can escape. They often form when very massive stars collapse.";
        }

        if (normalized.Contains("moon") || asksMoonInTagalog)
        {
            return useTagalog
                ? "Ang buwan ay natural na satellite na umiikot sa isang planeta o dwarf planet. Magkakaiba ang mga buwan sa kanilang ibabaw, atmosphere, at loob na istruktura."
                : "A moon is a natural satellite that orbits a planet or dwarf planet. Different moons can have very different surfaces, atmospheres, and internal structures.";
        }

        if (normalized.Contains("star") || asksStarInTagalog)
        {
            return useTagalog
                ? "Ang bituin ay napakalaking kumikislap na bola ng mainit na gas, karamihan ay hydrogen at helium. Nagniningning ang mga bituin dahil ang fusion sa kanilang ubod ay naglalabas ng napakaraming enerhiya."
                : "A star is a massive glowing sphere of hot gas, mostly hydrogen and helium. Stars shine because fusion in their cores releases large amounts of energy.";
        }

        if (normalized.Contains("planet") || asksPlanetInTagalog)
        {
            return useTagalog
                ? "Ang planeta ay malaking celestial body na umiikot sa isang bituin at may sapat na gravity upang manatiling halos bilog ang hugis. Sa ating Solar System, may mga mabatong planeta at may mga gas giant."
                : "A planet is a large celestial body that orbits a star and has enough gravity to maintain a nearly round shape. In our Solar System, the planets vary from rocky worlds to gas giants.";
        }

        if (normalized.Contains("astronomy") || normalized.Contains("space") || asksSpaceInTagalog)
        {
            return useTagalog
                ? "Ang astronomy ay pag-aaral ng mga bagay at pangyayari sa kalawakan, tulad ng mga planeta, bituin, galaxy, black holes, at ng kabuuang uniberso."
                : "Astronomy is the study of objects and events in space, such as planets, stars, galaxies, black holes, and the universe as a whole.";
        }

        if (asksUniverseInTagalog)
        {
            return useTagalog
                ? "Ang uniberso ay kabuuan ng lahat ng espasyo, panahon, matter, at energy. Kasama rito ang mga galaxy, bituin, planeta, at iba pang cosmic structures."
                : "The universe is everything that exists in space and time, including matter, energy, galaxies, stars, planets, and other cosmic structures.";
        }

        if (normalized.Contains("solar system"))
        {
            return useTagalog
                ? "Kasama sa Solar System ang Araw, walong pangunahing planeta, dwarf planets, mga buwan, asteroid, kometa, at iba pang bagay na pinagbubuklod ng gravity ng Araw."
                : "The Solar System includes the Sun, eight major planets, dwarf planets, moons, asteroids, comets, and other objects bound together by the Sun's gravity.";
        }

        if (normalized.Contains("hello") || normalized.Contains("hi") || normalized.Contains("hey"))
        {
            return useTagalog
                ? "Hello! Matutulungan kita sa mga paksang tungkol sa kalawakan tulad ng mga planeta, bituin, buwan, galaxy, black holes, exoplanets, at Solar System."
                : "Hello! I can help you with space topics like planets, stars, moons, galaxies, black holes, exoplanets, and the Solar System.";
        }

        return useTagalog
            ? "Makakatulong ako sa pagpapaliwanag ng mga paksang may kinalaman sa kalawakan tulad ng mga planeta, buwan, bituin, galaxy, exoplanets, kometa, asteroids, black holes, at Solar System. Magtanong ka ng mas tiyak na astronomy question at tutulungan kita."
            : "I can help explain space-related topics such as planets, moons, stars, galaxies, exoplanets, comets, asteroids, black holes, and the Solar System. Ask me a specific astronomy question and I'll help.";
    }

    private static string BuildMoonCountFallbackReply(string normalized)
    {
        if (!normalized.Contains("moon") && !normalized.Contains("buwan"))
        {
            return null;
        }

        if (normalized.Contains("mars"))
        {
            return "Mars has two natural moons: Phobos and Deimos.";
        }

        if (normalized.Contains("earth"))
        {
            return "Earth has one natural moon, usually called the Moon.";
        }

        if (normalized.Contains("mercury"))
        {
            return "Mercury has no natural moons.";
        }

        if (normalized.Contains("venus"))
        {
            return "Venus has no natural moons.";
        }

        if (normalized.Contains("jupiter"))
        {
            return "Jupiter has many known moons, including the four largest Galilean moons: Io, Europa, Ganymede, and Callisto.";
        }

        if (normalized.Contains("saturn"))
        {
            return "Saturn has many moons, and one of its most famous moons is Titan.";
        }

        if (normalized.Contains("uranus"))
        {
            return "Uranus has many moons, including Titania, Oberon, Umbriel, Ariel, and Miranda.";
        }

        if (normalized.Contains("neptune"))
        {
            return "Neptune has several moons, and its largest moon is Triton.";
        }

        if (normalized.Contains("pluto"))
        {
            return "Pluto has several known moons, including Charon, Nix, Hydra, Kerberos, and Styx.";
        }

        return null;
    }

    private static string BuildTemperatureFallbackReply(string normalized, bool useTagalog)
    {
        bool asksTemperature =
            normalized.Contains("temperature") ||
            normalized.Contains("temp") ||
            normalized.Contains("hot") ||
            normalized.Contains("cold") ||
            normalized.Contains("init") ||
            normalized.Contains("lamig");

        if (!asksTemperature)
        {
            return null;
        }

        if (normalized.Contains("neptune"))
        {
            return useTagalog
                ? "Napakalamig ng Neptune. Ang average temperature nito ay humigit-kumulang 55 kelvin o mga -218 degrees Celsius."
                : "Neptune is extremely cold. Its average temperature is about 55 kelvin, or around -218 degrees Celsius.";
        }

        if (normalized.Contains("uranus"))
        {
            return useTagalog
                ? "Napakalamig din ng Uranus. Maaari itong bumaba sa humigit-kumulang 49 kelvin o mga -224 degrees Celsius."
                : "Uranus is also extremely cold. It can drop to about 49 kelvin, or around -224 degrees Celsius.";
        }

        if (normalized.Contains("venus"))
        {
            return useTagalog
                ? "Napakainit ng Venus. Ang average surface temperature nito ay mga 465 degrees Celsius dahil sa makapal nitong atmosphere."
                : "Venus is extremely hot. Its average surface temperature is about 465 degrees Celsius because of its thick atmosphere.";
        }

        if (normalized.Contains("mercury"))
        {
            return useTagalog
                ? "Matindi ang pagbabago ng temperatura sa Mercury. Sa araw maaari itong umabot sa mga 430 degrees Celsius, at sa gabi maaari itong bumaba sa mga -180 degrees Celsius."
                : "Mercury has extreme temperature changes. In daylight it can reach about 430 degrees Celsius, while at night it can drop to about -180 degrees Celsius.";
        }

        if (normalized.Contains("mars"))
        {
            return useTagalog
                ? "Malamig ang Mars. Ang karaniwang temperatura nito ay mga -63 degrees Celsius, bagaman nagbabago ito depende sa oras at lokasyon."
                : "Mars is cold. Its average temperature is about -63 degrees Celsius, though it changes depending on time and location.";
        }

        if (normalized.Contains("earth"))
        {
            return useTagalog
                ? "Sa Earth, ang average surface temperature ay humigit-kumulang 15 degrees Celsius, pero nag-iiba ito depende sa klima at lokasyon."
                : "On Earth, the average surface temperature is about 15 degrees Celsius, though it varies by climate and location.";
        }

        if (normalized.Contains("sun"))
        {
            return useTagalog
                ? "Napakainit ng Araw. Ang surface temperature nito ay mga 5,500 degrees Celsius, at mas mainit pa nang husto sa core nito."
                : "The Sun is extremely hot. Its surface temperature is about 5,500 degrees Celsius, and its core is far hotter.";
        }

        if (normalized.Contains("absolute zero"))
        {
            return useTagalog
                ? "Ang absolute zero ang pinakamababang posibleng temperatura: 0 kelvin o -273.15 degrees Celsius."
                : "Absolute zero is the lowest possible temperature: 0 kelvin, or -273.15 degrees Celsius.";
        }

        return null;
    }

    private bool TryHandleLanguagePreferenceRequest(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || currentSession == null)
        {
            return false;
        }

        string normalized = message.Trim().ToLowerInvariant();
        if (!IsLanguagePreferenceRequest(normalized))
        {
            return false;
        }

        if (normalized.Contains("tagalog") || normalized.Contains("filipino"))
        {
            currentSession.preferredLanguage = "Tagalog";
            AddMessageToCurrentSession("assistant", "Sige, sasagot ako sa Tagalog hangga't kaya at mananatili ako sa mga paksang may kinalaman sa kalawakan.");
            return true;
        }

        if (normalized.Contains("english"))
        {
            currentSession.preferredLanguage = "English";
            AddMessageToCurrentSession("assistant", "Sure, I will continue answering in English and stay focused on space-related topics.");
            return true;
        }

        return false;
    }

    private static bool IsLanguagePreferenceRequest(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        bool mentionsLanguage = normalized.Contains("tagalog") || normalized.Contains("filipino") || normalized.Contains("english");
        bool asksForSpeakingStyle =
            normalized.Contains("speak") ||
            normalized.Contains("talk") ||
            normalized.Contains("answer") ||
            normalized.Contains("reply") ||
            normalized.Contains("use ") ||
            normalized.Contains("can you") ||
            normalized.Contains("pwede") ||
            normalized.Contains("puwede") ||
            normalized.Contains("mag") ||
            normalized.Contains("gamit") ||
            normalized.Contains("switch") ||
            normalized.Contains("change") ||
            normalized.Contains("back to") ||
            normalized.Contains("go back") ||
            normalized.Contains("return to") ||
            normalized.Contains("continue in") ||
            normalized.Contains("from now on") ||
            normalized.Contains("language");

        return mentionsLanguage && asksForSpeakingStyle;
    }

    private static bool IsGreetingOnlyMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        string normalized = message.Trim().ToLowerInvariant();
        string[] greetings =
        {
            "hi",
            "hello",
            "hey",
            "kumusta",
            "hello po",
            "hi po",
            "hey there"
        };

        foreach (string greeting in greetings)
        {
            if (normalized == greeting)
            {
                return true;
            }
        }

        return false;
    }

    private string GetCurrentLanguage()
    {
        if (currentSession == null || string.IsNullOrWhiteSpace(currentSession.preferredLanguage))
        {
            return "English";
        }

        return currentSession.preferredLanguage;
    }

    private static bool IsTagalogLanguage(string language)
    {
        return !string.IsNullOrWhiteSpace(language) &&
               (language.IndexOf("tagalog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                language.IndexOf("filipino", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool AreCloseKeywordMatch(string token, string keyword)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        string normalizedToken = token.Trim().ToLowerInvariant();
        string normalizedKeyword = keyword.Trim().ToLowerInvariant();

        if (normalizedKeyword.Contains(" "))
        {
            return false;
        }

        if (normalizedToken == normalizedKeyword)
        {
            return true;
        }

        if (normalizedToken.Length < 4 || normalizedKeyword.Length < 4)
        {
            return false;
        }

        int lengthDifference = Mathf.Abs(normalizedToken.Length - normalizedKeyword.Length);
        if (lengthDifference > 1)
        {
            return false;
        }

        return ComputeEditDistance(normalizedToken, normalizedKeyword) <= 1;
    }

    private static int ComputeEditDistance(string a, string b)
    {
        int[,] dp = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++)
        {
            dp[i, 0] = i;
        }

        for (int j = 0; j <= b.Length; j++)
        {
            dp[0, j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Mathf.Min(
                    Mathf.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[a.Length, b.Length];
    }

    private static string TranslateFallbackToTagalog(string englishReply, string normalizedQuestion)
    {
        if (string.IsNullOrWhiteSpace(englishReply))
        {
            return englishReply;
        }

        if (normalizedQuestion.Contains("mars"))
        {
            return "Ang Mars ay may dalawang natural na buwan: Phobos at Deimos.";
        }

        if (normalizedQuestion.Contains("earth"))
        {
            return "Ang Earth ay may isang natural na buwan, na karaniwang tinatawag na Moon.";
        }

        if (normalizedQuestion.Contains("mercury"))
        {
            return "Walang natural na buwan ang Mercury.";
        }

        if (normalizedQuestion.Contains("venus"))
        {
            return "Walang natural na buwan ang Venus.";
        }

        if (normalizedQuestion.Contains("jupiter"))
        {
            return "Maraming kilalang buwan ang Jupiter, kabilang ang apat na pinakamalaking Galilean moons: Io, Europa, Ganymede, at Callisto.";
        }

        if (normalizedQuestion.Contains("saturn"))
        {
            return "Maraming buwan ang Saturn, at isa sa pinakatanyag nito ay ang Titan.";
        }

        if (normalizedQuestion.Contains("uranus"))
        {
            return "Maraming buwan ang Uranus, kabilang ang Titania, Oberon, Umbriel, Ariel, at Miranda.";
        }

        if (normalizedQuestion.Contains("neptune"))
        {
            return "May ilang buwan ang Neptune, at ang pinakamalaki rito ay ang Triton.";
        }

        if (normalizedQuestion.Contains("pluto"))
        {
            return "May ilang kilalang buwan ang Pluto, kabilang ang Charon, Nix, Hydra, Kerberos, at Styx.";
        }

        return englishReply;
    }

    private ChatSessionData CreateEmptySession(string preferredLanguage = "English")
    {
        ChatSessionData session = new ChatSessionData
        {
            sessionId = Guid.NewGuid().ToString("N"),
            title = "New Chat",
            createdAtUtc = DateTime.UtcNow.ToString("o"),
            updatedAtUtc = DateTime.UtcNow.ToString("o"),
            preferredLanguage = string.IsNullOrWhiteSpace(preferredLanguage) ? "English" : preferredLanguage,
            messages = new List<ChatMessageData>()
        };

        session.messages.Add(new ChatMessageData
        {
            role = "assistant",
            text = BuildWelcomeMessage(session.preferredLanguage),
            timestampUtc = DateTime.UtcNow.ToString("o")
        });

        return session;
    }

    private static string BuildWelcomeMessage(string language)
    {
        return IsTagalogLanguage(language)
            ? "Hello! Ako ang AstroLearn AI Chatbot. Maaari kang magtanong tungkol sa kalawakan, mga planeta, buwan, bituin, galaxy, at Solar System."
            : "Hello! I am the AstroLearn AI Chatbot. You can ask me about space, planets, moons, stars, galaxies, and the Solar System.";
    }

    private ChatSessionData FindSessionById(string sessionId)
    {
        if (sessions == null || sessions.sessions == null || string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        return sessions.sessions.FirstOrDefault(session => session != null && session.sessionId == sessionId);
    }

    private ChatSessionData FindMostRecentSession()
    {
        if (sessions == null || sessions.sessions == null || sessions.sessions.Count == 0)
        {
            return null;
        }

        return sessions.sessions
            .Where(session => session != null)
            .OrderByDescending(GetSessionSortKey)
            .FirstOrDefault();
    }

    private static DateTime GetSessionSortKey(ChatSessionData session)
    {
        if (session == null || string.IsNullOrWhiteSpace(session.updatedAtUtc))
        {
            return DateTime.MinValue;
        }

        return DateTime.TryParse(session.updatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed)
            ? parsed
            : DateTime.MinValue;
    }

    private void ClearSpawnedItems(List<GameObject> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
            {
                Destroy(items[i]);
            }
        }

        items.Clear();
    }

    private GameObject CreateMessageRow(bool isUserMessage, float bubbleHeight)
    {
        if (messagesContent == null)
        {
            return null;
        }

        GameObject row = new GameObject(isUserMessage ? "UserMessageRow" : "BotMessageRow", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        RectTransform rowRect = row.transform as RectTransform;
        rowRect.SetParent(messagesContent, false);
        rowRect.localScale = Vector3.one;
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.sizeDelta = new Vector2(0f, bubbleHeight);

        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.ignoreLayout = false;
        rowLayout.preferredHeight = bubbleHeight;
        rowLayout.minHeight = bubbleHeight;
        rowLayout.flexibleHeight = 0f;

        HorizontalLayoutGroup rowGroup = row.GetComponent<HorizontalLayoutGroup>();
        rowGroup.childAlignment = isUserMessage ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        rowGroup.childControlWidth = false;
        rowGroup.childControlHeight = false;
        rowGroup.childForceExpandWidth = false;
        rowGroup.childForceExpandHeight = false;
        rowGroup.spacing = 0f;
        rowGroup.padding = isUserMessage
            ? new RectOffset(40, 24, 0, 0)
            : new RectOffset(170, 40, 0, 0);

        return row;
    }

    private void PrepareMessageBubbleForLayout(RectTransform itemRect, float bubbleWidth, float bubbleHeight)
    {
        if (itemRect == null)
        {
            return;
        }

        itemRect.localScale = Vector3.one;
        itemRect.anchorMin = new Vector2(0.5f, 0.5f);
        itemRect.anchorMax = new Vector2(0.5f, 0.5f);
        itemRect.pivot = new Vector2(0.5f, 0.5f);
        itemRect.anchoredPosition = Vector2.zero;
        itemRect.sizeDelta = new Vector2(bubbleWidth, bubbleHeight);

        LayoutElement layoutElement = itemRect.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = itemRect.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.ignoreLayout = false;
        layoutElement.preferredWidth = bubbleWidth;
        layoutElement.minWidth = bubbleWidth;
        layoutElement.preferredHeight = bubbleHeight;
        layoutElement.minHeight = bubbleHeight;
        layoutElement.flexibleHeight = 0f;
        layoutElement.flexibleWidth = 0f;
    }

    private void PrepareHistoryItemForLayout(RectTransform itemRect)
    {
        if (itemRect == null)
        {
            return;
        }

        itemRect.SetParent(historyContent, false);
        itemRect.localScale = Vector3.one;
        itemRect.anchorMin = new Vector2(0f, 1f);
        itemRect.anchorMax = new Vector2(1f, 1f);
        itemRect.pivot = new Vector2(0.5f, 1f);

        LayoutElement layoutElement = itemRect.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = itemRect.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.ignoreLayout = false;
        layoutElement.preferredHeight = itemRect.sizeDelta.y > 0f ? itemRect.sizeDelta.y : 110f;
        layoutElement.minHeight = layoutElement.preferredHeight;
        layoutElement.flexibleHeight = 0f;
    }

    private void ScrollMessagesToBottom()
    {
        if (messagesScrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        messagesScrollRect.normalizedPosition = new Vector2(0f, 0f);
    }

    private void NormalizeChatRootTransform()
    {
        if (chatbotRoot == null)
        {
            return;
        }

        RectTransform rectTransform = chatbotRoot.transform as RectTransform;
        if (rectTransform != null && rectTransform.localScale.sqrMagnitude < 0.01f)
        {
            rectTransform.localScale = Vector3.one;
        }
    }

    private static void ConfigureMessageText(TextMeshProUGUI text)
    {
        if (text == null)
        {
            return;
        }

        text.enableAutoSizing = true;
        text.fontSizeMin = 22f;
        text.fontSizeMax = 34f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private static void ConfigureTimeText(TextMeshProUGUI text)
    {
        if (text == null)
        {
            return;
        }

        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 22f;
    }

    private static string FormatChatTime(string timestampUtc)
    {
        if (!DateTime.TryParse(timestampUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
        {
            return string.Empty;
        }

        return parsed.ToLocalTime().ToString("h:mm tt");
    }

    private static string FormatHistoryTime(string timestampUtc)
    {
        if (!DateTime.TryParse(timestampUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
        {
            return string.Empty;
        }

        DateTime local = parsed.ToLocalTime();
        DateTime now = DateTime.Now;

        if (local.Date == now.Date)
        {
            return local.ToString("h:mm tt");
        }

        if (local.Date == now.Date.AddDays(-1))
        {
            return "Yesterday";
        }

        return local.ToString("MMM dd");
    }

    private static Button FindSiblingButton(Transform parent, GameObject excludeObject)
    {
        if (parent == null)
        {
            return null;
        }

        Button[] buttons = parent.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null || button.gameObject == excludeObject)
            {
                continue;
            }

            return button;
        }

        return null;
    }

    private static ScrollRect FindScrollRectWithContentName(GameObject root, string contentName)
    {
        if (root == null || string.IsNullOrWhiteSpace(contentName))
        {
            return null;
        }

        ScrollRect[] scrollRects = root.GetComponentsInChildren<ScrollRect>(true);
        foreach (ScrollRect scrollRect in scrollRects)
        {
            if (scrollRect != null && scrollRect.content != null && scrollRect.content.gameObject.name == contentName)
            {
                return scrollRect;
            }
        }

        return null;
    }

    private static ScrollRect FindScrollRectContaining(GameObject root, GameObject child)
    {
        if (root == null || child == null)
        {
            return null;
        }

        ScrollRect[] scrollRects = root.GetComponentsInChildren<ScrollRect>(true);
        foreach (ScrollRect scrollRect in scrollRects)
        {
            if (scrollRect == null || scrollRect.content == null)
            {
                continue;
            }

            if (child.transform.IsChildOf(scrollRect.content))
            {
                return scrollRect;
            }
        }

        return null;
    }

    private static TMP_InputField FindInputField(GameObject root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        TMP_InputField[] inputFields = root.GetComponentsInChildren<TMP_InputField>(true);
        foreach (TMP_InputField inputField in inputFields)
        {
            if (inputField != null && inputField.gameObject.name == objectName)
            {
                return inputField;
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

    private static Button FindButtonByChildName(GameObject root, string childObjectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childObjectName))
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

            if (FindObjectByNameWithin(button.gameObject, childObjectName) != null)
            {
                return button;
            }
        }

        return null;
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

            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null && string.Equals(text.text.Trim(), childText, StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }

    private static TextMeshProUGUI FindText(GameObject root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI text in texts)
        {
            if (text != null && text.gameObject.name == objectName)
            {
                return text;
            }
        }

        return null;
    }

    private static TextMeshProUGUI FindMessageText(GameObject root, TextMeshProUGUI timeText)
    {
        TextMeshProUGUI direct = FindText(root, "MessageText");
        if (direct != null && direct != timeText)
        {
            return direct;
        }

        TextMeshProUGUI[] texts = root != null ? root.GetComponentsInChildren<TextMeshProUGUI>(true) : null;
        if (texts == null || texts.Length == 0)
        {
            return null;
        }

        TextMeshProUGUI best = null;
        float bestScore = float.MinValue;
        foreach (TextMeshProUGUI candidate in texts)
        {
            if (candidate == null || candidate == timeText)
            {
                continue;
            }

            RectTransform rect = candidate.transform as RectTransform;
            float area = rect != null ? Mathf.Abs(rect.rect.width * rect.rect.height) : 0f;
            float score = area + candidate.fontSizeMax;
            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best ?? texts.FirstOrDefault(text => text != timeText);
    }

    private static TextMeshProUGUI FindTimeText(GameObject root)
    {
        TextMeshProUGUI direct = FindText(root, "TimeText");
        if (direct != null)
        {
            return direct;
        }

        return FindLastText(root);
    }

    private static TextMeshProUGUI FindFirstText(GameObject root)
    {
        return root != null ? root.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault() : null;
    }

    private static TextMeshProUGUI FindLastText(GameObject root)
    {
        TextMeshProUGUI[] texts = root != null ? root.GetComponentsInChildren<TextMeshProUGUI>(true) : null;
        return texts != null && texts.Length > 0 ? texts[texts.Length - 1] : null;
    }

    private static RectTransform FindRectTransform(GameObject root, string objectName)
    {
        GameObject found = FindObjectByNameWithin(root, objectName);
        return found != null ? found.transform as RectTransform : null;
    }

    private static GameObject FindObjectByNameWithin(GameObject root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform transform in transforms)
        {
            if (transform != null && transform.name == objectName)
            {
                return transform.gameObject;
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
        foreach (GameObject sceneObject in objects)
        {
            if (sceneObject == null || sceneObject.hideFlags != HideFlags.None || !sceneObject.scene.IsValid())
            {
                continue;
            }

            if (sceneObject.name == objectName)
            {
                return sceneObject;
            }
        }

        return null;
    }

    private static GameObject GetTopLevelSceneParent(GameObject child)
    {
        if (child == null)
        {
            return null;
        }

        Transform current = child.transform;
        while (current.parent != null && current.parent.gameObject.scene.IsValid())
        {
            current = current.parent;
        }

        return current.gameObject;
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }
    }

    private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }
}
