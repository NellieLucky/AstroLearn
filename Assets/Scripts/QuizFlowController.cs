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
    private const int OllamaQuizTimeoutSeconds = 40;
    private const int OllamaQuizMaxTokens = 650;

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

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            yield return StartCoroutine(RequestQuizFromEndpoint(endpoint.Trim(), bearerToken, anonKey, topic, difficulty, result => session = result));
        }

        bool canUseLocalQuizModel = !string.IsNullOrWhiteSpace(ollamaEndpoint) && !string.IsNullOrWhiteSpace(ollamaModel);
        if (session == null && canUseLocalQuizModel)
        {
            Debug.Log("[QuizFlowController] Gemini unavailable. Trying local Ollama quiz generation.");
            yield return StartCoroutine(RequestQuizFromOllama(ollamaEndpoint.Trim(), ollamaModel.Trim(), topic, difficulty, result => session = result));
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
            stream = false,
            keep_alive = "30m",
            options = new OllamaOptionsPayload
            {
                num_predict = OllamaQuizMaxTokens,
                temperature = 0.35f
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
            if (!ValidateSession(parsedSession, LocalGeneratedQuestionCount, DefaultQuestionCount))
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

        while (session.questions.Count < DefaultQuestionCount && fallbackPool.Count > 0)
        {
            QuizQuestionData question = fallbackPool[0];
            fallbackPool.RemoveAt(0);
            session.questions.Add(question);
            usedKeys.Add(NormalizeQuestionKey(question.question));
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
        builder.AppendLine("Each question must have exactly 4 choices.");
        builder.AppendLine("The correctAnswer must exactly match one of the 4 choices.");
        builder.AppendLine("Avoid duplicate questions.");
        builder.AppendLine("Keep each explanation to one short sentence.");
        builder.AppendLine("Make the questions suitable for Grade 7 to Grade 12 learners.");
        if (string.Equals(resolvedDifficulty, "Easy", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine("Easy questions should focus on direct facts, recognition, and basic understanding.");
        }
        else
        {
            builder.AppendLine("Hard questions should focus on deeper reasoning, comparison, and inference while still being answerable by students.");
        }

        return builder.ToString();
    }

    private static List<QuizQuestionData> BuildEasyFallbackQuestionPool(string topic)
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

    private static List<QuizQuestionData> BuildHardFallbackQuestionPool(string topic)
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
