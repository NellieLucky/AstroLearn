using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class QuizFlowController : MonoBehaviour
{
    private const int DefaultQuestionCount = 10;
    private const int LocalGeneratedQuestionCount = 4;
    private const int OllamaQuizTimeoutSeconds = 0;
    private const int OllamaQuizMaxTokens = 480;

    public static QuizFlowController Instance { get; private set; }

    private GameObject quizTopicPage;
    private GameObject quizIntroPage;
    private GameObject quizQuestionPage;
    private GameObject quizResultPage;
    private GameObject quizBreakdownPage;
    private GameObject quizHistoryPage;
    private GameObject quizHomePage;

    private TextMeshProUGUI questionText;
    private TextMeshProUGUI scoreValueText;
    private TextMeshProUGUI questionScoreLabelText;
    private TextMeshProUGUI questionProgressText;
    private readonly Button[] answerButtons = new Button[4];
    private readonly TextMeshProUGUI[] answerTexts = new TextMeshProUGUI[4];
    private Button questionBackButton;
    private CircularQuizTimer questionTimer;

    private TextMeshProUGUI resultScoreValueText;
    private TextMeshProUGUI resultTitleText;
    private TextMeshProUGUI resultOutOfText;
    private Button resultBackButton;
    private Button resultMoreQuizzesButton;
    private Button resultRestartButton;
    private Button resultReviewButton;

    private TextMeshProUGUI breakdownTitleText;
    private TextMeshProUGUI breakdownProgressText;
    private TextMeshProUGUI breakdownScoreValueText;
    private TextMeshProUGUI breakdownResultValueText;
    private Button breakdownBackButton;
    private Button breakdownPreviousButton;
    private Button breakdownNextButton;
    private ScrollRect breakdownScrollRect;
    private RectTransform breakdownContent;
    private RectTransform breakdownTemplateCard;
    private Scrollbar breakdownVerticalScrollbar;
    private readonly List<GameObject> generatedBreakdownCards = new List<GameObject>();

    private ScrollRect resultBreakdownScrollRect;
    private RectTransform resultBreakdownContent;
    private RectTransform resultBreakdownTemplateCard;
    private Scrollbar resultBreakdownVerticalScrollbar;
    private readonly List<GameObject> generatedResultBreakdownCards = new List<GameObject>();

    private sealed class TopicFactProfile
    {
        public string Key;
        public string DisplayName;
        public string TypeLabel;
        public string RelationFact;
        public string FeatureFact;
        public string AtmosphereFact;
        public string SystemFact;
        public string DistinctionFact;
        public string[] Keywords;
    }

    private QuizSessionData currentSession;
    private int breakdownQuestionIndex;
    private bool isGeneratingQuiz;
    private bool answerLocked;
    private bool isReviewingHistoryAttempt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<QuizFlowController>() != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject("QuizFlowController");
        controllerObject.AddComponent<QuizFlowController>();
    }

    private void Awake()
    {
        QuizFlowController[] controllers = FindObjectsByType<QuizFlowController>(FindObjectsSortMode.None);
        if (controllers.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
        RegisterListeners();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnregisterListeners();
    }

    public void BeginQuiz(string topic, string difficulty, Action<bool, string> onComplete = null)
    {
        if (isGeneratingQuiz)
        {
            onComplete?.Invoke(false, "Quiz generation is already in progress.");
            return;
        }

        if (string.IsNullOrWhiteSpace(topic))
        {
            onComplete?.Invoke(false, "Please select a topic first.");
            return;
        }

        StartCoroutine(BeginQuizRoutine(topic.Trim(), string.IsNullOrWhiteSpace(difficulty) ? "Easy" : difficulty.Trim(), onComplete));
    }

    public QuizSessionData GetCurrentSession()
    {
        return currentSession;
    }

    public void RestartCurrentQuiz()
    {
        if (currentSession == null)
        {
            return;
        }

        currentSession.score = 0;
        currentSession.currentQuestionIndex = 0;
        currentSession.isComplete = false;
        currentSession.completedAtUtc = string.Empty;
        currentSession.selectedAnswers = Enumerable.Repeat(string.Empty, currentSession.questions.Count).ToList();
        PersistSessionIfGuest();
        ShowQuestionPage();
        RenderCurrentQuestion();
    }

    private IEnumerator BeginQuizRoutine(string topic, string difficulty, Action<bool, string> onComplete)
    {
        isGeneratingQuiz = true;

        QuizSessionData generatedSession = null;
        string statusMessage = "Using local quiz fallback.";
        yield return StartCoroutine(GenerateQuizRoutine(topic, difficulty, session =>
        {
            generatedSession = session;
            statusMessage = session != null ? "Quiz generated successfully." : "Unable to generate quiz.";
        }));

        isGeneratingQuiz = false;

        if (generatedSession == null)
        {
            onComplete?.Invoke(false, statusMessage);
            yield break;
        }

        currentSession = generatedSession;
        EnsureSelectedAnswerSlots(currentSession);
        PersistSessionIfGuest();
        ShowQuestionPage();
        RenderCurrentQuestion();
        onComplete?.Invoke(true, statusMessage);
    }

    private IEnumerator GenerateQuizRoutine(string topic, string difficulty, Action<QuizSessionData> onGenerated)
    {
        QuizSessionData session = null;
        HashSet<string> recentQuestionKeys = BuildRecentQuestionKeySet(topic, difficulty);

        string endpoint = EnvFileLoader.Get("GEMINI_QUIZ_ENDPOINT");
        string bearerToken = EnvFileLoader.Get("GEMINI_QUIZ_BEARER_TOKEN");
        string anonKey = EnvFileLoader.Get("SUPABASE_ANON_KEY");
        string ollamaEndpoint = EnvFileLoader.Get(
            "OLLAMA_QUIZ_ENDPOINT",
            EnvFileLoader.Get("OLLAMA_CHAT_ENDPOINT", "http://127.0.0.1:11434/api/generate"));
        string ollamaModel = EnvFileLoader.Get(
            "OLLAMA_QUIZ_MODEL",
            EnvFileLoader.Get("OLLAMA_CHAT_MODEL", "llama3.2:3b"));

        bool canUseLocalQuizModel = !string.IsNullOrWhiteSpace(ollamaEndpoint) && !string.IsNullOrWhiteSpace(ollamaModel);
        if (canUseLocalQuizModel)
        {
            Debug.Log("[QuizFlowController] Trying local Ollama quiz generation first.");
            yield return StartCoroutine(RequestQuizFromOllama(ollamaEndpoint.Trim(), ollamaModel.Trim(), topic, difficulty, result => session = result));
        }

        if (session == null && !string.IsNullOrWhiteSpace(endpoint))
        {
            Debug.Log("[QuizFlowController] Ollama quiz generation unavailable. Trying Gemini endpoint.");
            yield return StartCoroutine(RequestQuizFromEndpoint(endpoint.Trim(), bearerToken, anonKey, topic, difficulty, result => session = result));
        }

        if (session == null)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            session = BuildFallbackQuiz(topic, difficulty, recentQuestionKeys);
        }
        else
        {
            session = RemoveDuplicateQuestions(session, recentQuestionKeys);
            FillMissingQuestionsFromFallback(session, recentQuestionKeys);
        }

        onGenerated?.Invoke(session);
    }

    private IEnumerator RequestQuizFromEndpoint(string endpoint, string bearerToken, string anonKey, string topic, string difficulty, Action<QuizSessionData> onResult)
    {
        QuizGenerationRequest requestPayload = new QuizGenerationRequest
        {
            topic = topic,
            difficulty = difficulty,
            questionCount = DefaultQuestionCount
        };

        byte[] requestBody = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestPayload));

        using (UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(requestBody);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 45;
            request.SetRequestHeader("Content-Type", "application/json");

            string authToken = !string.IsNullOrWhiteSpace(bearerToken)
                ? bearerToken.Trim()
                : anonKey.Trim();

            if (!string.IsNullOrWhiteSpace(anonKey))
            {
                request.SetRequestHeader("apikey", anonKey.Trim());
            }

            if (!string.IsNullOrWhiteSpace(authToken))
            {
                request.SetRequestHeader("Authorization", "Bearer " + authToken);
            }

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[QuizFlowController] Gemini quiz request failed: " + request.error);
                onResult?.Invoke(null);
                yield break;
            }

            string responseJson = request.downloadHandler.text;
            QuizSessionData parsedSession = ParseQuizSession(responseJson, topic, difficulty);
            parsedSession = SanitizeGeneratedSession(parsedSession, topic, difficulty);
            if (!ValidateSession(parsedSession))
            {
                Debug.LogWarning("[QuizFlowController] Gemini quiz response was invalid. Falling back to local quiz.");
                onResult?.Invoke(null);
                yield break;
            }

            onResult?.Invoke(parsedSession);
        }
    }

    private IEnumerator RequestQuizFromOllama(string endpoint, string model, string topic, string difficulty, Action<QuizSessionData> onResult)
    {
        string prompt = BuildLocalQuizPrompt(topic, difficulty);
        Debug.Log("[QuizFlowController] Sending quiz request to Ollama model: " + model);
        float requestStartedAt = Time.realtimeSinceStartup;

        OllamaGenerateRequestPayload payload = new OllamaGenerateRequestPayload
        {
            model = model,
            prompt = prompt,
            format = "json",
            stream = false,
            keep_alive = "30m",
            options = new OllamaOptionsPayload
            {
                num_predict = OllamaQuizMaxTokens,
                temperature = 0.2f
            }
        };

        byte[] requestBody = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

        using (UnityWebRequest request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(requestBody);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = OllamaQuizTimeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                float elapsedSeconds = Time.realtimeSinceStartup - requestStartedAt;
                Debug.LogWarning("[QuizFlowController] Ollama quiz request failed after " + elapsedSeconds.ToString("0.0") + "s: " + request.error);
                onResult?.Invoke(null);
                yield break;
            }

            string responseText = request.downloadHandler != null ? request.downloadHandler.text : null;
            if (string.IsNullOrWhiteSpace(responseText))
            {
                Debug.LogWarning("[QuizFlowController] Ollama quiz response was empty.");
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
                Debug.LogWarning("[QuizFlowController] Ollama quiz response parse failed: " + exception.Message);
            }

            if (response != null && !string.IsNullOrWhiteSpace(response.error))
            {
                Debug.LogWarning("[QuizFlowController] Ollama quiz response error: " + response.error);
                onResult?.Invoke(null);
                yield break;
            }

            string normalizedJson = NormalizeOllamaQuizJson(response != null ? response.response : null);
            QuizSessionData parsedSession = ParseQuizSession(normalizedJson, topic, difficulty);
            parsedSession = SanitizeGeneratedSession(parsedSession, topic, difficulty);
            if (!ValidateSession(parsedSession, 1, DefaultQuestionCount))
            {
                Debug.LogWarning("[QuizFlowController] Ollama quiz response was invalid. Falling back to built-in quiz.");
                onResult?.Invoke(null);
                yield break;
            }

            Debug.Log("[QuizFlowController] Ollama quiz generated " + parsedSession.questions.Count + " question(s) in " + (Time.realtimeSinceStartup - requestStartedAt).ToString("0.0") + "s.");

            onResult?.Invoke(parsedSession);
        }
    }

    private void RenderCurrentQuestion()
    {
        if (currentSession == null || currentSession.questions == null || currentSession.questions.Count == 0)
        {
            return;
        }

        currentSession.currentQuestionIndex = Mathf.Clamp(currentSession.currentQuestionIndex, 0, currentSession.questions.Count - 1);
        EnsureSelectedAnswerSlots(currentSession);
        ResetAnswerButtonVisuals();
        answerLocked = false;

        QuizQuestionData questionData = currentSession.questions[currentSession.currentQuestionIndex];

        if (questionText != null)
        {
            questionText.text = questionData.question;
        }

        if (scoreValueText != null)
        {
            scoreValueText.text = currentSession.score.ToString();
        }

        if (questionScoreLabelText != null)
        {
            questionScoreLabelText.text = $"Score : {currentSession.score}";
        }

        if (questionProgressText != null)
        {
            questionProgressText.text = $"{currentSession.currentQuestionIndex + 1} / {currentSession.questions.Count}";
        }

        for (int i = 0; i < answerButtons.Length; i++)
        {
            bool hasChoice = questionData.choices != null && i < questionData.choices.Count;
            if (answerButtons[i] != null)
            {
                answerButtons[i].interactable = hasChoice;
            }

            if (answerTexts[i] != null)
            {
                answerTexts[i].text = hasChoice ? questionData.choices[i] : string.Empty;
            }
        }

        if (questionTimer != null)
        {
            questionTimer.ResetTimer();
            questionTimer.StartTimer();
        }

        PersistSessionIfGuest();
    }

    private void SubmitAnswer(int answerIndex)
    {
        if (answerLocked || currentSession == null || currentSession.questions == null)
        {
            return;
        }

        QuizQuestionData questionData = currentSession.questions[currentSession.currentQuestionIndex];
        if (questionData.choices == null || answerIndex < 0 || answerIndex >= questionData.choices.Count)
        {
            return;
        }

        answerLocked = true;

        string selectedAnswer = questionData.choices[answerIndex];
        currentSession.selectedAnswers[currentSession.currentQuestionIndex] = selectedAnswer;

        bool isCorrect = string.Equals(selectedAnswer, questionData.correctAnswer, StringComparison.OrdinalIgnoreCase);
        if (isCorrect)
        {
            currentSession.score++;
        }

        if (scoreValueText != null)
        {
            scoreValueText.text = currentSession.score.ToString();
        }

        if (questionScoreLabelText != null)
        {
            questionScoreLabelText.text = $"Score : {currentSession.score}";
        }

        HighlightSubmittedAnswer(answerIndex, questionData.correctAnswer);

        if (questionTimer != null)
        {
            questionTimer.StopTimer();
        }

        PersistSessionIfGuest();
        StartCoroutine(AdvanceAfterAnswerRoutine());
    }

    private IEnumerator AdvanceAfterAnswerRoutine()
    {
        yield return new WaitForSecondsRealtime(0.9f);

        if (currentSession == null)
        {
            yield break;
        }

        if (currentSession.currentQuestionIndex >= currentSession.questions.Count - 1)
        {
            CompleteQuiz();
            yield break;
        }

        currentSession.currentQuestionIndex++;
        RenderCurrentQuestion();
    }

    private void HandleTimerExpired()
    {
        if (answerLocked || currentSession == null)
        {
            return;
        }

        EnsureSelectedAnswerSlots(currentSession);
        currentSession.selectedAnswers[currentSession.currentQuestionIndex] = string.Empty;
        answerLocked = true;
        HighlightSubmittedAnswer(-1, currentSession.questions[currentSession.currentQuestionIndex].correctAnswer);
        PersistSessionIfGuest();
        StartCoroutine(AdvanceAfterAnswerRoutine());
    }

    private void CompleteQuiz()
    {
        if (currentSession == null)
        {
            return;
        }

        currentSession.isComplete = true;
        currentSession.completedAtUtc = DateTime.UtcNow.ToString("o");
        PersistSessionIfGuest();

        if (SupabaseAuthService.Instance != null && SupabaseAuthService.Instance.IsGuestMode)
        {
            GuestQuizStorage.AppendHistory(new QuizHistoryEntry
            {
                sessionId = currentSession.sessionId,
                topic = currentSession.topic,
                difficulty = currentSession.difficulty,
                score = currentSession.score,
                totalQuestions = currentSession.questions != null ? currentSession.questions.Count : 0,
                createdAtUtc = currentSession.createdAtUtc,
                completedAtUtc = currentSession.completedAtUtc,
                selectedAnswers = CloneStringList(currentSession.selectedAnswers),
                questions = CloneQuestions(currentSession.questions)
            });
        }

        ShowResultPage();
        RenderResultPage();
    }

    private void RenderResultPage()
    {
        if (currentSession == null)
        {
            return;
        }

        if (resultScoreValueText != null)
        {
            resultScoreValueText.text = currentSession.score.ToString();
        }

        if (resultOutOfText != null)
        {
            resultOutOfText.text = $"{currentSession.score} / {currentSession.questions.Count}";
        }

        if (resultTitleText != null)
        {
            resultTitleText.text = $"{currentSession.topic.ToUpper()} {currentSession.difficulty.ToUpper()} QUIZ";
        }

        RenderResultBreakdownCards();
    }

    private void RenderBreakdownPage()
    {
        if (currentSession == null || currentSession.questions == null || currentSession.questions.Count == 0)
        {
            return;
        }

        if (breakdownTitleText != null)
        {
            breakdownTitleText.text = "Quiz Breakdown";
        }

        if (breakdownProgressText != null)
        {
            breakdownProgressText.text = $"{currentSession.score} / {currentSession.questions.Count}";
        }

        if (breakdownScoreValueText != null)
        {
            breakdownScoreValueText.text = currentSession.score.ToString();
        }

        if (breakdownResultValueText != null)
        {
            breakdownResultValueText.text = $"{currentSession.score} / {currentSession.questions.Count}";
        }

        if (breakdownPreviousButton != null)
        {
            breakdownPreviousButton.gameObject.SetActive(false);
        }

        if (breakdownNextButton != null)
        {
            breakdownNextButton.gameObject.SetActive(false);
        }

        RenderBreakdownCards();
    }

    private void HandleBreakdownNext()
    {
        if (isReviewingHistoryAttempt)
        {
            ShowHistoryPage();
        }
        else
        {
            ShowResultPage();
        }
    }

    private void HandleBreakdownPrevious()
    {
        HandleBreakdownBack();
    }

    private void HandleQuestionBack()
    {
        if (questionTimer != null)
        {
            questionTimer.StopTimer();
        }

        ShowSelectionPageForCurrentContext();
    }

    private void ShowQuestionPage()
    {
        SetQuizPageState(quizQuestionPage);
    }

    private void ShowResultPage()
    {
        isReviewingHistoryAttempt = false;
        SetQuizPageState(quizResultPage);
    }

    private void ShowBreakdownPage()
    {
        isReviewingHistoryAttempt = false;
        SetQuizPageState(quizBreakdownPage);
        RenderBreakdownPage();
    }

    private void ShowTopicPage()
    {
        isReviewingHistoryAttempt = false;
        SetQuizPageState(quizTopicPage);
    }

    private void HandleResultBack()
    {
        isReviewingHistoryAttempt = false;

        if (TryRestoreFocusedCelestialBodyView())
        {
            return;
        }

        ShowTopicPage();
    }

    private void HandleMoreQuizzes()
    {
        isReviewingHistoryAttempt = false;

        if (ShowSelectionPageForCurrentContext())
        {
            return;
        }

        ShowTopicPage();
    }

    private bool ShowSelectionPageForCurrentContext()
    {
        AuthUIManager authUiManager = FindFirstObjectByType<AuthUIManager>();
        if (authUiManager != null &&
            authUiManager.IsFocusedCelestialBodyQuizFlowActive() &&
            currentSession != null &&
            !string.IsNullOrWhiteSpace(currentSession.topic))
        {
            authUiManager.OpenQuizHomeForTopic(currentSession.topic);
            return true;
        }

        ShowTopicPage();
        return true;
    }

    private bool TryRestoreFocusedCelestialBodyView()
    {
        AuthUIManager authUiManager = FindFirstObjectByType<AuthUIManager>();
        if (authUiManager == null)
        {
            return false;
        }

        return authUiManager.TryRestoreFocusedCelestialBodyFromQuiz();
    }

    private void ShowHistoryPage()
    {
        SetQuizPageState(quizHistoryPage);
    }

    private void SetQuizPageState(GameObject activePage)
    {
        SetPageActive(quizTopicPage, activePage == quizTopicPage);
        SetPageActive(quizIntroPage, activePage == quizIntroPage);
        SetPageActive(quizQuestionPage, activePage == quizQuestionPage);
        SetPageActive(quizResultPage, activePage == quizResultPage);
        SetPageActive(quizBreakdownPage, activePage == quizBreakdownPage);
        SetPageActive(quizHistoryPage, activePage == quizHistoryPage);
        SetPageActive(quizHomePage, activePage == quizHomePage);
    }

    private void PersistSessionIfGuest()
    {
        if (currentSession == null || SupabaseAuthService.Instance == null || !SupabaseAuthService.Instance.IsGuestMode)
        {
            return;
        }

        GuestQuizStorage.SaveCurrentSession(currentSession);
    }

    private void RegisterListeners()
    {
        for (int i = 0; i < answerButtons.Length; i++)
        {
            Button button = answerButtons[i];
            if (button == null)
            {
                continue;
            }

            int capturedIndex = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SubmitAnswer(capturedIndex));
        }

        if (questionBackButton != null)
        {
            questionBackButton.onClick.RemoveAllListeners();
            questionBackButton.onClick.AddListener(HandleQuestionBack);
        }

        if (questionTimer != null)
        {
            questionTimer.TimerExpired -= HandleTimerExpired;
            questionTimer.TimerExpired += HandleTimerExpired;
        }

        if (resultBackButton != null)
        {
            resultBackButton.onClick.RemoveAllListeners();
            resultBackButton.onClick.AddListener(HandleResultBack);
        }

        if (resultMoreQuizzesButton != null)
        {
            resultMoreQuizzesButton.onClick.RemoveAllListeners();
            resultMoreQuizzesButton.onClick.AddListener(HandleMoreQuizzes);
        }

        if (resultRestartButton != null)
        {
            resultRestartButton.onClick.RemoveAllListeners();
            resultRestartButton.onClick.AddListener(RestartCurrentQuiz);
        }

        if (resultReviewButton != null)
        {
            resultReviewButton.onClick.RemoveAllListeners();
            resultReviewButton.onClick.AddListener(ShowBreakdownPage);
        }

        if (breakdownBackButton != null)
        {
            breakdownBackButton.onClick.RemoveAllListeners();
            breakdownBackButton.onClick.AddListener(HandleBreakdownBack);
        }

        if (breakdownPreviousButton != null)
        {
            breakdownPreviousButton.onClick.RemoveAllListeners();
            breakdownPreviousButton.onClick.AddListener(HandleBreakdownPrevious);
        }

        if (breakdownNextButton != null)
        {
            breakdownNextButton.onClick.RemoveAllListeners();
            breakdownNextButton.onClick.AddListener(HandleBreakdownNext);
        }
    }

    private void UnregisterListeners()
    {
        if (questionTimer != null)
        {
            questionTimer.TimerExpired -= HandleTimerExpired;
        }
    }

    private void ResolveReferences()
    {
        quizTopicPage = FindObjectByName("QuizTopicPage");
        quizIntroPage = FindObjectByName("QuizIntroPage");
        quizQuestionPage = FindObjectByName("QuizQuestionPage");
        quizResultPage = FindObjectByName("QuizResultPage");
        quizBreakdownPage = FindObjectByName("QuizBreakdownPage");
        quizHistoryPage = FindObjectByName("QuizHistoryPage");
        quizHomePage = FindObjectByName("QuizHomePage");

        questionText = FindText(quizQuestionPage, "Question") ?? FindText(quizQuestionPage, "QuestionText");
        scoreValueText = FindText(quizQuestionPage, "ScoreValueText");
        questionScoreLabelText = FindText(quizQuestionPage, "ScoreText");
        questionProgressText = FindText(quizQuestionPage, "ProgressText")
            ?? FindText(quizQuestionPage, "CorrectText")
            ?? FindTextContaining(quizQuestionPage, "/");
        answerButtons[0] = FindButton(quizQuestionPage, "AnswerAButton");
        answerButtons[1] = FindButton(quizQuestionPage, "AnswerBButton");
        answerButtons[2] = FindButton(quizQuestionPage, "AnswerCButton");
        answerButtons[3] = FindButton(quizQuestionPage, "AnswerDButton");
        answerTexts[0] = FindText(quizQuestionPage, "AnswerAText");
        answerTexts[1] = FindText(quizQuestionPage, "AnswerBText");
        answerTexts[2] = FindText(quizQuestionPage, "AnswerCText");
        answerTexts[3] = FindText(quizQuestionPage, "AnswerDText");
        questionBackButton = FindButton(quizQuestionPage, "BackButton");
        questionTimer = quizQuestionPage != null ? quizQuestionPage.GetComponentInChildren<CircularQuizTimer>(true) : null;

        for (int i = 0; i < answerTexts.Length; i++)
        {
            ConfigureAnswerText(answerTexts[i]);
        }

        resultScoreValueText = FindText(quizResultPage, "ScoreValueText");
        resultTitleText = FindText(quizResultPage, "TitleText");
        resultOutOfText = FindText(quizResultPage, "CorrectText") ?? FindTextContaining(quizResultPage, "/");
        resultBackButton = FindButton(quizResultPage, "BackButton");
        resultMoreQuizzesButton = FindButton(quizResultPage, "MoreQuizzesButton");
        resultRestartButton = FindButtonByChildText(quizResultPage, "RESTART QUIZ");
        resultReviewButton = FindButtonByChildText(quizResultPage, "PLAY QUIZ") ?? FindButtonByChildText(quizResultPage, "REVIEW ANSWERS");

        resultBreakdownScrollRect = FindComponentInChildrenByName<ScrollRect>(quizResultPage, "BreakdownScrollView")
            ?? (quizResultPage != null ? quizResultPage.GetComponentInChildren<ScrollRect>(true) : null);
        resultBreakdownContent = resultBreakdownScrollRect != null ? resultBreakdownScrollRect.content : null;
        resultBreakdownVerticalScrollbar = resultBreakdownScrollRect != null
            ? FindComponentInChildrenByName<Scrollbar>(resultBreakdownScrollRect.gameObject, "Scrollbar Vertical")
            : null;
        resultBreakdownTemplateCard = resultBreakdownContent != null
            ? FindComponentInChildrenByName<RectTransform>(resultBreakdownContent.gameObject, "QuestionCard")
            : null;

        if (resultBreakdownTemplateCard == null)
        {
            resultBreakdownTemplateCard = FindRectTransform(quizResultPage, "QuestionCard", "Question")
                ?? FindRectTransform(quizResultPage, "QuestionCard", "QuestionText")
                ?? FindRectTransform(quizResultPage, "Content", "Question")
                ?? FindRectTransform(quizResultPage, "Content", "QuestionText");
        }

        breakdownTitleText = FindText(quizBreakdownPage, "TitleText");
        breakdownProgressText = FindText(quizBreakdownPage, "CorrectText");
        breakdownScoreValueText = FindText(quizBreakdownPage, "ScoreValueText");
        breakdownResultValueText = breakdownProgressText;
        breakdownBackButton = FindButton(quizBreakdownPage, "BackButton");
        breakdownPreviousButton = FindButton(quizBreakdownPage, "PreviousButton");
        breakdownNextButton = FindButton(quizBreakdownPage, "NextButton");
        breakdownScrollRect = FindComponentInChildrenByName<ScrollRect>(quizBreakdownPage, "BreakdownScrollView")
            ?? (quizBreakdownPage != null ? quizBreakdownPage.GetComponentInChildren<ScrollRect>(true) : null);
        breakdownContent = breakdownScrollRect != null ? breakdownScrollRect.content : null;
        breakdownVerticalScrollbar = breakdownScrollRect != null
            ? FindComponentInChildrenByName<Scrollbar>(breakdownScrollRect.gameObject, "Scrollbar Vertical")
            : null;
        breakdownTemplateCard = breakdownContent != null
            ? FindComponentInChildrenByName<RectTransform>(breakdownContent.gameObject, "QuestionCard")
            : null;

        if (breakdownTemplateCard == null)
        {
            breakdownTemplateCard = FindRectTransform(quizBreakdownPage, "QuestionCard", "Question")
                ?? FindRectTransform(quizBreakdownPage, "QuestionCard", "QuestionText")
                ?? FindRectTransform(quizBreakdownPage, "Content", "Question")
                ?? FindRectTransform(quizBreakdownPage, "Content", "QuestionText");
        }
    }

    public bool OpenHistoryAttempt(QuizHistoryEntry entry)
    {
        if (entry == null || entry.questions == null || entry.questions.Count == 0)
        {
            Debug.LogWarning("[QuizFlowController] History attempt did not contain detailed question data.");
            return false;
        }

        currentSession = new QuizSessionData
        {
            sessionId = entry.sessionId,
            topic = entry.topic,
            difficulty = entry.difficulty,
            score = entry.score,
            currentQuestionIndex = 0,
            isComplete = true,
            createdAtUtc = entry.createdAtUtc,
            completedAtUtc = entry.completedAtUtc,
            selectedAnswers = CloneStringList(entry.selectedAnswers),
            questions = CloneQuestions(entry.questions)
        };

        EnsureSelectedAnswerSlots(currentSession);
        isReviewingHistoryAttempt = true;
        SetQuizPageState(quizBreakdownPage);
        RenderBreakdownPage();
        return true;
    }

    private void HandleBreakdownBack()
    {
        if (isReviewingHistoryAttempt)
        {
            ShowHistoryPage();
            return;
        }

        ShowResultPage();
    }

    private void RenderResultBreakdownCards()
    {
        if (resultBreakdownTemplateCard == null || resultBreakdownContent == null || currentSession == null || currentSession.questions == null)
        {
            return;
        }

        EnsureResultBreakdownContentLayout();
        ClearGeneratedResultBreakdownCards();

        resultBreakdownTemplateCard.gameObject.SetActive(false);

        for (int i = 0; i < currentSession.questions.Count; i++)
        {
            RectTransform card = Instantiate(resultBreakdownTemplateCard, resultBreakdownContent);
            card.name = "ResultBreakdownCard_" + i;
            PrepareResultBreakdownCardForReuse(card);
            generatedResultBreakdownCards.Add(card.gameObject);

            BindBreakdownCard(card.gameObject, currentSession.questions[i], GetSelectedAnswer(i), i);
        }

        ResizeResultBreakdownContent(currentSession.questions.Count);
        LayoutRebuilder.ForceRebuildLayoutImmediate(resultBreakdownContent);
        Canvas.ForceUpdateCanvases();

        if (resultBreakdownScrollRect != null)
        {
            resultBreakdownScrollRect.normalizedPosition = new Vector2(0f, 1f);
        }
    }

    private void EnsureResultBreakdownContentLayout()
    {
        if (resultBreakdownContent == null)
        {
            return;
        }

        if (resultBreakdownScrollRect != null)
        {
            resultBreakdownScrollRect.horizontal = false;
            resultBreakdownScrollRect.vertical = true;
            resultBreakdownScrollRect.movementType = ScrollRect.MovementType.Clamped;

            if (resultBreakdownVerticalScrollbar != null)
            {
                resultBreakdownVerticalScrollbar.gameObject.SetActive(true);
                resultBreakdownScrollRect.verticalScrollbar = resultBreakdownVerticalScrollbar;
                resultBreakdownScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            }
        }

        VerticalLayoutGroup layout = resultBreakdownContent.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = Mathf.Max(layout.spacing, 20f);
        }

        ContentSizeFitter fitter = resultBreakdownContent.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = resultBreakdownContent.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void PrepareResultBreakdownCardForReuse(RectTransform card)
    {
        if (card == null)
        {
            return;
        }

        card.SetParent(resultBreakdownContent, false);
        card.gameObject.SetActive(true);
        card.anchorMin = new Vector2(0f, 1f);
        card.anchorMax = new Vector2(1f, 1f);
        card.pivot = new Vector2(0.5f, 1f);
        card.localScale = Vector3.one;
        card.anchoredPosition = Vector2.zero;

        LayoutElement layoutElement = card.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = card.gameObject.AddComponent<LayoutElement>();
        }

        float preferredHeight = card.sizeDelta.y > 0f ? card.sizeDelta.y : card.rect.height;
        if (preferredHeight <= 0f)
        {
            preferredHeight = 760f;
        }

        layoutElement.ignoreLayout = false;
        layoutElement.preferredWidth = -1f;
        layoutElement.minWidth = -1f;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.minHeight = preferredHeight;
        layoutElement.flexibleHeight = 0f;

        ContentSizeFitter fitter = card.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.enabled = false;
        }
    }

    private void ClearGeneratedResultBreakdownCards()
    {
        for (int i = 0; i < generatedResultBreakdownCards.Count; i++)
        {
            if (generatedResultBreakdownCards[i] != null)
            {
                Destroy(generatedResultBreakdownCards[i]);
            }
        }

        generatedResultBreakdownCards.Clear();
    }

    private void ResizeResultBreakdownContent(int cardCount)
    {
        if (resultBreakdownContent == null || resultBreakdownTemplateCard == null)
        {
            return;
        }

        EnsureResultBreakdownContentLayout();

        float spacing = GetResultBreakdownCardSpacing();
        float cardHeight = resultBreakdownTemplateCard.sizeDelta.y > 0f ? resultBreakdownTemplateCard.sizeDelta.y : resultBreakdownTemplateCard.rect.height;
        if (cardHeight <= 0f)
        {
            cardHeight = 760f;
        }

        VerticalLayoutGroup layout = resultBreakdownContent.GetComponent<VerticalLayoutGroup>();
        float paddingTop = layout != null ? layout.padding.top : 0f;
        float paddingBottom = layout != null ? layout.padding.bottom : 0f;
        float totalHeight = paddingTop + paddingBottom + (cardHeight * Mathf.Max(1, cardCount)) + (spacing * Mathf.Max(0, cardCount - 1));

        Vector2 sizeDelta = resultBreakdownContent.sizeDelta;
        sizeDelta.y = totalHeight;
        resultBreakdownContent.sizeDelta = sizeDelta;
    }

    private float GetResultBreakdownCardSpacing()
    {
        VerticalLayoutGroup layout = resultBreakdownContent != null ? resultBreakdownContent.GetComponent<VerticalLayoutGroup>() : null;
        return layout != null ? layout.spacing : 20f;
    }

    private void RenderBreakdownCards()
    {
        if (breakdownTemplateCard == null || breakdownContent == null || currentSession == null || currentSession.questions == null)
        {
            return;
        }

        EnsureBreakdownContentLayout();
        ClearGeneratedBreakdownCards();

        breakdownTemplateCard.gameObject.SetActive(false);

        for (int i = 0; i < currentSession.questions.Count; i++)
        {
            RectTransform card = Instantiate(breakdownTemplateCard, breakdownContent);
            card.name = "BreakdownCard_" + i;
            PrepareBreakdownCardForReuse(card);
            generatedBreakdownCards.Add(card.gameObject);

            BindBreakdownCard(card.gameObject, currentSession.questions[i], GetSelectedAnswer(i), i);
        }

        ResizeBreakdownContent(currentSession.questions.Count);
        LayoutRebuilder.ForceRebuildLayoutImmediate(breakdownContent);
        Canvas.ForceUpdateCanvases();

        if (breakdownScrollRect != null)
        {
            breakdownScrollRect.normalizedPosition = new Vector2(0f, 1f);
        }
    }

    private void EnsureBreakdownContentLayout()
    {
        if (breakdownContent == null)
        {
            return;
        }

        if (breakdownScrollRect != null)
        {
            breakdownScrollRect.horizontal = false;
            breakdownScrollRect.vertical = true;
            breakdownScrollRect.movementType = ScrollRect.MovementType.Clamped;

            if (breakdownVerticalScrollbar != null)
            {
                breakdownVerticalScrollbar.gameObject.SetActive(true);
                breakdownScrollRect.verticalScrollbar = breakdownVerticalScrollbar;
                breakdownScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            }
        }

        VerticalLayoutGroup layout = breakdownContent.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = Mathf.Max(layout.spacing, 20f);
        }

        ContentSizeFitter fitter = breakdownContent.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = breakdownContent.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void PrepareBreakdownCardForReuse(RectTransform card)
    {
        if (card == null)
        {
            return;
        }

        card.SetParent(breakdownContent, false);
        card.gameObject.SetActive(true);
        card.anchorMin = new Vector2(0f, 1f);
        card.anchorMax = new Vector2(1f, 1f);
        card.pivot = new Vector2(0.5f, 1f);
        card.localScale = Vector3.one;
        card.anchoredPosition = Vector2.zero;

        LayoutElement layoutElement = card.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = card.gameObject.AddComponent<LayoutElement>();
        }

        float preferredHeight = card.sizeDelta.y > 0f ? card.sizeDelta.y : card.rect.height;
        if (preferredHeight <= 0f)
        {
            preferredHeight = 760f;
        }

        layoutElement.ignoreLayout = false;
        layoutElement.preferredWidth = -1f;
        layoutElement.minWidth = -1f;
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.minHeight = preferredHeight;
        layoutElement.flexibleHeight = 0f;

        ContentSizeFitter fitter = card.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.enabled = false;
        }
    }

    private void BindBreakdownCard(GameObject card, QuizQuestionData questionData, string selectedAnswer, int questionIndex)
    {
        if (card == null || questionData == null)
        {
            return;
        }

        TextMeshProUGUI questionLabel = FindText(card, "Question") ?? FindText(card, "QuestionText");
        TextMeshProUGUI yourAnswerValueLabel = FindText(card, "YourAnswerText");
        TextMeshProUGUI yourAnswerStaticLabel = FindText(card, "YourAnswerLabel");
        TextMeshProUGUI correctAnswerValueLabel = FindText(card, "CorrectAnswerText");
        TextMeshProUGUI correctAnswerStaticLabel = FindText(card, "CorrectAnswerLabel");
        TextMeshProUGUI explanationValueLabel = FindText(card, "ExplanationText");
        TextMeshProUGUI explanationStaticLabel = FindText(card, "ExplanationLabel");
        TextMeshProUGUI stateLabel = FindText(card, "CorrectOrWrongText");
        Image stateIcon = FindImage(card, "CorrectOrWrongIcon");

        if (questionLabel != null)
        {
            ConfigureBreakdownText(questionLabel, true, 24f, 40f);
            questionLabel.text = BuildBreakdownQuestionText(questionIndex + 1, questionData.question, currentSession.completedAtUtc);
        }

        if (yourAnswerStaticLabel != null)
        {
            yourAnswerStaticLabel.text = "Your Answer:";
        }

        if (yourAnswerValueLabel != null)
        {
            ConfigureBreakdownText(yourAnswerValueLabel, false, 20f, 30f);
            yourAnswerValueLabel.text = string.IsNullOrWhiteSpace(selectedAnswer) ? "No answer" : selectedAnswer;
        }

        if (correctAnswerStaticLabel != null)
        {
            correctAnswerStaticLabel.text = "Correct Answer:";
        }

        if (correctAnswerValueLabel != null)
        {
            ConfigureBreakdownText(correctAnswerValueLabel, false, 20f, 30f);
            correctAnswerValueLabel.text = questionData.correctAnswer;
        }

        if (explanationStaticLabel != null)
        {
            explanationStaticLabel.text = "Explanation:";
        }

        if (explanationValueLabel != null)
        {
            ConfigureBreakdownText(explanationValueLabel, false, 18f, 28f);
            explanationValueLabel.text = questionData.explanation;
        }

        bool isCorrect = !string.IsNullOrWhiteSpace(selectedAnswer) &&
            string.Equals(selectedAnswer, questionData.correctAnswer, StringComparison.OrdinalIgnoreCase);

        if (stateLabel != null)
        {
            stateLabel.text = isCorrect ? "Correct!" : "Incorrect";
            stateLabel.color = isCorrect ? new Color(0.4f, 1f, 0.4f, 1f) : new Color(1f, 0.45f, 0.45f, 1f);
        }

        if (stateIcon != null)
        {
            stateIcon.color = isCorrect ? new Color(0.15f, 0.85f, 0.2f, 1f) : new Color(0.9f, 0.2f, 0.2f, 1f);
        }

        for (int i = 0; i < 4; i++)
        {
            GameObject choiceObject = FindObjectByNameWithinPage(card, "Choice" + (char)('A' + i));
            TextMeshProUGUI choiceText = FindText(choiceObject, "ChoiceText");
            Image choiceImage = choiceObject != null ? choiceObject.GetComponent<Image>() : null;

            string choice = questionData.choices != null && i < questionData.choices.Count
                ? questionData.choices[i]
                : string.Empty;

            if (choiceText != null)
            {
                ConfigureBreakdownText(choiceText, false, 18f, 28f);
                choiceText.alignment = TextAlignmentOptions.Center;
                choiceText.text = string.IsNullOrWhiteSpace(choice) ? string.Empty : $"{(char)('A' + i)}. {choice}";
            }

            if (choiceImage != null)
            {
                choiceImage.color = ResolveBreakdownChoiceColor(choice, selectedAnswer, questionData.correctAnswer);
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(card.transform as RectTransform);
    }

    private void ClearGeneratedBreakdownCards()
    {
        for (int i = 0; i < generatedBreakdownCards.Count; i++)
        {
            if (generatedBreakdownCards[i] != null)
            {
                Destroy(generatedBreakdownCards[i]);
            }
        }

        generatedBreakdownCards.Clear();
    }

    private void ResizeBreakdownContent(int cardCount)
    {
        if (breakdownContent == null || breakdownTemplateCard == null)
        {
            return;
        }

        EnsureBreakdownContentLayout();

        float spacing = GetBreakdownCardSpacing();
        float cardHeight = breakdownTemplateCard.sizeDelta.y > 0f ? breakdownTemplateCard.sizeDelta.y : breakdownTemplateCard.rect.height;
        if (cardHeight <= 0f)
        {
            cardHeight = 760f;
        }

        VerticalLayoutGroup layout = breakdownContent.GetComponent<VerticalLayoutGroup>();
        float paddingTop = layout != null ? layout.padding.top : 0f;
        float paddingBottom = layout != null ? layout.padding.bottom : 0f;
        float totalHeight = paddingTop + paddingBottom + (cardHeight * Mathf.Max(1, cardCount)) + (spacing * Mathf.Max(0, cardCount - 1));

        Vector2 sizeDelta = breakdownContent.sizeDelta;
        sizeDelta.y = totalHeight;
        breakdownContent.sizeDelta = sizeDelta;
    }

    private float GetBreakdownCardSpacing()
    {
        VerticalLayoutGroup layout = breakdownContent != null ? breakdownContent.GetComponent<VerticalLayoutGroup>() : null;
        return layout != null ? layout.spacing : 20f;
    }

    private string GetSelectedAnswer(int questionIndex)
    {
        return currentSession != null &&
               currentSession.selectedAnswers != null &&
               questionIndex >= 0 &&
               questionIndex < currentSession.selectedAnswers.Count
            ? currentSession.selectedAnswers[questionIndex]
            : string.Empty;
    }

    private static Color ResolveBreakdownChoiceColor(string choice, string selectedAnswer, string correctAnswer)
    {
        bool isSelected = !string.IsNullOrWhiteSpace(choice) && string.Equals(choice, selectedAnswer, StringComparison.OrdinalIgnoreCase);
        bool isCorrect = !string.IsNullOrWhiteSpace(choice) && string.Equals(choice, correctAnswer, StringComparison.OrdinalIgnoreCase);

        if (isSelected && isCorrect)
        {
            return new Color(0.45f, 0.85f, 0.45f, 1f);
        }

        if (isCorrect)
        {
            return new Color(0.15f, 0.55f, 0.2f, 1f);
        }

        if (isSelected)
        {
            return new Color(0.65f, 0.2f, 0.2f, 1f);
        }

        return new Color(0.45f, 0.45f, 0.45f, 1f);
    }

    private static string BuildBreakdownQuestionText(int questionNumber, string question, string completedAtUtc)
    {
        string dateLabel = FormatHistoryTimestamp(completedAtUtc);
        return string.IsNullOrWhiteSpace(dateLabel)
            ? $"Question {questionNumber}: {question}"
            : $"Question {questionNumber}: {question}\n<size=60%>{dateLabel}</size>";
    }

    private static string FormatHistoryTimestamp(string timestamp)
    {
        if (string.IsNullOrWhiteSpace(timestamp))
        {
            return string.Empty;
        }

        if (!DateTime.TryParse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
        {
            return string.Empty;
        }

        return "Taken on " + parsed.ToLocalTime().ToString("MMMM dd, yyyy h:mm tt");
    }

    private static void ConfigureBreakdownText(TextMeshProUGUI textComponent, bool emphasize, float minFontSize, float maxFontSize)
    {
        if (textComponent == null)
        {
            return;
        }

        textComponent.enableAutoSizing = true;
        textComponent.fontSizeMin = minFontSize;
        textComponent.fontSizeMax = maxFontSize;
        textComponent.textWrappingMode = TextWrappingModes.Normal;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
        textComponent.alignment = emphasize ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopLeft;
    }

    private static List<string> CloneStringList(List<string> values)
    {
        return values != null ? new List<string>(values) : new List<string>();
    }

    private static List<QuizQuestionData> CloneQuestions(List<QuizQuestionData> questions)
    {
        List<QuizQuestionData> clones = new List<QuizQuestionData>();
        if (questions == null)
        {
            return clones;
        }

        foreach (QuizQuestionData question in questions)
        {
            if (question == null)
            {
                continue;
            }

            clones.Add(new QuizQuestionData
            {
                question = question.question,
                choices = question.choices != null ? new List<string>(question.choices) : new List<string>(),
                correctAnswer = question.correctAnswer,
                explanation = question.explanation
            });
        }

        return clones;
    }

    private void HighlightSubmittedAnswer(int selectedIndex, string correctAnswer)
    {
        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (answerButtons[i] == null)
            {
                continue;
            }

            Image graphic = answerButtons[i].targetGraphic as Image ?? answerButtons[i].GetComponent<Image>();
            if (graphic == null)
            {
                continue;
            }

            string choiceText = answerTexts[i] != null ? answerTexts[i].text : string.Empty;
            bool isCorrect = string.Equals(choiceText, correctAnswer, StringComparison.OrdinalIgnoreCase);
            bool isSelected = i == selectedIndex;

            if (isCorrect)
            {
                graphic.color = new Color(0.15f, 0.45f, 0.2f, 1f);
            }
            else if (isSelected)
            {
                graphic.color = new Color(0.5f, 0.15f, 0.15f, 1f);
            }
            else
            {
                graphic.color = new Color(0.082f, 0.082f, 0.238f, 1f);
            }
        }
    }

    private void ResetAnswerButtonVisuals()
    {
        foreach (Button button in answerButtons)
        {
            if (button == null)
            {
                continue;
            }

            Image graphic = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (graphic != null)
            {
                graphic.color = new Color(0.082f, 0.082f, 0.238f, 1f);
            }
        }
    }

    private static void EnsureSelectedAnswerSlots(QuizSessionData session)
    {
        if (session == null)
        {
            return;
        }

        if (session.selectedAnswers == null)
        {
            session.selectedAnswers = new List<string>();
        }

        int requiredCount = session.questions != null ? session.questions.Count : 0;
        while (session.selectedAnswers.Count < requiredCount)
        {
            session.selectedAnswers.Add(string.Empty);
        }
    }

    private static QuizSessionData ParseQuizSession(string responseJson, string topic, string difficulty)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return null;
        }

        QuizSessionData parsedSession = JsonUtility.FromJson<QuizSessionData>(responseJson);
        if (parsedSession == null || parsedSession.questions == null || parsedSession.questions.Count == 0)
        {
            return null;
        }

        parsedSession.sessionId = string.IsNullOrWhiteSpace(parsedSession.sessionId) ? Guid.NewGuid().ToString("N") : parsedSession.sessionId;
        parsedSession.topic = string.IsNullOrWhiteSpace(parsedSession.topic) ? topic : parsedSession.topic;
        parsedSession.difficulty = string.IsNullOrWhiteSpace(parsedSession.difficulty) ? difficulty : parsedSession.difficulty;
        parsedSession.score = 0;
        parsedSession.currentQuestionIndex = 0;
        parsedSession.isComplete = false;
        parsedSession.createdAtUtc = DateTime.UtcNow.ToString("o");
        EnsureSelectedAnswerSlots(parsedSession);
        return parsedSession;
    }

    private static string NormalizeOllamaQuizJson(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return null;
        }

        string trimmed = rawResponse.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed.Substring(firstNewline + 1).Trim();
            }

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 3).Trim();
            }
        }

        int objectStart = trimmed.IndexOf('{');
        int objectEnd = trimmed.LastIndexOf('}');
        if (objectStart >= 0 && objectEnd > objectStart)
        {
            trimmed = trimmed.Substring(objectStart, objectEnd - objectStart + 1);
        }

        return trimmed;
    }

    private static bool ValidateSession(QuizSessionData session, int minimumQuestionCount = DefaultQuestionCount, int maximumQuestionCount = DefaultQuestionCount)
    {
        if (session == null || session.questions == null)
        {
            return false;
        }

        int questionCount = session.questions.Count;
        if (questionCount < minimumQuestionCount || questionCount > maximumQuestionCount)
        {
            return false;
        }

        HashSet<string> seenQuestions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (QuizQuestionData question in session.questions)
        {
            if (question == null || string.IsNullOrWhiteSpace(question.question) || question.choices == null || question.choices.Count != 4)
            {
                return false;
            }

            if (!seenQuestions.Add(question.question.Trim()))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(question.correctAnswer) || !question.choices.Any(choice => string.Equals(choice, question.correctAnswer, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return true;
    }

    private static QuizSessionData BuildFallbackQuiz(string topic, string difficulty, HashSet<string> excludedQuestionKeys = null)
    {
        string normalizedTopic = string.IsNullOrWhiteSpace(topic) ? "Solar System" : topic.Trim();
        string normalizedDifficulty = string.IsNullOrWhiteSpace(difficulty) ? "Easy" : difficulty.Trim();

        List<QuizQuestionData> fullQuestionPool = BuildFallbackQuestionPool(normalizedTopic, normalizedDifficulty);
        List<QuizQuestionData> preferredQuestionPool = fullQuestionPool;
        if (excludedQuestionKeys != null && excludedQuestionKeys.Count > 0)
        {
            preferredQuestionPool = fullQuestionPool
                .Where(question => question != null && !excludedQuestionKeys.Contains(NormalizeQuestionKey(question.question)))
                .ToList();
        }

        Shuffle(preferredQuestionPool);
        List<QuizQuestionData> questions = preferredQuestionPool
            .Take(DefaultQuestionCount)
            .Select(CloneQuestion)
            .ToList();

        if (questions.Count < DefaultQuestionCount)
        {
            HashSet<string> usedKeys = new HashSet<string>(
                questions.Where(question => question != null).Select(question => NormalizeQuestionKey(question.question)),
                StringComparer.Ordinal);

            List<QuizQuestionData> topUpPool = fullQuestionPool
                .Where(question => question != null && !usedKeys.Contains(NormalizeQuestionKey(question.question)))
                .Select(CloneQuestion)
                .ToList();

            Shuffle(topUpPool);
            while (questions.Count < DefaultQuestionCount && topUpPool.Count > 0)
            {
                QuizQuestionData nextQuestion = topUpPool[0];
                topUpPool.RemoveAt(0);
                questions.Add(nextQuestion);
            }
        }

        QuizSessionData session = new QuizSessionData
        {
            sessionId = Guid.NewGuid().ToString("N"),
            topic = normalizedTopic,
            difficulty = normalizedDifficulty,
            score = 0,
            currentQuestionIndex = 0,
            isComplete = false,
            createdAtUtc = DateTime.UtcNow.ToString("o"),
            questions = questions,
            selectedAnswers = Enumerable.Repeat(string.Empty, questions.Count).ToList()
        };

        return session;
    }

    private static QuizSessionData RemoveDuplicateQuestions(QuizSessionData session, HashSet<string> excludedQuestionKeys)
    {
        if (session == null || session.questions == null)
        {
            return session;
        }

        HashSet<string> usedKeys = excludedQuestionKeys != null
            ? new HashSet<string>(excludedQuestionKeys, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        List<QuizQuestionData> filteredQuestions = new List<QuizQuestionData>();
        foreach (QuizQuestionData question in session.questions)
        {
            if (question == null || string.IsNullOrWhiteSpace(question.question))
            {
                continue;
            }

            string key = NormalizeQuestionKey(question.question);
            if (string.IsNullOrWhiteSpace(key) || usedKeys.Contains(key))
            {
                continue;
            }

            usedKeys.Add(key);
            filteredQuestions.Add(CloneQuestion(question));
        }

        session.questions = filteredQuestions;
        session.selectedAnswers = Enumerable.Repeat(string.Empty, filteredQuestions.Count).ToList();
        return session;
    }

    private static void FillMissingQuestionsFromFallback(QuizSessionData session, HashSet<string> excludedQuestionKeys)
    {
        if (session == null)
        {
            return;
        }

        if (session.questions == null)
        {
            session.questions = new List<QuizQuestionData>();
        }

        HashSet<string> usedKeys = excludedQuestionKeys != null
            ? new HashSet<string>(excludedQuestionKeys, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);

        foreach (QuizQuestionData question in session.questions)
        {
            if (question == null)
            {
                continue;
            }

            usedKeys.Add(NormalizeQuestionKey(question.question));
        }

        List<QuizQuestionData> fallbackPool = BuildFallbackQuestionPool(session.topic, session.difficulty)
            .Where(question => question != null && !usedKeys.Contains(NormalizeQuestionKey(question.question)))
            .Select(CloneQuestion)
            .ToList();

        Shuffle(fallbackPool);

        int originalQuestionCount = session.questions.Count;

        while (session.questions.Count < DefaultQuestionCount && fallbackPool.Count > 0)
        {
            QuizQuestionData question = fallbackPool[0];
            fallbackPool.RemoveAt(0);
            session.questions.Add(question);
            usedKeys.Add(NormalizeQuestionKey(question.question));
        }

        if (session.questions.Count < DefaultQuestionCount)
        {
            List<QuizQuestionData> reusePool = BuildFallbackQuestionPool(session.topic, session.difficulty)
                .Where(question => question != null && !session.questions.Any(existing => existing != null && string.Equals(NormalizeQuestionKey(existing.question), NormalizeQuestionKey(question.question), StringComparison.Ordinal)))
                .Select(CloneQuestion)
                .ToList();

            Shuffle(reusePool);

            while (session.questions.Count < DefaultQuestionCount && reusePool.Count > 0)
            {
                QuizQuestionData question = reusePool[0];
                reusePool.RemoveAt(0);
                session.questions.Add(question);
                usedKeys.Add(NormalizeQuestionKey(question.question));
            }
        }

        int addedQuestionCount = session.questions.Count - originalQuestionCount;
        if (addedQuestionCount > 0)
        {
            Debug.Log("[QuizFlowController] Added " + addedQuestionCount + " fallback question(s) for " + session.topic + " " + session.difficulty + " quiz.");
        }

        if (session.questions.Count < DefaultQuestionCount)
        {
            Debug.LogWarning("[QuizFlowController] Quiz still has only " + session.questions.Count + " question(s) after fallback top-up for " + session.topic + " " + session.difficulty + ".");
        }

        session.selectedAnswers = Enumerable.Repeat(string.Empty, session.questions.Count).ToList();
    }

    private static HashSet<string> BuildRecentQuestionKeySet(string topic, string difficulty)
    {
        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        QuizHistoryCollection history = GuestQuizStorage.LoadHistory();
        if (history == null || history.entries == null)
        {
            return keys;
        }

        foreach (QuizHistoryEntry entry in history.entries)
        {
            if (entry == null ||
                !string.Equals(entry.topic, topic, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(entry.difficulty, difficulty, StringComparison.OrdinalIgnoreCase) ||
                entry.questions == null)
            {
                continue;
            }

            foreach (QuizQuestionData question in entry.questions)
            {
                if (question == null || string.IsNullOrWhiteSpace(question.question))
                {
                    continue;
                }

                keys.Add(NormalizeQuestionKey(question.question));
            }
        }

        return keys;
    }

    private static List<QuizQuestionData> BuildFallbackQuestionPool(string topic, string difficulty)
    {
        bool isEasy = string.Equals(difficulty, "Easy", StringComparison.OrdinalIgnoreCase);
        return isEasy ? BuildEasyFallbackQuestionPool(topic) : BuildHardFallbackQuestionPool(topic);
    }

    private static string BuildLocalQuizPrompt(string topic, string difficulty)
    {
        string resolvedTopic = ResolveTopicDisplayName(topic);
        string resolvedDifficulty = string.Equals(difficulty, "Hard", StringComparison.OrdinalIgnoreCase) ? "Hard" : "Easy";
        TopicFactProfile profile = GetTopicFactProfile(resolvedTopic);

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("You are generating a multiple-choice astronomy quiz for the AstroLearn Unity app.");
        builder.AppendLine("Return valid JSON only. Do not use markdown fences, commentary, or extra text.");
        builder.AppendLine("The JSON must match this exact schema:");
        builder.AppendLine("{");
        builder.AppendLine("  \"topic\": \"string\",");
        builder.AppendLine("  \"difficulty\": \"string\",");
        builder.AppendLine("  \"questions\": [");
        builder.AppendLine("    {");
        builder.AppendLine("      \"question\": \"string\",");
        builder.AppendLine("      \"choices\": [\"string\", \"string\", \"string\", \"string\"],");
        builder.AppendLine("      \"correctAnswer\": \"string\",");
        builder.AppendLine("      \"explanation\": \"string\"");
        builder.AppendLine("    }");
        builder.AppendLine("  ]");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine($"Generate exactly {LocalGeneratedQuestionCount} unique questions.");
        builder.AppendLine($"Topic: {resolvedTopic}");
        builder.AppendLine($"Difficulty: {resolvedDifficulty}");
        builder.AppendLine("All questions must stay within astronomy and the Solar System.");
        builder.AppendLine($"Every question must directly test knowledge about {resolvedTopic}.");
        builder.AppendLine("Each question must have exactly 4 choices.");
        builder.AppendLine("The correctAnswer must exactly match one of the 4 choices.");
        builder.AppendLine("Avoid duplicate questions.");
        builder.AppendLine("Keep each explanation to one short sentence.");
        builder.AppendLine("Make the questions suitable for Grade 7 to Grade 12 learners.");
        builder.AppendLine("Do not ask about quizzes, learners, study habits, question difficulty, or how to answer tests.");
        builder.AppendLine("Do not write filler prompts such as 'Which object is this quiz about?' or 'What does a hard quiz test?'.");
        builder.AppendLine("Use concrete astronomy facts, recognizable features, and scientifically accurate comparisons.");
        if (string.Equals(resolvedDifficulty, "Easy", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("Easy questions should focus on direct facts, recognition, and basic understanding.");
        }
        else
        {
            builder.AppendLine("Hard questions should focus on deeper reasoning, comparison, and inference while still being answerable by students.");
        }

        if (profile != null)
        {
            builder.AppendLine("Use these verified AstroLearn facts as source material:");
            foreach (string fact in BuildPromptFacts(profile))
            {
                builder.AppendLine("- " + fact);
            }
        }

        return builder.ToString();
    }

    private static List<QuizQuestionData> BuildEasyFallbackQuestionPool(string topic)
    {
        TopicFactProfile profile = GetTopicFactProfile(topic);
        if (profile == null)
        {
            Debug.LogWarning("[QuizFlowController] No topic fact profile found for '" + topic + "'. Using legacy easy fallback questions.");
            return BuildLegacyEasyFallbackQuestionPool(topic);
        }

        List<QuizQuestionData> questions = new List<QuizQuestionData>();
        questions.AddRange(BuildSharedEasyQuestions(profile));
        questions.AddRange(BuildProfileSpecificEasyQuestions(profile));
        return DeduplicateQuestions(questions).Take(DefaultQuestionCount).ToList();
    }

    private static List<QuizQuestionData> BuildHardFallbackQuestionPool(string topic)
    {
        TopicFactProfile profile = GetTopicFactProfile(topic);
        if (profile == null)
        {
            Debug.LogWarning("[QuizFlowController] No topic fact profile found for '" + topic + "'. Using legacy hard fallback questions.");
            return BuildLegacyHardFallbackQuestionPool(topic);
        }

        List<QuizQuestionData> questions = new List<QuizQuestionData>();
        questions.AddRange(BuildSharedHardQuestions(profile));
        questions.AddRange(BuildProfileSpecificHardQuestions(profile));
        return DeduplicateQuestions(questions).Take(DefaultQuestionCount).ToList();
    }

    private static List<QuizQuestionData> BuildLegacyEasyFallbackQuestionPool(string topic)
    {
        string answer = ResolveTopicDisplayName(topic);

        return new List<QuizQuestionData>
        {
            CreateQuestion($"Which object is this quiz mainly about?", answer, "A distant star cluster", "A type of galaxy", "A spacecraft mission", $"{answer} is the selected topic for this quiz."),
            CreateQuestion($"Which science field mainly studies {answer}?", "Astronomy", "Botany", "Civil engineering", "Oceanography", $"Astronomy studies planets, stars, and other space objects including {answer}."),
            CreateQuestion($"Which tool is commonly used to observe {answer} from Earth?", "Telescope", "Thermometer", "Compass", "Seismograph", "Telescopes help astronomers observe distant space objects."),
            CreateQuestion($"Why do quizzes about {answer} help learners?", "They check understanding", "They change planetary orbits", "They create new moons", "They replace rockets", "Quizzes help learners review and measure what they know."),
            CreateQuestion($"What is at the center of our Solar System?", "The Sun", "Earth", "Jupiter", "The Moon", "The Sun is the star at the center of the Solar System."),
            CreateQuestion($"Which planet is known as the Red Planet?", "Mars", "Venus", "Mercury", "Neptune", "Mars appears reddish because of iron oxide on its surface."),
            CreateQuestion($"What is a large body that orbits the Sun and is not a star called?", "Planet", "Galaxy", "Constellation", "Astrolabe", "A planet is a large object that orbits the Sun."),
            CreateQuestion($"What usually makes a comet's tail visible?", "Solar heat and solar wind", "Sound waves", "Ocean currents", "Volcanic ash", "Comet tails form when sunlight warms the comet and releases gas and dust."),
            CreateQuestion($"Which statement best describes {answer} as a Solar System topic?", answer, "A type of weather pattern", "A mountain range", "A river system", $"{answer} is one of the objects studied in Solar System science."),
            CreateQuestion($"What does an easy quiz usually focus on?", "Basic facts and direct recall", "Advanced orbital calculations only", "Writing code for rockets", "Building telescopes from scratch", "Easy quizzes usually focus on foundational understanding."),
            CreateQuestion($"Which skill is most useful in an easy {answer} quiz?", "Remembering key facts", "Solving calculus proofs", "Programming satellites", "Designing engines", "Easy questions usually ask learners to recall important facts."),
            CreateQuestion($"Why is it helpful to begin with easy questions about {answer}?", "They build confidence and foundation", "They remove the need to study", "They change the topic", "They prevent all mistakes", "Easy questions help learners warm up before harder challenges."),
            CreateQuestion($"Which phrase best matches an easy question style?", "What is it?", "How does perturbation affect resonance over time?", "Compare competing formation theories in detail.", "Derive the orbital equation.", "Easy questions usually ask short, direct fact-based prompts."),
            CreateQuestion($"Which activity best supports learning basic facts about {answer}?", "Reviewing definitions and visuals", "Ignoring examples", "Memorizing unrelated formulas", "Skipping feedback", "Simple review materials help learners build accurate understanding."),
        };
    }

    private static List<QuizQuestionData> BuildLegacyHardFallbackQuestionPool(string topic)
    {
        string answer = ResolveTopicDisplayName(topic);

        return new List<QuizQuestionData>
        {
            CreateQuestion($"Why might scientists compare {answer} with other Solar System objects?", "To analyze patterns and differences", "To remove gravity", "To stop planetary motion", "To rename the Solar System", "Comparison helps scientists understand how objects are similar and different."),
            CreateQuestion($"Which skill best fits a hard quiz about {answer}?", "Applying facts to reasoning questions", "Guessing without evidence", "Ignoring explanations", "Choosing answers randomly", "Hard quizzes usually ask learners to apply knowledge, not just recall it."),
            CreateQuestion($"Why is evidence important when answering difficult questions about {answer}?", "It supports the best explanation", "It makes gravity weaker", "It changes the correct answer", "It removes the need to compare choices", "Hard questions often require choosing the answer that is best supported by scientific evidence."),
            CreateQuestion($"Which type of question is more likely in a hard {answer} quiz?", "A question requiring comparison or inference", "A single-word color question only", "A question with no context", "A question unrelated to space", "Hard quizzes usually include comparison, reasoning, or inference."),
            CreateQuestion($"If two choices about {answer} seem possible, what is the best strategy?", "Use scientific clues to eliminate weaker options", "Always choose the longest answer", "Pick the first option immediately", "Skip the question without reading", "Carefully comparing evidence helps identify the strongest answer."),
            CreateQuestion($"Why do explanations matter after a difficult question about {answer}?", "They clarify the reasoning behind the answer", "They erase the question", "They change your score automatically", "They remove all future mistakes", "Explanations help learners understand why one option is stronger than the others."),
            CreateQuestion($"Which object is central to the Solar System and strongly influences all planetary orbits?", "The Sun", "Mars", "Europa", "A comet tail", "The Sun's gravity is the dominant force shaping planetary orbits."),
            CreateQuestion($"Which planet's red appearance is best explained by iron oxide on its surface?", "Mars", "Venus", "Mercury", "Neptune", "Mars looks red because iron oxide covers much of its surface."),
            CreateQuestion($"Why would a scientist classify an object orbiting the Sun as a planet rather than a star?", "Because it does not produce its own starlight", "Because it is always blue", "Because it makes sound in space", "Because it has no gravity", "Planets reflect light, while stars generate their own light through fusion."),
            CreateQuestion($"What best explains why a comet develops a tail near the Sun?", "Heating releases gas and dust that solar radiation pushes outward", "The comet enters Earth's atmosphere", "Its surface turns into metal", "Its gravity disappears", "As comets warm, released material is pushed away and forms a visible tail."),
            CreateQuestion($"What makes a hard quiz different from an easy quiz in AstroLearn?", "It expects deeper reasoning from the same topic", "It uses no correct answers", "It avoids scientific vocabulary entirely", "It removes explanations", "Hard mode raises the level of thinking required from the learner."),
            CreateQuestion($"Why is context important in a difficult multiple-choice question about {answer}?", "It helps distinguish the best-supported answer", "It makes every answer correct", "It prevents reading the question", "It removes all distractors", "Context gives the clues needed to separate strong and weak answer choices."),
            CreateQuestion($"Which approach best improves performance on hard quizzes about {answer}?", "Review concepts, then practice applying them", "Memorize one answer pattern", "Ignore why answers are correct", "Use random guessing each time", "Difficult quizzes reward understanding and application."),
            CreateQuestion($"Why are distractor choices used in hard quizzes about {answer}?", "To test whether learners can evaluate similar-looking options", "To hide the topic completely", "To prevent scoring", "To replace explanations", "Good distractors make learners think carefully about why one answer is strongest."),
        };
    }

    private static QuizSessionData SanitizeGeneratedSession(QuizSessionData session, string topic, string difficulty)
    {
        if (session == null || session.questions == null)
        {
            return session;
        }

        TopicFactProfile profile = GetTopicFactProfile(topic);
        List<QuizQuestionData> filteredQuestions = new List<QuizQuestionData>();
        HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < session.questions.Count; i++)
        {
            QuizQuestionData sanitizedQuestion = SanitizeQuestion(session.questions[i]);
            if (sanitizedQuestion == null)
            {
                continue;
            }

            if (IsMetaQuizQuestion(sanitizedQuestion))
            {
                continue;
            }

            if (profile != null && !IsQuestionSpecificToTopic(sanitizedQuestion, profile))
            {
                continue;
            }

            string key = NormalizeQuestionKey(sanitizedQuestion.question);
            if (string.IsNullOrWhiteSpace(key) || !seenKeys.Add(key))
            {
                continue;
            }

            filteredQuestions.Add(sanitizedQuestion);
        }

        if (filteredQuestions.Count != session.questions.Count)
        {
            Debug.Log("[QuizFlowController] Removed " + (session.questions.Count - filteredQuestions.Count) + " low-value generated question(s) before using the quiz.");
        }

        session.questions = filteredQuestions;
        session.topic = string.IsNullOrWhiteSpace(session.topic) ? ResolveTopicDisplayName(topic) : session.topic.Trim();
        session.difficulty = string.IsNullOrWhiteSpace(session.difficulty) ? difficulty : session.difficulty.Trim();
        session.selectedAnswers = Enumerable.Repeat(string.Empty, filteredQuestions.Count).ToList();
        return session;
    }

    private static QuizQuestionData SanitizeQuestion(QuizQuestionData question)
    {
        if (question == null)
        {
            return null;
        }

        List<string> choices = new List<string>();
        if (question.choices != null)
        {
            for (int i = 0; i < question.choices.Count; i++)
            {
                string choice = string.IsNullOrWhiteSpace(question.choices[i]) ? string.Empty : question.choices[i].Trim();
                if (!string.IsNullOrWhiteSpace(choice))
                {
                    choices.Add(choice);
                }
            }
        }

        return new QuizQuestionData
        {
            question = string.IsNullOrWhiteSpace(question.question) ? string.Empty : question.question.Trim(),
            choices = choices,
            correctAnswer = string.IsNullOrWhiteSpace(question.correctAnswer) ? string.Empty : question.correctAnswer.Trim(),
            explanation = string.IsNullOrWhiteSpace(question.explanation) ? string.Empty : question.explanation.Trim()
        };
    }

    private static bool IsMetaQuizQuestion(QuizQuestionData question)
    {
        if (question == null)
        {
            return true;
        }

        string normalized = NormalizeQuestionKey(BuildQuestionSearchText(question));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        string[] bannedPhrases =
        {
            "quiz",
            "quizzes",
            "multiple choice",
            "selected topic",
            "science field",
            "commonly used to observe",
            "tool is commonly used",
            "study strategy",
            "easy question",
            "hard question",
            "easy mode",
            "hard mode",
            "foundational understanding",
            "build confidence",
            "review concepts",
            "distractor",
            "learners",
            "students",
            "what does an easy quiz",
            "what makes a hard quiz",
            "which skill",
            "performance on hard quizzes"
        };

        for (int i = 0; i < bannedPhrases.Length; i++)
        {
            if (normalized.Contains(NormalizeQuestionKey(bannedPhrases[i])))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsQuestionSpecificToTopic(QuizQuestionData question, TopicFactProfile profile)
    {
        if (question == null || profile == null)
        {
            return false;
        }

        string normalized = NormalizeQuestionKey(BuildQuestionSearchText(question));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        for (int i = 0; i < profile.Keywords.Length; i++)
        {
            string keyword = NormalizeQuestionKey(profile.Keywords[i]);
            if (!string.IsNullOrWhiteSpace(keyword) && normalized.Contains(keyword))
            {
                return true;
            }
        }

        return normalized.Contains(NormalizeQuestionKey(profile.DisplayName));
    }

    private static string BuildQuestionSearchText(QuizQuestionData question)
    {
        if (question == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        builder.Append(question.question);
        builder.Append(' ');
        builder.Append(question.correctAnswer);
        builder.Append(' ');
        builder.Append(question.explanation);

        if (question.choices != null)
        {
            for (int i = 0; i < question.choices.Count; i++)
            {
                builder.Append(' ');
                builder.Append(question.choices[i]);
            }
        }

        return builder.ToString();
    }

    private static IEnumerable<string> BuildPromptFacts(TopicFactProfile profile)
    {
        if (profile == null)
        {
            yield break;
        }

        yield return $"{profile.DisplayName} is a {profile.TypeLabel.ToLowerInvariant()}.";
        yield return $"{profile.DisplayName} is {profile.RelationFact.ToLowerInvariant()}.";
        yield return $"{profile.DisplayName} is known for {profile.FeatureFact.ToLowerInvariant()}.";
        yield return $"{profile.DisplayName} has {profile.AtmosphereFact.ToLowerInvariant()}.";
        yield return $"{profile.DisplayName} also has this system or motion clue: {profile.SystemFact}.";
        yield return $"{profile.DisplayName} is notable because it {profile.DistinctionFact.ToLowerInvariant()}.";
    }

    private static List<QuizQuestionData> BuildSharedEasyQuestions(TopicFactProfile profile)
    {
        string[] typeDistractors = GetTypeDistractors(profile.TypeLabel);
        string[] relationDistractors = GetFactDistractors(profile.Key, item => item.RelationFact, "Far beyond Neptune", "Inside a distant galaxy", "Orbiting another star");
        string[] featureDistractors = GetFactDistractors(profile.Key, item => item.FeatureFact, "A surface made entirely of metal", "A permanent rainbow ocean", "A glowing crystal atmosphere");
        string[] atmosphereDistractors = GetFactDistractors(profile.Key, item => item.AtmosphereFact, "A shell of solid diamond", "A surface of burning wood", "A vacuum filled with liquid iron");
        string[] systemDistractors = GetFactDistractors(profile.Key, item => item.SystemFact, "Has continents connected by light bridges", "Never rotates or orbits anything", "Creates its own moons from sunlight");
        string[] distinctionDistractors = GetFactDistractors(profile.Key, item => item.DistinctionFact, "Exists outside the Solar System", "Never rotates on its axis", "Has no physical structure");
        string[] bodyDistractors = GetBodyDistractors(profile.Key);

        return new List<QuizQuestionData>
        {
            CreateQuestion($"What type of object is {profile.DisplayName}?", profile.TypeLabel, typeDistractors[0], typeDistractors[1], typeDistractors[2], $"{profile.DisplayName} is classified as a {profile.TypeLabel.ToLowerInvariant()}."),
            CreateQuestion($"Which statement correctly describes {profile.DisplayName}'s place or relationship in the Solar System?", profile.RelationFact, relationDistractors[0], relationDistractors[1], relationDistractors[2], profile.RelationFact + " is the correct relationship clue."),
            CreateQuestion($"Which feature is most strongly associated with {profile.DisplayName}?", profile.FeatureFact, featureDistractors[0], featureDistractors[1], featureDistractors[2], profile.FeatureFact + " is one of the best-known facts about " + profile.DisplayName + "."),
            CreateQuestion($"Which statement about {profile.DisplayName}'s atmosphere or composition is correct?", profile.AtmosphereFact, atmosphereDistractors[0], atmosphereDistractors[1], atmosphereDistractors[2], profile.AtmosphereFact + " is the accurate composition clue."),
            CreateQuestion($"Which statement about {profile.DisplayName}'s system or motion is correct?", profile.SystemFact, systemDistractors[0], systemDistractors[1], systemDistractors[2], profile.SystemFact + " is the correct system or motion detail."),
            CreateQuestion($"Which fact makes {profile.DisplayName} especially notable?", profile.DistinctionFact, distinctionDistractors[0], distinctionDistractors[1], distinctionDistractors[2], profile.DistinctionFact + " is the standout fact here."),
            CreateQuestion($"Which object matches this clue: {profile.FeatureFact}?", profile.DisplayName, bodyDistractors[0], bodyDistractors[1], bodyDistractors[2], profile.DisplayName + " is identified by that feature."),
            CreateQuestion($"Which object fits both of these clues: {profile.RelationFact} and {profile.TypeLabel}?", profile.DisplayName, bodyDistractors[0], bodyDistractors[1], bodyDistractors[2], profile.DisplayName + " matches both clues.")
        };
    }

    private static List<QuizQuestionData> BuildProfileSpecificEasyQuestions(TopicFactProfile profile)
    {
        switch (profile.Key)
        {
            case "sun":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("What process powers the Sun's energy output?", "Nuclear fusion", "Reflection of moonlight", "Burning liquid water", "Collisions with comets", "The Sun releases energy through nuclear fusion in its core."),
                    CreateQuestion("Why is the Sun so important to Earth?", "It provides most of Earth's light and heat", "It orbits Earth once a day", "It creates tides more strongly than the Moon", "It is the nearest planet", "Sunlight and solar heat are essential for Earth's climate and life.")
                };
            case "mercury":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("About how long is one year on Mercury?", "88 Earth days", "24 hours", "365 Earth days", "12 Earth years", "Mercury circles the Sun in about 88 Earth days."),
                    CreateQuestion("Why does Mercury's temperature change so dramatically?", "It has almost no atmosphere to trap heat", "It is covered by thick oceans", "It produces its own light", "It has giant rings", "Mercury's thin exosphere cannot hold heat well.")
                };
            case "venus":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("Why is Venus hotter than Mercury?", "Its thick carbon dioxide atmosphere traps heat", "It is farther from the Sun", "It spins faster than all other planets", "It has the most moons", "Venus is hottest because its dense atmosphere causes a powerful greenhouse effect."),
                    CreateQuestion("What kind of clouds cover Venus?", "Sulfuric acid clouds", "Water-vapor rain clouds", "Methane ice clouds", "Ammonia storm clouds", "Venus is wrapped in thick clouds rich in sulfuric acid.")
                };
            case "earth":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("Which feature most helps Earth support life?", "Stable liquid water", "Bright rings", "Thick methane haze", "A giant red storm", "Liquid water is one of Earth's most important life-supporting features."),
                    CreateQuestion("How many natural moons does Earth have?", "One", "Two", "Four", "Seventy-nine", "Earth has one natural moon.")
                };
            case "moon":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("Why does the same side of the Moon always face Earth?", "The Moon is tidally locked", "The Moon does not rotate at all", "Earth's atmosphere blocks the other side", "The Sun pulls only one side", "The Moon rotates at the same rate that it orbits Earth."),
                    CreateQuestion("What natural effect is strongly linked to the Moon's gravity?", "Ocean tides", "Solar flares", "Jupiter's storms", "Saturn's rings", "The Moon's gravity helps drive ocean tides on Earth.")
                };
            case "mars":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("What gives Mars its reddish color?", "Iron oxide", "Liquid methane", "Sulfur clouds", "Blue ice", "Iron oxide, or rust, gives Mars its red appearance."),
                    CreateQuestion("What are Mars's two small moons?", "Phobos and Deimos", "Io and Europa", "Titan and Enceladus", "Triton and Nereid", "Mars has two small moons named Phobos and Deimos.")
                };
            case "jupiter":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("What is the Great Red Spot on Jupiter?", "A giant storm", "A solid island", "A frozen crater", "A glowing moon", "The Great Red Spot is a huge storm in Jupiter's atmosphere."),
                    CreateQuestion("Which group includes Jupiter's largest famous moons?", "Io, Europa, Ganymede, and Callisto", "Titan, Rhea, Dione, and Iapetus", "Phobos, Deimos, Titania, and Ariel", "Triton, Charon, Oberon, and Miranda", "The Galilean moons are Io, Europa, Ganymede, and Callisto.")
                };
            case "saturn":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("Saturn's rings are made mostly of what?", "Ice and rock particles", "Liquid fire", "Thick glass sheets", "Solid metal plates", "Saturn's rings are mainly icy particles mixed with rock and dust."),
                    CreateQuestion("Which large moon is especially associated with Saturn?", "Titan", "Europa", "Phobos", "Charon", "Titan is Saturn's largest and most famous moon.")
                };
            case "uranus":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("What gas gives Uranus its blue-green color?", "Methane", "Oxygen", "Nitrogen", "Carbon monoxide", "Methane absorbs red light and helps make Uranus look blue-green."),
                    CreateQuestion("Why are Uranus's seasons so unusual?", "Its axis is tilted strongly on its side", "It is closest to the Sun", "It has no atmosphere", "It spins only once per year", "Uranus is tilted so far that it experiences very unusual seasons.")
                };
            case "neptune":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("What is Neptune especially known for?", "Extremely fast winds", "Bright yellow deserts", "Thick ring walls", "Being the hottest planet", "Neptune is famous for its very powerful winds."),
                    CreateQuestion("What is Neptune's largest moon?", "Triton", "Titan", "Europa", "Deimos", "Triton is Neptune's largest moon.")
                };
            default:
                return new List<QuizQuestionData>();
        }
    }

    private static List<QuizQuestionData> BuildSharedHardQuestions(TopicFactProfile profile)
    {
        string[] bodyDistractors = GetBodyDistractors(profile.Key);
        string[] distinctionDistractors = GetFactDistractors(profile.Key, item => item.DistinctionFact, "Never moves in space", "Exists outside the Solar System", "Has no physical structure");
        string[] featureDistractors = GetFactDistractors(profile.Key, item => item.FeatureFact, "A surface made entirely of metal", "A permanent rainbow ocean", "A glowing crystal atmosphere");
        string[] atmosphereDistractors = GetFactDistractors(profile.Key, item => item.AtmosphereFact, "A shell of solid diamond", "A surface of burning wood", "A vacuum filled with liquid iron");
        string[] systemDistractors = GetFactDistractors(profile.Key, item => item.SystemFact, "Has continents connected by light bridges", "Never rotates or orbits anything", "Creates its own moons from sunlight");

        TopicFactProfile firstOther = GetNthOtherProfile(profile.Key, 0);
        TopicFactProfile secondOther = GetNthOtherProfile(profile.Key, 1);
        TopicFactProfile thirdOther = GetNthOtherProfile(profile.Key, 2);

        string correctPair = profile.DisplayName + " - " + profile.DistinctionFact;
        string wrongPairA = firstOther != null ? firstOther.DisplayName + " - " + profile.DistinctionFact : "Venus - " + profile.DistinctionFact;
        string wrongPairB = secondOther != null ? profile.DisplayName + " - " + secondOther.DistinctionFact : profile.DisplayName + " - Has glowing crystal oceans";
        string wrongPairC = firstOther != null && thirdOther != null ? thirdOther.DisplayName + " - " + firstOther.FeatureFact : "Mars - Producing light through fusion";

        return new List<QuizQuestionData>
        {
            CreateQuestion($"An observer reports an object that is {profile.RelationFact.ToLowerInvariant()} and known for {profile.FeatureFact.ToLowerInvariant()}. Which object is being described?", profile.DisplayName, bodyDistractors[0], bodyDistractors[1], bodyDistractors[2], profile.DisplayName + " matches both clues."),
            CreateQuestion($"Which object best matches this combination: {profile.AtmosphereFact} and {profile.SystemFact.ToLowerInvariant()}?", profile.DisplayName, bodyDistractors[0], bodyDistractors[1], bodyDistractors[2], profile.DisplayName + " is the object that matches those combined facts."),
            CreateQuestion("Which pair correctly matches an object and one verified fact?", correctPair, wrongPairA, wrongPairB, wrongPairC, correctPair + " is the only accurate match."),
            CreateQuestion($"Which statement best distinguishes {profile.DisplayName} from many other Solar System objects?", profile.DistinctionFact, distinctionDistractors[0], distinctionDistractors[1], distinctionDistractors[2], profile.DistinctionFact + " is the strongest distinguishing clue."),
            CreateQuestion($"Which observation would most strongly suggest an astronomer is studying {profile.DisplayName}?", profile.FeatureFact, featureDistractors[0], featureDistractors[1], featureDistractors[2], profile.FeatureFact + " is the signature observation here."),
            CreateQuestion($"Which atmosphere or composition clue points most clearly to {profile.DisplayName}?", profile.AtmosphereFact, atmosphereDistractors[0], atmosphereDistractors[1], atmosphereDistractors[2], profile.AtmosphereFact + " is the best composition clue."),
            CreateQuestion($"Which clue about motion or system best fits {profile.DisplayName}?", profile.SystemFact, systemDistractors[0], systemDistractors[1], systemDistractors[2], profile.SystemFact + " is the accurate system or motion clue."),
            CreateQuestion($"Which object is most likely being described as a {profile.TypeLabel.ToLowerInvariant()} with this relationship: {profile.RelationFact}?", profile.DisplayName, bodyDistractors[0], bodyDistractors[1], bodyDistractors[2], profile.DisplayName + " fits the type and relationship clues together.")
        };
    }

    private static List<QuizQuestionData> BuildProfileSpecificHardQuestions(TopicFactProfile profile)
    {
        switch (profile.Key)
        {
            case "sun":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("Why can the Sun emit its own light while planets cannot?", "Fusion in its core produces energy", "It reflects light from Jupiter", "It is made of solid rock", "It has no gravity", "The Sun shines because nuclear fusion releases huge amounts of energy."),
                    CreateQuestion("Which fact best explains why planets stay in orbit around the Sun?", "The Sun's gravity is strongest in the Solar System", "The Sun spins faster than every planet", "The Moon pulls planets inward", "Planetary rings connect them", "The Sun's gravity is the main force that keeps planets in orbit.")
                };
            case "mercury":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("Why are Mercury's day and night temperatures extremely different?", "Its thin exosphere cannot retain heat well", "It has a thick ocean that evaporates daily", "It is the largest gas giant", "It receives no sunlight", "Mercury has almost no atmosphere to smooth out temperature changes."),
                    CreateQuestion("Why is a year on Mercury much shorter than a year on Earth?", "Mercury travels in a small, fast orbit close to the Sun", "Mercury rotates backward", "Mercury has two suns", "Mercury is farther from the Sun", "Mercury completes its orbit quickly because it is very close to the Sun.")
                };
            case "venus":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("Why is Venus hotter than Mercury even though Mercury is closer to the Sun?", "Venus's dense atmosphere creates a strong greenhouse effect", "Venus has no atmosphere", "Venus is made of fire", "Venus absorbs heat from Jupiter", "Venus traps heat extremely well because of its thick carbon dioxide atmosphere."),
                    CreateQuestion("Why is Venus sometimes called Earth's twin but still very hostile?", "It is similar in size to Earth but has crushing pressure and extreme heat", "It has the same oceans as Earth", "It rotates with Earth's moon", "It has life but no land", "Venus is close to Earth in size, but its environment is far more hostile.")
                };
            case "earth":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("Which combination best explains why Earth can support life?", "Liquid water, moderate temperatures, and a protective atmosphere", "Giant rings, methane clouds, and no moon", "Extreme greenhouse heating and no oceans", "A solid hydrogen surface and constant storms", "Earth's water and protective atmosphere are key reasons it can support life."),
                    CreateQuestion("Why do Earth and its Moon remain together as a system?", "Earth's gravity keeps the Moon in orbit", "The Sun's rings connect them", "Mars pushes the Moon around Earth", "The Moon has no motion of its own", "The Moon stays near Earth because Earth's gravity keeps it in orbit.")
                };
            case "moon":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("Why do lunar phases change over time?", "We see different sunlit portions as the Moon orbits Earth", "The Moon creates its own changing light", "Earth's shadow covers the Moon every night", "The Sun moves around the Moon", "Lunar phases change because the Moon's sunlit half is viewed from different angles."),
                    CreateQuestion("Why does the Moon keep so many visible craters compared with Earth?", "It lacks a thick atmosphere, liquid water, and active weather that erase impacts", "It is closer to Jupiter", "It is made of gas", "It receives no sunlight", "The Moon preserves craters because little erosion happens there.")
                };
            case "mars":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("Why do scientists see Mars as an important place to study past habitability?", "Evidence suggests ancient water once existed there", "Mars is hotter than the Sun", "Mars has thick oceans today", "Mars is the only planet with gravity", "Signs of ancient water make Mars important in the search for past habitability."),
                    CreateQuestion("Which evidence best supports the idea that Mars has a thin atmosphere?", "It has large temperature swings and frequent dust storms", "It has bright icy rings", "It produces its own light", "It is covered by liquid methane seas", "Big temperature changes are one sign that Mars has a thin atmosphere.")
                };
            case "jupiter":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("Why is Jupiter classified as a gas giant rather than a terrestrial planet?", "It is composed mostly of hydrogen and helium and lacks a solid rocky surface", "It is smaller than Mercury", "It has no gravity", "It is Earth's satellite", "Jupiter is a gas giant because it is massive and made mostly of light gases."),
                    CreateQuestion("Why does Jupiter strongly influence nearby objects in the Solar System?", "Its enormous mass gives it very strong gravity", "Its surface is made of iron oxide", "It is closest to Earth", "It has no moons", "Jupiter's huge mass gives it a powerful gravitational pull.")
                };
            case "saturn":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("Why do Saturn's rings appear especially bright from a distance?", "Sunlight reflects off the icy ring particles", "The rings are made of molten metal", "The rings create their own light", "The rings absorb all sunlight", "Saturn's icy ring particles reflect a lot of sunlight."),
                    CreateQuestion("Why is Saturn's average density unusual among planets?", "It is so low that it is less dense than water", "It is made only of iron", "It has no atmosphere", "It is the hottest planet", "Saturn's average density is remarkably low for such a large planet.")
                };
            case "uranus":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("Why does Uranus experience extreme and unusual seasons?", "Its axis is tilted about 98 degrees, so it rotates almost on its side", "It has no sunlight", "It orbits Earth", "Its rings block all heat", "Uranus's dramatic tilt leads to its unusual seasonal pattern."),
                    CreateQuestion("Which clue best identifies Uranus instead of Neptune?", "Uranus is the ice giant famous for rotating on its side", "Uranus has the fastest winds in the Solar System", "Uranus is the closest planet to the Sun", "Uranus is the only star in our system", "Uranus is best known for its sideways rotation.")
                };
            case "neptune":
                return new List<QuizQuestionData>
                {
                    CreateQuestion("Why can Neptune have powerful winds even though it is very far from the Sun?", "Internal heat and atmospheric dynamics help drive strong storms", "Neptune has no atmosphere", "Neptune is the hottest planet", "The Sun is closest to Neptune", "Neptune's internal energy helps power its strong winds."),
                    CreateQuestion("Which clue best distinguishes Neptune from Uranus?", "Neptune is best known for its extremely fast winds and Triton", "Neptune rotates on its side more than any planet", "Neptune is the brightest ringed planet", "Neptune is Earth's natural satellite", "Neptune stands out for its powerful winds and its large moon Triton.")
                };
            default:
                return new List<QuizQuestionData>();
        }
    }

    private static List<QuizQuestionData> DeduplicateQuestions(List<QuizQuestionData> questions)
    {
        List<QuizQuestionData> uniqueQuestions = new List<QuizQuestionData>();
        if (questions == null)
        {
            return uniqueQuestions;
        }

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < questions.Count; i++)
        {
            QuizQuestionData question = questions[i];
            if (question == null || string.IsNullOrWhiteSpace(question.question))
            {
                continue;
            }

            string key = NormalizeQuestionKey(question.question);
            if (string.IsNullOrWhiteSpace(key) || !seen.Add(key))
            {
                continue;
            }

            uniqueQuestions.Add(question);
        }

        return uniqueQuestions;
    }

    private static string[] GetTypeDistractors(string correctType)
    {
        string[] allTypes =
        {
            "Star",
            "Terrestrial planet",
            "Gas giant",
            "Ice giant",
            "Natural satellite"
        };

        List<string> distractors = new List<string>();
        for (int i = 0; i < allTypes.Length; i++)
        {
            if (!string.Equals(allTypes[i], correctType, StringComparison.OrdinalIgnoreCase))
            {
                distractors.Add(allTypes[i]);
            }
        }

        return distractors.Take(3).ToArray();
    }

    private static string[] GetBodyDistractors(string key)
    {
        List<string> distractors = new List<string>();
        TopicFactProfile[] profiles = GetAllTopicProfiles();
        for (int i = 0; i < profiles.Length; i++)
        {
            if (!string.Equals(profiles[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                distractors.Add(profiles[i].DisplayName);
            }
        }

        return distractors.Take(3).ToArray();
    }

    private static string[] GetFactDistractors(string key, Func<TopicFactProfile, string> selector, params string[] fallbacks)
    {
        List<string> distractors = new List<string>();
        TopicFactProfile[] profiles = GetAllTopicProfiles();
        for (int i = 0; i < profiles.Length; i++)
        {
            TopicFactProfile profile = profiles[i];
            if (string.Equals(profile.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = selector(profile);
            if (string.IsNullOrWhiteSpace(value) || ContainsIgnoreCase(distractors, value))
            {
                continue;
            }

            distractors.Add(value);
            if (distractors.Count == 3)
            {
                break;
            }
        }

        for (int i = 0; i < fallbacks.Length && distractors.Count < 3; i++)
        {
            if (!string.IsNullOrWhiteSpace(fallbacks[i]) && !ContainsIgnoreCase(distractors, fallbacks[i]))
            {
                distractors.Add(fallbacks[i]);
            }
        }

        while (distractors.Count < 3)
        {
            distractors.Add("Unknown observation");
        }

        return distractors.Take(3).ToArray();
    }

    private static bool ContainsIgnoreCase(List<string> values, string candidate)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static TopicFactProfile GetNthOtherProfile(string key, int index)
    {
        int foundCount = 0;
        TopicFactProfile[] profiles = GetAllTopicProfiles();
        for (int i = 0; i < profiles.Length; i++)
        {
            if (string.Equals(profiles[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (foundCount == index)
            {
                return profiles[i];
            }

            foundCount++;
        }

        return null;
    }

    private static TopicFactProfile GetTopicFactProfile(string topic)
    {
        string normalizedTopic = NormalizeTopicKey(topic);
        TopicFactProfile[] profiles = GetAllTopicProfiles();
        for (int i = 0; i < profiles.Length; i++)
        {
            if (string.Equals(profiles[i].Key, normalizedTopic, StringComparison.OrdinalIgnoreCase))
            {
                return profiles[i];
            }
        }

        return null;
    }

    private static TopicFactProfile[] GetAllTopicProfiles()
    {
        return new[]
        {
            new TopicFactProfile
            {
                Key = "sun",
                DisplayName = "Sun",
                TypeLabel = "Star",
                RelationFact = "At the center of the Solar System",
                FeatureFact = "Producing light and heat through nuclear fusion",
                AtmosphereFact = "A composition dominated by hydrogen and helium plasma",
                SystemFact = "Its gravity holds the planets in orbit",
                DistinctionFact = "Is the only star in our Solar System",
                Keywords = new[] { "sun", "star", "fusion", "center of the solar system", "hydrogen", "helium", "gravity" }
            },
            new TopicFactProfile
            {
                Key = "mercury",
                DisplayName = "Mercury",
                TypeLabel = "Terrestrial planet",
                RelationFact = "The closest planet to the Sun",
                FeatureFact = "A small, rocky world covered with many craters",
                AtmosphereFact = "An extremely thin exosphere that cannot trap much heat",
                SystemFact = "It has no natural moons or rings",
                DistinctionFact = "Has the shortest year of the major planets",
                Keywords = new[] { "mercury", "closest planet", "88 earth days", "craters", "thin exosphere", "smallest planet" }
            },
            new TopicFactProfile
            {
                Key = "venus",
                DisplayName = "Venus",
                TypeLabel = "Terrestrial planet",
                RelationFact = "The second planet from the Sun",
                FeatureFact = "A cloud-covered world with a powerful greenhouse effect",
                AtmosphereFact = "A dense carbon dioxide atmosphere with sulfuric acid clouds",
                SystemFact = "It has no natural moons and rotates very slowly",
                DistinctionFact = "Is the hottest planet in the Solar System",
                Keywords = new[] { "venus", "hottest planet", "greenhouse effect", "carbon dioxide", "sulfuric acid clouds", "second planet" }
            },
            new TopicFactProfile
            {
                Key = "earth",
                DisplayName = "Earth",
                TypeLabel = "Terrestrial planet",
                RelationFact = "The third planet from the Sun",
                FeatureFact = "A world with stable liquid water and life",
                AtmosphereFact = "A nitrogen-oxygen atmosphere that supports living things",
                SystemFact = "It has one natural moon and active plate tectonics",
                DistinctionFact = "Is the only known habitable planet in the Solar System",
                Keywords = new[] { "earth", "life", "liquid water", "nitrogen oxygen atmosphere", "habitable", "third planet" }
            },
            new TopicFactProfile
            {
                Key = "moon",
                DisplayName = "Moon",
                TypeLabel = "Natural satellite",
                RelationFact = "Earth's natural satellite",
                FeatureFact = "Showing the same face to Earth because it is tidally locked",
                AtmosphereFact = "No thick atmosphere and a heavily cratered surface",
                SystemFact = "Its gravity strongly affects Earth's ocean tides",
                DistinctionFact = "Is the only natural moon of Earth",
                Keywords = new[] { "moon", "natural satellite", "tidally locked", "tides", "craters", "phases" }
            },
            new TopicFactProfile
            {
                Key = "mars",
                DisplayName = "Mars",
                TypeLabel = "Terrestrial planet",
                RelationFact = "The fourth planet from the Sun",
                FeatureFact = "A reddish world with Olympus Mons and signs of ancient water",
                AtmosphereFact = "A thin atmosphere made mostly of carbon dioxide",
                SystemFact = "It has two small moons named Phobos and Deimos",
                DistinctionFact = "Is widely known as the Red Planet",
                Keywords = new[] { "mars", "red planet", "iron oxide", "olympus mons", "ancient water", "phobos", "deimos" }
            },
            new TopicFactProfile
            {
                Key = "jupiter",
                DisplayName = "Jupiter",
                TypeLabel = "Gas giant",
                RelationFact = "The fifth planet from the Sun",
                FeatureFact = "A giant planet famous for the Great Red Spot",
                AtmosphereFact = "An atmosphere made mostly of hydrogen and helium",
                SystemFact = "It has many moons including Io, Europa, Ganymede, and Callisto",
                DistinctionFact = "Is the largest planet in the Solar System",
                Keywords = new[] { "jupiter", "largest planet", "great red spot", "gas giant", "hydrogen and helium", "galilean moons" }
            },
            new TopicFactProfile
            {
                Key = "saturn",
                DisplayName = "Saturn",
                TypeLabel = "Gas giant",
                RelationFact = "The sixth planet from the Sun",
                FeatureFact = "A giant planet surrounded by bright rings",
                AtmosphereFact = "An atmosphere made mostly of hydrogen and helium",
                SystemFact = "It has many moons, including Titan, and broad icy rings",
                DistinctionFact = "Is the planet most famous for its visible ring system",
                Keywords = new[] { "saturn", "rings", "titan", "gas giant", "icy ring system", "sixth planet" }
            },
            new TopicFactProfile
            {
                Key = "uranus",
                DisplayName = "Uranus",
                TypeLabel = "Ice giant",
                RelationFact = "The seventh planet from the Sun",
                FeatureFact = "Rotating on its side with a blue-green appearance",
                AtmosphereFact = "Methane in the atmosphere gives it a blue-green color",
                SystemFact = "It has faint rings and many moons",
                DistinctionFact = "Has the most dramatically tilted axis of the major planets",
                Keywords = new[] { "uranus", "ice giant", "tilted axis", "rotates on its side", "methane", "blue green" }
            },
            new TopicFactProfile
            {
                Key = "neptune",
                DisplayName = "Neptune",
                TypeLabel = "Ice giant",
                RelationFact = "The eighth and farthest major planet from the Sun",
                FeatureFact = "A deep blue world with extremely fast winds",
                AtmosphereFact = "Methane-rich upper layers over an ice-giant interior",
                SystemFact = "It has a large moon named Triton",
                DistinctionFact = "Is the windiest major planet in the Solar System",
                Keywords = new[] { "neptune", "ice giant", "fast winds", "triton", "farthest planet", "deep blue" }
            }
        };
    }

    private static string NormalizeTopicKey(string topic)
    {
        return string.IsNullOrWhiteSpace(topic) ? string.Empty : topic.Trim().ToLowerInvariant();
    }

    private static string ResolveTopicDisplayName(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return "Solar System";
        }

        string trimmed = topic.Trim();
        return char.ToUpper(trimmed[0]) + trimmed.Substring(1).ToLowerInvariant();
    }

    private static QuizQuestionData CreateQuestion(string question, string correctAnswer, string wrongA, string wrongB, string wrongC, string explanation)
    {
        List<string> choices = new List<string> { correctAnswer, wrongA, wrongB, wrongC };
        Shuffle(choices);

        return new QuizQuestionData
        {
            question = question,
            choices = choices,
            correctAnswer = correctAnswer,
            explanation = explanation
        };
    }

    private static QuizQuestionData CloneQuestion(QuizQuestionData question)
    {
        if (question == null)
        {
            return null;
        }

        return new QuizQuestionData
        {
            question = question.question,
            choices = question.choices != null ? new List<string>(question.choices) : new List<string>(),
            correctAnswer = question.correctAnswer,
            explanation = question.explanation
        };
    }

    private static string NormalizeQuestionKey(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(question.Length);
        bool previousWasSpace = false;

        for (int i = 0; i < question.Length; i++)
        {
            char character = char.ToLowerInvariant(question[i]);
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static void Shuffle<T>(IList<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            T temp = values[i];
            values[i] = values[swapIndex];
            values[swapIndex] = temp;
        }
    }

    private static void SetPageActive(GameObject page, bool isActive)
    {
        if (page != null)
        {
            page.SetActive(isActive);
        }
    }

    private static Button FindButton(GameObject root, string objectName)
    {
        return FindComponentInChildrenByName<Button>(root, objectName);
    }

    private static Image FindImage(GameObject root, string objectName)
    {
        return FindComponentInChildrenByName<Image>(root, objectName);
    }

    private static RectTransform FindRectTransform(GameObject root, string objectName, string requiredChildName)
    {
        if (root == null)
        {
            return null;
        }

        RectTransform[] transforms = root.GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform transform in transforms)
        {
            if (transform == null || transform.gameObject.name != objectName)
            {
                continue;
            }

            if (FindObjectByNameWithinPage(transform.gameObject, requiredChildName) != null)
            {
                return transform;
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
        return FindComponentInChildrenByName<TextMeshProUGUI>(root, objectName);
    }

    private static TextMeshProUGUI FindTextContaining(GameObject root, string fragment)
    {
        if (root == null || string.IsNullOrWhiteSpace(fragment))
        {
            return null;
        }

        TextMeshProUGUI[] textComponents = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI textComponent in textComponents)
        {
            if (textComponent != null && !string.IsNullOrWhiteSpace(textComponent.text) && textComponent.text.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return textComponent;
            }
        }

        return null;
    }

    private static void ConfigureAnswerText(TextMeshProUGUI textComponent)
    {
        if (textComponent == null)
        {
            return;
        }

        textComponent.enableAutoSizing = true;
        textComponent.fontSizeMax = Mathf.Max(textComponent.fontSizeMax, textComponent.fontSize);
        textComponent.fontSizeMin = Mathf.Min(textComponent.fontSizeMin > 0f ? textComponent.fontSizeMin : 18f, 16f);
        textComponent.textWrappingMode = TextWrappingModes.Normal;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
        textComponent.alignment = TextAlignmentOptions.Center;

        RectTransform rectTransform = textComponent.rectTransform;
        if (rectTransform != null && rectTransform.parent is RectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = new Vector2(18f, 12f);
            rectTransform.offsetMax = new Vector2(-18f, -12f);
        }
    }

    private static T FindComponentInChildrenByName<T>(GameObject root, string objectName) where T : Component
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        T[] components = root.GetComponentsInChildren<T>(true);
        foreach (T component in components)
        {
            if (component != null && component.gameObject.name == objectName)
            {
                return component;
            }
        }

        return null;
    }

    private static GameObject FindObjectByNameWithinPage(GameObject root, string objectName)
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
}
