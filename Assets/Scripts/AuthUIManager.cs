using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AuthUIManager : MonoBehaviour
{
    private enum QuizReturnTarget
    {
        SolarSystem,
        FocusedCelestialBody
    }

    public static bool ForceSolarSystemUiOnNextStart { get; set; }
    public static bool SuppressStartupFlowOnNextStart { get; set; }

    [SerializeField] private float splashPageDuration = 2f;

    [Header("Password Visibility Icons")]
    [SerializeField] private Sprite showPasswordIcon;
    [SerializeField] private Sprite hidePasswordIcon;

    private GameObject launchUiRoot;
    private GameObject splashPage;
    private GameObject landingPage;
    private GameObject logInPage;
    private GameObject createAccountPage;
    private GameObject forgotPasswordPage;
    private GameObject verifyPasswordPage;
    private GameObject resetPasswordPage;
    private GameObject verifyAccountPage;
    private GameObject setDisplayNamePage;
    private GameObject menuRoot;

    private GameObject solarSystemUiRoot;
    private GameObject solarSystemRoot;
    private GameObject celestialBodyUiRoot;
    private GameObject quizUiRoot;
    private GameObject quizUiContainerRoot;
    private GameObject aiUiRoot;
    private GameObject arUiRoot;
    private GameObject planetInfoCard;
    private GameObject imagesGalleryOverlay;
    private GameObject imageViewerOverlay;

    private GameObject quizHomePage;
    private GameObject quizTopicPage;
    private GameObject quizQuestionPage;
    private GameObject quizResultPage;
    private GameObject quizHistoryPage;
    private GameObject quizIntroPage;
    private GameObject quizBreakdownPage;

    private Button landingStartButton;
    private Button loginPageBackButton;
    private Button logInButton;
    private Button forgotPasswordButton;
    private Button createAccountFromLoginButton;
    private Button guestModeButton;
    private Button createAccountBackButton;
    private Button createAccountSubmitButton;
    private Button createAccountLogInButton;
    private Button forgotPasswordBackButton;
    private Button sendPasswordButton;
    private Button verifyPasswordBackButton;
    private Button verifyPasswordButton;
    private Button verifyPasswordResendCodeButton;
    private Button resetPasswordBackButton;
    private Button resetPasswordSubmitButton;
    private Button verifyAccountBackButton;
    private Button verifyAccountButton;
    private Button verifyAccountResendCodeButton;
    private Button logOutButton;
    private readonly List<Button> backToLoginButtons = new List<Button>();
    private readonly List<Button> menuOpenButtons = new List<Button>();
    private readonly List<Button> menuBackButtons = new List<Button>();
    private readonly List<Button> quizHistoryButtons = new List<Button>();

    private Button quizButton;
    private Button askAiButton;
    private readonly List<Button> quizBackButtons = new List<Button>();
    private Button quizHomeBackButton;
    private Button quizIntroBackButton;
    private Button quizIntroStartButton;
    private Button quizHistoryBackButton;
    private Button aiExitButton;
    private QuizReturnTarget quizReturnTarget = QuizReturnTarget.SolarSystem;

    private TMP_InputField logInEmailInputField;
    private TMP_InputField logInPasswordInputField;
    private TMP_InputField createAccountDisplayNameInputField;
    private TMP_InputField createAccountEmailInputField;
    private TMP_InputField createAccountPasswordInputField;
    private TMP_InputField createAccountConfirmPasswordInputField;
    private TMP_InputField forgotPasswordEmailInputField;
    private GameObject verifyAccountOtpContainer;
    private GameObject verifyPasswordOtpContainer;
    private TMP_InputField resetPasswordInputField;
    private TMP_InputField resetConfirmPasswordInputField;
    private TMP_Text resetPasswordNoteText;
    private TMP_Text resetPasswordStatusText;
    private TMP_Text verifyPasswordStatusText;
    private TMP_Text authStatusText;
    private TMP_Text createAccountStatusText;
    private TMP_Text forgotPasswordStatusText;
    private TMP_Text verifyAccountStatusText;
    private GameObject authLoadingOverlay;
    private TMP_Text loadingText;

    private TMP_InputField displayNameInputField;
    private Button confirmIdentityButton;
    private Button randomizeNameButton;
    private TMP_Text displayNameCharCounterText;

    private Coroutine splashTransitionCoroutine;
    private TMP_Text verifyAccountTimerText;
    private TMP_Text verifyPasswordTimerText;
    private Coroutine verifyAccountTimerCoroutine;
    private Coroutine verifyPasswordTimerCoroutine;
    private Coroutine lockoutTimerCoroutine;
    private bool isClearingOtp = false;

    private TMP_Text verifyAccountNoteText;
    private TMP_Text verifyPasswordNoteText;
    private TMP_Text verifyAccountTitleText;
    private TMP_Text verifyPasswordTitleText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<AuthUIManager>() != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("AuthUIManager");
        managerObject.AddComponent<AuthUIManager>();
    }

    private void Awake()
    {
        AuthUIManager[] managers = FindObjectsByType<AuthUIManager>(FindObjectsSortMode.None);
        if (managers.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        ResolveSceneReferences();
        RegisterListeners();
    }

    private void Start()
    {
        if (SuppressStartupFlowOnNextStart)
        {
            SuppressStartupFlowOnNextStart = false;
            RefreshSceneBindings();

            if (launchUiRoot != null)
            {
                launchUiRoot.SetActive(false);
            }

            SetPageActive(menuRoot, false);
            SetLoadingState(false);
            ApplyHistoryAccessState();
            return;
        }

        if (ForceSolarSystemUiOnNextStart)
        {
            ForceSolarSystemUiOnNextStart = false;
            RefreshSceneBindings();
            ApplyHistoryAccessState();
            return;
        }

        InitializeNavigationState();
        ApplyHistoryAccessState();

        SetupPasswordToggle(logInPasswordInputField);
        SetupPasswordToggle(createAccountPasswordInputField);
        SetupPasswordToggle(createAccountConfirmPasswordInputField);
        SetupPasswordToggle(resetPasswordInputField);
        SetupPasswordToggle(resetConfirmPasswordInputField);

        if (logInEmailInputField != null)
        {
            logInEmailInputField.characterLimit = 100;
            logInEmailInputField.onValueChanged.AddListener((val) =>
            {
                string email = val.Trim();
                if (IsLockedOut(email, out string lockoutMessage))
                {
                    SetStatus(authStatusText, lockoutMessage);
                    StartLockoutTimer(email);
                }
                else
                {
                    if (authStatusText != null && authStatusText.text.StartsWith("Too many failed attempts"))
                    {
                        SetStatus(authStatusText, string.Empty);
                    }
                }
            });
        }
        if (logInPasswordInputField != null) logInPasswordInputField.characterLimit = 100;
        if (createAccountDisplayNameInputField != null) createAccountDisplayNameInputField.characterLimit = 50;
        if (createAccountEmailInputField != null) createAccountEmailInputField.characterLimit = 100;
        if (createAccountPasswordInputField != null) createAccountPasswordInputField.characterLimit = 100;
        if (createAccountConfirmPasswordInputField != null) createAccountConfirmPasswordInputField.characterLimit = 100;
        if (forgotPasswordEmailInputField != null) forgotPasswordEmailInputField.characterLimit = 100;
        if (resetPasswordInputField != null) resetPasswordInputField.characterLimit = 100;
        if (resetConfirmPasswordInputField != null) resetConfirmPasswordInputField.characterLimit = 100;

        // Force Display Name input fields to allow spaces and other symbols
        if (createAccountDisplayNameInputField != null)
        {
            createAccountDisplayNameInputField.characterValidation = TMP_InputField.CharacterValidation.None;
            createAccountDisplayNameInputField.contentType = TMP_InputField.ContentType.Standard;
        }
        if (displayNameInputField != null)
        {
            displayNameInputField.characterValidation = TMP_InputField.CharacterValidation.None;
            displayNameInputField.contentType = TMP_InputField.ContentType.Standard;
        }

        // Force Password input fields to allow any symbols and copy-pasting
        if (logInPasswordInputField != null)
        {
            logInPasswordInputField.characterValidation = TMP_InputField.CharacterValidation.None;
        }
        if (createAccountPasswordInputField != null)
        {
            createAccountPasswordInputField.characterValidation = TMP_InputField.CharacterValidation.None;
        }
        if (createAccountConfirmPasswordInputField != null)
        {
            createAccountConfirmPasswordInputField.characterValidation = TMP_InputField.CharacterValidation.None;
        }
        if (resetPasswordInputField != null)
        {
            resetPasswordInputField.characterValidation = TMP_InputField.CharacterValidation.None;
        }
        if (resetConfirmPasswordInputField != null)
        {
            resetConfirmPasswordInputField.characterValidation = TMP_InputField.CharacterValidation.None;
        }

        SetupOtpInputContainer(verifyAccountOtpContainer);
        SetupOtpInputContainer(verifyPasswordOtpContainer);

        if (SupabaseAuthService.Instance != null && SupabaseAuthService.Instance.HasStoredSession && !SupabaseAuthService.Instance.IsGuestMode)
        {
            StartCoroutine(TryRestoreSessionRoutine());
        }
    }

    private void OnDestroy()
    {
        UnregisterListeners();

        if (splashTransitionCoroutine != null)
        {
            StopCoroutine(splashTransitionCoroutine);
            splashTransitionCoroutine = null;
        }

        if (verifyAccountTimerCoroutine != null)
        {
            StopCoroutine(verifyAccountTimerCoroutine);
            verifyAccountTimerCoroutine = null;
        }

        if (verifyPasswordTimerCoroutine != null)
        {
            StopCoroutine(verifyPasswordTimerCoroutine);
            verifyPasswordTimerCoroutine = null;
        }

        if (lockoutTimerCoroutine != null)
        {
            StopCoroutine(lockoutTimerCoroutine);
            lockoutTimerCoroutine = null;
        }
    }

    private void ResolveSceneReferences()
    {
        launchUiRoot = FindObjectByName("LaunchUI");
        splashPage = FindObjectByName("SplashPage");
        landingPage = FindObjectByName("LandingPage");
        logInPage = FindObjectByName("LogInPage");
        createAccountPage = FindObjectByName("CreateAccountPage");
        forgotPasswordPage = FindObjectByName("ForgotPasswordPage");
        verifyPasswordPage = FindObjectByName("VerifyPasswordPage");
        if (verifyPasswordPage == null) verifyPasswordPage = FindObjectByName("PasswordVerificationPage");
        if (verifyPasswordPage == null) verifyPasswordPage = FindObjectByName("VerifyResetPage");

        resetPasswordPage = FindObjectByName("ResetPasswordPage");
        if (resetPasswordPage == null) resetPasswordPage = FindObjectByName("PasswordResetPage");
        if (resetPasswordPage == null) resetPasswordPage = FindObjectByName("ResetPage");

        verifyAccountPage = FindObjectByName("VerifyAccountPage");
        if (verifyAccountPage == null) verifyAccountPage = FindObjectByName("AccountVerificationPage");

        menuRoot = FindObjectByName("Menu");

        solarSystemUiRoot = FindObjectByName("SolarSystemUI");
        solarSystemRoot = FindObjectByName("SolarSystemRoot");
        celestialBodyUiRoot = FindObjectByName("CelestialBodyUI");
        quizUiRoot = FindObjectByName("QuizManager") ?? FindObjectByName("QuizUI");
        quizUiContainerRoot = quizUiRoot != null && quizUiRoot.transform.parent != null
            ? quizUiRoot.transform.parent.gameObject
            : null;
        aiUiRoot = FindObjectByName("AIUI") ?? FindObjectByName("ChatbotCanvas") ?? FindObjectByName("Chatbot Canvas");
        if (aiUiRoot == null)
        {
            GameObject chatPage = FindObjectByName("ChatPage");
            if (chatPage != null)
            {
                aiUiRoot = GetTopLevelSceneParent(chatPage);
            }
        }
        arUiRoot = FindObjectByName("ARUI");
        planetInfoCard = FindObjectByName("PlanetInfoCard");
        imagesGalleryOverlay = FindObjectByName("ImagesGalleryOverlay");
        imageViewerOverlay = FindObjectByName("ImageViewerOverlay");

        landingStartButton = FindButton(landingPage, "StartButton");
        loginPageBackButton = FindButton(logInPage, "BackButton");
        logInButton = FindButton(logInPage, "LogInButton");
        forgotPasswordButton = FindButton(logInPage, "ForgotPasswordButton");
        createAccountFromLoginButton = FindButton(logInPage, "CreateAccountButton");
        guestModeButton = FindButton(logInPage, "GuestModeButton");

        createAccountBackButton = FindButton(createAccountPage, "BackButton");
        createAccountSubmitButton = FindButton(createAccountPage, "CreateAccountButton");
        createAccountLogInButton = FindButton(createAccountPage, "LogInButton");

        forgotPasswordBackButton = FindButton(forgotPasswordPage, "BackButton");
        sendPasswordButton = FindButton(forgotPasswordPage, "SendPasswordButton");

        verifyPasswordBackButton = FindButton(verifyPasswordPage, "BackButton");
        
        verifyPasswordButton = FindButton(verifyPasswordPage, "VerifyButton");
        if (verifyPasswordButton == null) verifyPasswordButton = FindButton(verifyPasswordPage, "ResetPasswordButton");
        if (verifyPasswordButton == null) verifyPasswordButton = FindButton(verifyPasswordPage, "ResetButton");
        if (verifyPasswordButton == null) verifyPasswordButton = FindButton(verifyPasswordPage, "VerifyPasswordButton");
        
        verifyPasswordResendCodeButton = FindButton(verifyPasswordPage, "ResendCodeButton");

        resetPasswordBackButton = FindButton(resetPasswordPage, "BackButton");
        resetPasswordSubmitButton = FindButton(resetPasswordPage, "ResetPasswordButton");

        verifyAccountBackButton = FindButton(verifyAccountPage, "BackButton");
        
        verifyAccountButton = FindButton(verifyAccountPage, "VerifyButton");
        if (verifyAccountButton == null) verifyAccountButton = FindButton(verifyAccountPage, "VerifyAccountButton");
        
        verifyAccountResendCodeButton = FindButton(verifyAccountPage, "ResendCodeButton");

        // Diagnostic logs to help developers in the editor
        if (verifyPasswordPage == null) Debug.LogWarning("[AuthUIManager] verifyPasswordPage GameObject was not found in the scene.");
        if (resetPasswordPage == null) Debug.LogWarning("[AuthUIManager] resetPasswordPage GameObject was not found in the scene.");
        if (verifyPasswordButton == null) Debug.LogWarning("[AuthUIManager] verifyPasswordButton is null under VerifyPasswordPage.");
        if (verifyAccountButton == null) Debug.LogWarning("[AuthUIManager] verifyAccountButton is null under VerifyAccountPage.");
        if (resetPasswordSubmitButton == null) Debug.LogWarning("[AuthUIManager] resetPasswordSubmitButton is null under ResetPasswordPage.");
        logOutButton = FindButton(menuRoot, "LogOutButton");

        logInEmailInputField = FindInputField(logInPage, "EmailUserInputField");
        logInPasswordInputField = FindInputField(logInPage, "PasswordInputField");

        createAccountDisplayNameInputField = FindInputField(createAccountPage, "DisplayNameInputField");
        createAccountEmailInputField = FindInputField(createAccountPage, "EmailUserInputField");
        createAccountPasswordInputField = FindInputField(createAccountPage, "PasswordInputField");
        createAccountConfirmPasswordInputField = FindInputField(createAccountPage, "ConfirmPasswordInputField");

        forgotPasswordEmailInputField = FindInputField(forgotPasswordPage, "EmailUserInputField");
        verifyAccountOtpContainer = FindComponentInChildrenByName<RectTransform>(verifyAccountPage, "OTP_ContainerInput")?.gameObject;
        if (verifyAccountOtpContainer == null)
        {
            verifyAccountOtpContainer = FindComponentInChildrenByName<RectTransform>(verifyAccountPage, "OTP_Container")?.gameObject;
        }

        verifyPasswordOtpContainer = FindComponentInChildrenByName<RectTransform>(verifyPasswordPage, "OTP_ContainerInput")?.gameObject;
        if (verifyPasswordOtpContainer == null)
        {
            verifyPasswordOtpContainer = FindComponentInChildrenByName<RectTransform>(verifyPasswordPage, "OTP_Container")?.gameObject;
        }

        resetPasswordInputField = FindInputField(resetPasswordPage, "PasswordInputField");
        resetConfirmPasswordInputField = FindInputField(resetPasswordPage, "ConfirmPasswordInputField");
        resetPasswordNoteText = FindText(resetPasswordPage, "ResetPasswordLabel");
        resetPasswordStatusText = FindText(resetPasswordPage, "LogInValidation");

        authStatusText = FindText(logInPage, "AuthStatusText");
        createAccountStatusText = FindText(createAccountPage, "CreateAccountStatusText");
        forgotPasswordStatusText = FindText(forgotPasswordPage, "ForgotPasswordStatusText");

        verifyAccountStatusText = FindText(verifyAccountPage, "LoginValidation");
        if (verifyAccountStatusText == null) verifyAccountStatusText = FindText(verifyAccountPage, "LogInValidation");
        if (verifyAccountStatusText == null) verifyAccountStatusText = FindText(verifyAccountPage, "VerifyAccountStatusText");

        verifyPasswordStatusText = FindText(verifyPasswordPage, "LoginValidation");
        if (verifyPasswordStatusText == null) verifyPasswordStatusText = FindText(verifyPasswordPage, "LogInValidation");
        if (verifyPasswordStatusText == null) verifyPasswordStatusText = FindText(verifyPasswordPage, "VerifyAccountStatusText");

        verifyAccountTimerText = FindText(verifyAccountPage, "ResendTimer");
        verifyPasswordTimerText = FindText(verifyPasswordPage, "ResendTimer");

        verifyAccountNoteText = FindText(verifyAccountPage, "VerifyNoteLabel");
        verifyPasswordNoteText = FindText(verifyPasswordPage, "VerifyNoteLabel");
        verifyAccountTitleText = FindText(verifyAccountPage, "VerifyLabel");
        verifyPasswordTitleText = FindText(verifyPasswordPage, "VerifyLabel");
        authLoadingOverlay = FindObjectByName("AuthLoadingOverlay");
        loadingText = FindText(authLoadingOverlay, "LoadingText");

        backToLoginButtons.Clear();
        if (launchUiRoot != null)
        {
            backToLoginButtons.AddRange(FindButtons(launchUiRoot, "BacktoLogInButton"));
        }

        menuOpenButtons.Clear();
        if (solarSystemUiRoot != null)
        {
            menuOpenButtons.AddRange(FindButtons(solarSystemUiRoot, "MenuButton"));
        }

        menuBackButtons.Clear();
        if (menuRoot != null)
        {
            menuBackButtons.AddRange(FindButtons(menuRoot, "BackButton"));
        }

        quizHistoryButtons.Clear();
        quizHistoryButtons.AddRange(FindButtonsByNameGlobal("QuizHistoryButton"));

        // Resolve quiz pages from inside QuizUI first so duplicate names elsewhere do not break navigation.
        quizHomePage = FindChildObject(quizUiRoot, "QuizHomePage") ?? FindObjectByName("QuizHomePage");
        quizTopicPage = FindChildObject(quizUiRoot, "QuizTopicPage") ?? FindObjectByName("QuizTopicPage");
        quizQuestionPage = FindChildObject(quizUiRoot, "QuizQuestionPage") ?? FindObjectByName("QuizQuestionPage");
        quizResultPage = FindChildObject(quizUiRoot, "QuizResultPage") ?? FindObjectByName("QuizResultPage");
        quizHistoryPage = FindChildObject(quizUiRoot, "QuizHistoryPage") ?? FindObjectByName("QuizHistoryPage");
        quizIntroPage = FindChildObject(quizUiRoot, "QuizIntroPage") ?? FindObjectByName("QuizIntroPage");
        quizBreakdownPage = FindChildObject(quizUiRoot, "QuizBreakdownPage") ?? FindObjectByName("QuizBreakdownPage");

        if (quizHomePage == null) Debug.LogWarning("[AuthUIManager] QuizHomePage not found in scene.");
        quizUiRoot = FindObjectByName("QuizManager") ?? FindObjectByName("QuizUI");
        if (quizUiRoot == null)
        {
            Debug.LogWarning("[AuthUIManager] QuizUI not found in scene. Attempting to resolve via QuizTopicPage.");
            if (quizTopicPage != null && quizTopicPage.transform.parent != null)
            {
                quizUiRoot = quizTopicPage.transform.parent.gameObject;
                Debug.Log($"[AuthUIManager] Resolved QuizUI to {quizUiRoot.name}");
                
                quizUiContainerRoot = quizUiRoot != null && quizUiRoot.transform.parent != null
                    ? quizUiRoot.transform.parent.gameObject
                    : null;
            }
        }

        // Resolve quiz trigger and back buttons.
        // The top-right trigger has used multiple names in the scene, so prefer the local TopRightGroup match first.
        GameObject topRightGroup = FindObjectByName("TopRightGroup");
        quizButton = topRightGroup != null
            ? FindButton(topRightGroup, "QuizTopicButton") ?? FindButton(topRightGroup, "QuizButton")
            : null;
        askAiButton = topRightGroup != null
            ? FindButton(topRightGroup, "AskAIButton")
            : null;
        if (quizButton == null)
        {
            List<Button> quizButtons = FindButtonsByNameGlobal("QuizTopicButton");
            if (quizButtons.Count == 0)
            {
                quizButtons = FindButtonsByNameGlobal("QuizButton");
            }
            quizButton = quizButtons.Count > 0 ? quizButtons[0] : null;
        }
        if (askAiButton == null)
        {
            List<Button> askAiButtons = FindButtonsByNameGlobal("AskAIButton");
            askAiButton = askAiButtons.Count > 0 ? askAiButtons[0] : null;
        }
        quizBackButtons.Clear();
        Button quizTopicBackButton = FindButton(quizTopicPage, "BackButton");
        if (quizTopicBackButton != null)
        {
            quizBackButtons.Add(quizTopicBackButton);
        }
        quizHomeBackButton = FindButton(quizHomePage, "BackButton");
        if (quizHomeBackButton != null)
        {
            quizBackButtons.Add(quizHomeBackButton);
        }
        quizIntroBackButton = FindButton(quizIntroPage, "BackButton");

        quizIntroStartButton = FindButton(quizIntroPage, "StartQuizButton") ?? FindButtonByChildText(quizIntroPage, "START QUIZ");
        if (quizIntroStartButton != null)
        {
            quizHistoryButtons.Remove(quizIntroStartButton);
        }
        quizHistoryBackButton = FindButton(quizHistoryPage, "BackButton");
        if (quizHistoryBackButton != null)
        {
            quizBackButtons.Remove(quizHistoryBackButton);
        }

        if (quizButton == null) Debug.LogWarning("[AuthUIManager] Quiz trigger button was not found in TopRightGroup.");
        else Debug.Log($"[AuthUIManager] Quiz trigger bound to {GetHierarchyPath(quizButton.transform)}");
        aiExitButton = FindButton(aiUiRoot, "ExitButton") ?? FindButtonByChildText(aiUiRoot, "EXIT");
        if (askAiButton == null) Debug.LogWarning("[AuthUIManager] AskAI trigger button was not found in TopRightGroup.");
        if (aiUiRoot == null) Debug.LogWarning("[AuthUIManager] AI chat page was not found in scene.");

        setDisplayNamePage = FindObjectByName("SetDisplayNamePage");
        if (setDisplayNamePage != null)
        {
            displayNameInputField = FindInputField(setDisplayNamePage, "DisplayNameInputField");
            confirmIdentityButton = FindButton(setDisplayNamePage, "ConfirmIdentityButton");
            randomizeNameButton = FindButton(setDisplayNamePage, "Button");
            displayNameCharCounterText = FindText(setDisplayNamePage, "DisplayNameLabel1");
        }
    }

    private void RegisterListeners()
    {
        AddListener(landingStartButton, HandleLandingStartButton);
        AddListener(loginPageBackButton, HandleLoginPageBackButton);
        AddListener(logInButton, HandleLogInButton);
        AddListener(forgotPasswordButton, HandleForgotPasswordNavigation);
        AddListener(createAccountFromLoginButton, HandleCreateAccountNavigation);
        AddListener(guestModeButton, HandleGuestModeButton);

        AddListener(createAccountBackButton, ShowLoginPage);
        AddListener(createAccountSubmitButton, HandleCreateAccountSubmit);
        AddListener(createAccountLogInButton, ShowLoginPage);

        AddListener(forgotPasswordBackButton, ShowLoginPage);
        AddListener(sendPasswordButton, HandleSendPasswordButton);

        AddListener(verifyPasswordBackButton, ShowForgotPasswordPage);
        AddListener(verifyPasswordButton, HandleVerifyPasswordButton);
        AddListener(verifyPasswordResendCodeButton, ShowForgotPasswordPage);

        AddListener(resetPasswordBackButton, ShowForgotPasswordPage);
        AddListener(resetPasswordSubmitButton, HandleResetPasswordSubmit);

        AddListener(verifyAccountBackButton, ShowCreateAccountPage);
        AddListener(verifyAccountButton, HandleVerifyAccountButton);
        AddListener(verifyAccountResendCodeButton, HandleVerifyAccountResendButton);
        AddListener(logOutButton, HandleLogOutButton);
        AddListener(confirmIdentityButton, HandleConfirmIdentity);
        AddListener(randomizeNameButton, HandleRandomizeName);
        if (displayNameInputField != null)
        {
            displayNameInputField.onValueChanged.AddListener(HandleDisplayNameInputChanged);
        }

        foreach (Button button in backToLoginButtons)
        {
            AddListener(button, ShowLoginPage);
        }

        foreach (Button button in menuOpenButtons)
        {
            AddListener(button, ShowMenu);
        }

        foreach (Button button in menuBackButtons)
        {
            AddListener(button, ShowSolarSystemUiOnly);
        }

        AddListener(quizButton, ShowQuizUi);
        AddListener(askAiButton, ShowAiUi);
        foreach (Button button in quizBackButtons)
        {
            AddListener(button, HandleQuizBackNavigation);
        }
        foreach (Button button in quizHistoryButtons)
        {
            AddListener(button, ShowQuizHistoryPage);
        }
        AddListener(quizHistoryBackButton, ShowQuizUi);
        AddListener(aiExitButton, ShowSolarSystemUiOnly);
    }

    private void UnregisterListeners()
    {
        RemoveListener(landingStartButton, HandleLandingStartButton);
        RemoveListener(loginPageBackButton, HandleLoginPageBackButton);
        RemoveListener(logInButton, HandleLogInButton);
        RemoveListener(forgotPasswordButton, HandleForgotPasswordNavigation);
        RemoveListener(createAccountFromLoginButton, HandleCreateAccountNavigation);
        RemoveListener(guestModeButton, HandleGuestModeButton);

        RemoveListener(createAccountBackButton, ShowLoginPage);
        RemoveListener(createAccountSubmitButton, HandleCreateAccountSubmit);
        RemoveListener(createAccountLogInButton, ShowLoginPage);

        RemoveListener(forgotPasswordBackButton, ShowLoginPage);
        RemoveListener(sendPasswordButton, HandleSendPasswordButton);

        RemoveListener(verifyPasswordBackButton, ShowForgotPasswordPage);
        RemoveListener(verifyPasswordButton, HandleVerifyPasswordButton);
        RemoveListener(verifyPasswordResendCodeButton, ShowForgotPasswordPage);

        RemoveListener(resetPasswordBackButton, ShowForgotPasswordPage);
        RemoveListener(resetPasswordSubmitButton, HandleResetPasswordSubmit);

        RemoveListener(verifyAccountBackButton, ShowCreateAccountPage);
        RemoveListener(verifyAccountButton, HandleVerifyAccountButton);
        RemoveListener(verifyAccountResendCodeButton, HandleVerifyAccountResendButton);
        RemoveListener(logOutButton, HandleLogOutButton);
        RemoveListener(confirmIdentityButton, HandleConfirmIdentity);
        RemoveListener(randomizeNameButton, HandleRandomizeName);
        if (displayNameInputField != null)
        {
            displayNameInputField.onValueChanged.RemoveListener(HandleDisplayNameInputChanged);
        }

        foreach (Button button in backToLoginButtons)
        {
            RemoveListener(button, ShowLoginPage);
        }

        foreach (Button button in menuOpenButtons)
        {
            RemoveListener(button, ShowMenu);
        }

        foreach (Button button in menuBackButtons)
        {
            RemoveListener(button, ShowSolarSystemUiOnly);
        }

        RemoveListener(quizButton, ShowQuizUi);
        RemoveListener(askAiButton, ShowAiUi);
        foreach (Button button in quizBackButtons)
        {
            RemoveListener(button, HandleQuizBackNavigation);
        }
        foreach (Button button in quizHistoryButtons)
        {
            RemoveListener(button, ShowQuizHistoryPage);
        }
        RemoveListener(quizHistoryBackButton, ShowQuizUi);
        RemoveListener(aiExitButton, ShowSolarSystemUiOnly);
    }

    private void SetupOtpInputContainer(GameObject container)
    {
        if (container == null) return;

        List<TMP_InputField> inputFields = new List<TMP_InputField>();
        for (int i = 0; i < container.transform.childCount; i++)
        {
            TMP_InputField inputField = container.transform.GetChild(i).GetComponentInChildren<TMP_InputField>(true);
            if (inputField != null)
            {
                inputFields.Add(inputField);
            }
        }

        for (int i = 0; i < inputFields.Count; i++)
        {
            int index = i;
            TMP_InputField currentField = inputFields[index];

            currentField.characterLimit = 100;
            currentField.contentType = TMP_InputField.ContentType.IntegerNumber;
            currentField.characterValidation = TMP_InputField.CharacterValidation.Digit;

            currentField.onValueChanged.AddListener((text) =>
            {
                if (isClearingOtp) return;

                if (text.Length > 1)
                {
                    string clipboardText = GUIUtility.systemCopyBuffer;
                    string extractedCode = Extract8DigitCode(clipboardText);

                    // Fallback to the merged input field text if clipboard check failed
                    if (extractedCode == null)
                    {
                        extractedCode = Extract8DigitCode(text);
                    }

                    if (extractedCode != null)
                    {
                        isClearingOtp = true;
                        for (int k = 0; k < inputFields.Count && k < 8; k++)
                        {
                            inputFields[k].text = extractedCode[k].ToString();
                        }
                        isClearingOtp = false;

                        if (inputFields.Count > 0)
                        {
                            inputFields[Mathf.Min(7, inputFields.Count - 1)].Select();
                            inputFields[Mathf.Min(7, inputFields.Count - 1)].ActivateInputField();
                        }
                    }
                    else
                    {
                        isClearingOtp = true;
                        char lastChar = text[text.Length - 1];
                        currentField.text = char.IsDigit(lastChar) ? lastChar.ToString() : string.Empty;
                        isClearingOtp = false;

                        if (currentField.text.Length == 1 && index + 1 < inputFields.Count)
                        {
                            inputFields[index + 1].Select();
                            inputFields[index + 1].ActivateInputField();
                        }
                    }
                }
                else if (text.Length == 1)
                {
                    if (index + 1 < inputFields.Count)
                    {
                        inputFields[index + 1].Select();
                        inputFields[index + 1].ActivateInputField();
                    }
                }
                else if (text.Length == 0)
                {
                    if (index - 1 >= 0)
                    {
                        inputFields[index - 1].Select();
                        inputFields[index - 1].ActivateInputField();
                    }
                }
            });
        }
    }

    private void ClearOtpContainer(GameObject container)
    {
        if (container == null) return;

        isClearingOtp = true;
        for (int i = 0; i < container.transform.childCount; i++)
        {
            Transform child = container.transform.GetChild(i);
            TMP_InputField inputField = child.GetComponentInChildren<TMP_InputField>(true);
            if (inputField != null)
            {
                inputField.text = string.Empty;
            }
        }
        isClearingOtp = false;
    }

    private IEnumerator OtpTimerRoutine(float durationSeconds, TMP_Text timerText, Button resendButton)
    {
        if (resendButton != null)
        {
            resendButton.interactable = false;
        }

        float timeRemaining = durationSeconds;
        while (timeRemaining > 0)
        {
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);

            if (timerText != null)
            {
                timerText.text = string.Format("Resend code in {0:00}:{1:00}", minutes, seconds);
                timerText.gameObject.SetActive(true);
            }

            yield return new WaitForSecondsRealtime(1f);
            timeRemaining -= 1f;
        }

        if (timerText != null)
        {
            timerText.text = "You can now resend the code.";
        }

        if (resendButton != null)
        {
            resendButton.interactable = true;
        }
    }

    private void SetupPasswordToggle(TMP_InputField inputField)
    {
        if (inputField == null) return;

        Button toggleButton = inputField.GetComponentInChildren<Button>(true);
        if (toggleButton == null) return;

        Image toggleImage = toggleButton.GetComponent<Image>();
        if (toggleImage == null) return;

#if UNITY_EDITOR
        if (showPasswordIcon == null)
        {
            showPasswordIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Icons/view.png");
        }
        if (hidePasswordIcon == null)
        {
            hidePasswordIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Icons/hide.png");
        }
#endif

        if (showPasswordIcon == null || hidePasswordIcon == null)
        {
            Debug.LogWarning($"[AuthUIManager] Password visibility sprites are not fully assigned for {inputField.name} password toggle.");
        }

        // Set to password type initially (hidden characters)
        inputField.contentType = TMP_InputField.ContentType.Password;
        if (hidePasswordIcon != null)
        {
            toggleImage.sprite = hidePasswordIcon; // Show hide icon first (slashed eye)
        }

        // Initially hide the toggle button if the input field is empty
        toggleButton.gameObject.SetActive(!string.IsNullOrEmpty(inputField.text));

        // Automatically show/hide the toggle button as the user types
        inputField.onValueChanged.AddListener((text) =>
        {
            toggleButton.gameObject.SetActive(!string.IsNullOrEmpty(text));
        });

        // Set up toggle onClick action
        toggleButton.onClick.AddListener(() =>
        {
            if (inputField.contentType == TMP_InputField.ContentType.Password)
            {
                // Switch to standard (visible characters)
                inputField.contentType = TMP_InputField.ContentType.Standard;
                inputField.inputType = TMP_InputField.InputType.Standard;
                if (showPasswordIcon != null)
                {
                    toggleImage.sprite = showPasswordIcon; // Show view icon (open eye) when visible
                }
            }
            else
            {
                // Switch back to password (hidden characters)
                inputField.contentType = TMP_InputField.ContentType.Password;
                inputField.inputType = TMP_InputField.InputType.Password;
                if (hidePasswordIcon != null)
                {
                    toggleImage.sprite = hidePasswordIcon; // Show hide icon (slashed eye) when hidden
                }
            }
            inputField.ForceLabelUpdate();
        });
    }

    private void InitializeNavigationState()
    {
        if (launchUiRoot == null)
        {
            return;
        }

        if (splashTransitionCoroutine != null)
        {
            StopCoroutine(splashTransitionCoroutine);
            splashTransitionCoroutine = null;
        }

        launchUiRoot.SetActive(true);
        SetMainApplicationVisible(false);
        SetPageActive(menuRoot, false);
        SetLoadingState(false);
        ClearStatusTexts();

        if (splashPage != null)
        {
            ShowLaunchPage(splashPage);
            splashTransitionCoroutine = StartCoroutine(ShowLandingPageAfterDelay());
            return;
        }

        if (landingPage != null)
        {
            ShowLandingPage();
            return;
        }

        ShowLoginPage();
    }

    private IEnumerator ShowLandingPageAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, splashPageDuration));
        splashTransitionCoroutine = null;

        if (landingPage != null)
        {
            ShowLandingPage();
        }
        else
        {
            ShowLoginPage();
        }
    }

    private void ShowLandingPage()
    {
        if (landingPage != null)
        {
            ShowLaunchPage(landingPage);
            return;
        }

        ShowLoginPage();
    }


    private void ShowLoginPage()
    {
        string email = logInEmailInputField != null ? logInEmailInputField.text.Trim() : string.Empty;
        if (IsLockedOut(email, out string lockoutMessage))
        {
            SetStatus(authStatusText, lockoutMessage);
            StartLockoutTimer(email);
        }
        ShowLaunchPage(logInPage);
    }

    private void ShowCreateAccountPage()
    {
        ShowLaunchPage(createAccountPage);
    }

    private void ShowForgotPasswordPage()
    {
        ShowLaunchPage(forgotPasswordPage);
    }

    private void ShowVerifyPasswordPage()
    {
        ClearOtpContainer(verifyPasswordOtpContainer);

        string email = SupabaseAuthService.Instance != null ? SupabaseAuthService.Instance.PendingEmail : string.Empty;
        if (string.IsNullOrEmpty(email))
        {
            email = forgotPasswordEmailInputField != null ? forgotPasswordEmailInputField.text.Trim() : "your email";
        }

        if (verifyPasswordTitleText != null)
        {
            verifyPasswordTitleText.text = "VERIFY CODE";
            verifyPasswordTitleText.fontSize = 80f;
        }

        if (verifyPasswordButton != null)
        {
            TMP_Text btnText = verifyPasswordButton.GetComponentInChildren<TMP_Text>();
            if (btnText != null)
            {
                btnText.text = "VERIFY CODE";
            }
        }

        if (verifyPasswordNoteText != null)
        {
            verifyPasswordNoteText.text = $"We sent a code to your account\n{email}. Please enter the 8-digit code.";
        }

        if (verifyPasswordTimerCoroutine != null)
        {
            StopCoroutine(verifyPasswordTimerCoroutine);
        }
        verifyPasswordTimerCoroutine = StartCoroutine(OtpTimerRoutine(600f, verifyPasswordTimerText, verifyPasswordResendCodeButton));

        ShowLaunchPage(verifyPasswordPage);
    }

    private void ShowResetPasswordPage()
    {
        string email = SupabaseAuthService.Instance != null ? SupabaseAuthService.Instance.PendingEmail : string.Empty;
        if (string.IsNullOrEmpty(email))
        {
            email = forgotPasswordEmailInputField != null ? forgotPasswordEmailInputField.text.Trim() : "your email";
        }

        if (resetPasswordNoteText != null)
        {
            resetPasswordNoteText.text = $"Reset your password for {email}";
        }

        ShowLaunchPage(resetPasswordPage);
    }

    private void ShowVerifyAccountPage()
    {
        ClearOtpContainer(verifyAccountOtpContainer);

        string email = SupabaseAuthService.Instance != null ? SupabaseAuthService.Instance.PendingEmail : string.Empty;
        if (string.IsNullOrEmpty(email))
        {
            email = createAccountEmailInputField != null ? createAccountEmailInputField.text.Trim() : "your email";
        }

        if (verifyAccountTitleText != null)
        {
            verifyAccountTitleText.text = "VERIFY ACCOUNT";
            verifyAccountTitleText.fontSize = 80f;
        }

        if (verifyAccountButton != null)
        {
            TMP_Text btnText = verifyAccountButton.GetComponentInChildren<TMP_Text>();
            if (btnText != null)
            {
                btnText.text = "VERIFY";
            }
        }

        if (verifyAccountNoteText != null)
        {
            verifyAccountNoteText.text = $"We sent a code to your account\n{email}. Please enter the 8-digit code.";
        }

        if (verifyAccountTimerCoroutine != null)
        {
            StopCoroutine(verifyAccountTimerCoroutine);
        }
        verifyAccountTimerCoroutine = StartCoroutine(OtpTimerRoutine(600f, verifyAccountTimerText, verifyAccountResendCodeButton));

        ShowLaunchPage(verifyAccountPage);
    }

    private void ShowLaunchPage(GameObject targetPage)
    {
        if (launchUiRoot == null)
        {
            OpenMainApplication();
            return;
        }

        if (targetPage == null)
        {
            OpenMainApplication();
            return;
        }

        if (splashTransitionCoroutine != null && targetPage != splashPage)
        {
            StopCoroutine(splashTransitionCoroutine);
            splashTransitionCoroutine = null;
        }

        launchUiRoot.SetActive(true);
        SetMainApplicationVisible(false);
        SetPageActive(menuRoot, false);
        SetLoadingState(false);

        // Clear all input fields and status texts when switching pages
        ClearAllInputFields();

        SetPageActive(splashPage, targetPage == splashPage);
        SetPageActive(landingPage, targetPage == landingPage);
        SetPageActive(logInPage, targetPage == logInPage);
        SetPageActive(createAccountPage, targetPage == createAccountPage);
        SetPageActive(forgotPasswordPage, targetPage == forgotPasswordPage);
        SetPageActive(verifyPasswordPage, targetPage == verifyPasswordPage);
        SetPageActive(resetPasswordPage, targetPage == resetPasswordPage);
        SetPageActive(verifyAccountPage, targetPage == verifyAccountPage);
        SetPageActive(setDisplayNamePage, targetPage == setDisplayNamePage);
    }

    private void ClearAllInputFields()
    {
        // Login page
        if (logInEmailInputField != null) logInEmailInputField.text = string.Empty;
        if (logInPasswordInputField != null) logInPasswordInputField.text = string.Empty;

        // Create account page
        if (createAccountDisplayNameInputField != null) createAccountDisplayNameInputField.text = string.Empty;
        if (createAccountEmailInputField != null) createAccountEmailInputField.text = string.Empty;
        if (createAccountPasswordInputField != null) createAccountPasswordInputField.text = string.Empty;
        if (createAccountConfirmPasswordInputField != null) createAccountConfirmPasswordInputField.text = string.Empty;

        // Forgot password page
        if (forgotPasswordEmailInputField != null) forgotPasswordEmailInputField.text = string.Empty;

        // Reset password page
        if (resetPasswordInputField != null) resetPasswordInputField.text = string.Empty;
        if (resetConfirmPasswordInputField != null) resetConfirmPasswordInputField.text = string.Empty;

        // Status texts
        if (authStatusText != null) authStatusText.text = string.Empty;
        if (createAccountStatusText != null) createAccountStatusText.text = string.Empty;
        if (forgotPasswordStatusText != null) forgotPasswordStatusText.text = string.Empty;
        if (verifyAccountStatusText != null) verifyAccountStatusText.text = string.Empty;
        if (verifyPasswordStatusText != null) verifyPasswordStatusText.text = string.Empty;
        if (resetPasswordStatusText != null) resetPasswordStatusText.text = string.Empty;
    }

    private void OpenMainApplication()
    {
        if (splashTransitionCoroutine != null)
        {
            StopCoroutine(splashTransitionCoroutine);
            splashTransitionCoroutine = null;
        }

        SetPageActive(splashPage, false);
        SetPageActive(landingPage, false);
        SetPageActive(logInPage, false);
        SetPageActive(createAccountPage, false);
        SetPageActive(forgotPasswordPage, false);
        SetPageActive(verifyPasswordPage, false);
        SetPageActive(resetPasswordPage, false);
        SetPageActive(verifyAccountPage, false);
        SetPageActive(setDisplayNamePage, false);

        if (launchUiRoot != null)
        {
            launchUiRoot.SetActive(false);
        }
        SetPageActive(menuRoot, false);

        if (planetInfoCard != null)
        {
            planetInfoCard.SetActive(false);
        }

        if (celestialBodyUiRoot != null)
        {
            celestialBodyUiRoot.SetActive(false);
        }

        if (imagesGalleryOverlay != null)
        {
            imagesGalleryOverlay.SetActive(false);
        }

        if (imageViewerOverlay != null)
        {
            imageViewerOverlay.SetActive(false);
        }

        ClearStatusTexts();
        SetMainApplicationVisible(true);
        ApplyHistoryAccessState();
    }

    private void SetMainApplicationVisible(bool isVisible)
    {
        if (solarSystemUiRoot != null)
        {
            solarSystemUiRoot.SetActive(isVisible);
        }

        if (solarSystemRoot != null)
        {
            solarSystemRoot.SetActive(isVisible);
        }

        if (!isVisible && celestialBodyUiRoot != null)
        {
            celestialBodyUiRoot.SetActive(false);
        }

        if (quizUiRoot != null)
        {
            quizUiRoot.SetActive(false);
        }

        if (quizUiContainerRoot != null && quizUiContainerRoot != quizUiRoot)
        {
            quizUiContainerRoot.SetActive(false);
        }

        if (aiUiRoot != null)
        {
            aiUiRoot.SetActive(false);
        }

        if (arUiRoot != null)
        {
            arUiRoot.SetActive(false);
        }

        if (!isVisible)
        {
            SetPageActive(menuRoot, false);
        }
    }

    private void HandleLandingStartButton()
    {
        ShowLoginPage();
    }

    private void HandleLoginPageBackButton()
    {
        if (landingPage != null)
        {
            ShowLandingPage();
            return;
        }

        ShowLaunchPage(splashPage != null ? splashPage : logInPage);
    }

    private void HandleLogInButton()
    {
        StartCoroutine(LogInRoutine());
    }

    private void HandleForgotPasswordNavigation()
    {
        ShowForgotPasswordPage();
    }

    private void HandleCreateAccountNavigation()
    {
        ShowCreateAccountPage();
    }

    private void HandleCreateAccountSubmit()
    {
        StartCoroutine(CreateAccountRoutine());
    }

    private void HandleSendPasswordButton()
    {
        StartCoroutine(ForgotPasswordRoutine());
    }

    private void HandleVerifyPasswordButton()
    {
        StartCoroutine(VerifyPasswordCodeRoutine());
    }

    private void HandleResetPasswordSubmit()
    {
        StartCoroutine(ResetPasswordRoutine());
    }

    private void HandleResetPasswordButton()
    {
        ShowLoginPage();
    }

    private void HandleVerifyAccountButton()
    {
        StartCoroutine(VerifyAccountRoutine());
    }

    private void HandleVerifyAccountResendButton()
    {
        StartCoroutine(ResendVerificationRoutine());
    }

    private void HandleGuestModeButton()
    {
        if (SupabaseAuthService.Instance != null)
        {
            SupabaseAuthService.Instance.SignOut();
            SupabaseAuthService.Instance.SetGuestMode(true);
        }

        OpenMainApplication();
    }

    private void HandleLogOutButton()
    {
        if (SupabaseAuthService.Instance != null)
        {
            SupabaseAuthService.Instance.SignOut();
        }

        ApplyHistoryAccessState();
        ShowLoginPage();
    }

    private IEnumerator TryRestoreSessionRoutine()
    {
        if (SupabaseAuthService.Instance == null)
        {
            yield break;
        }

        SetLoadingState(true, "Restoring session...");
        yield return SupabaseAuthService.Instance.TryRestoreSession(result =>
        {
            SetLoadingState(false);

            if (!result.Success)
            {
                ApplyHistoryAccessState();
                return;
            }

            if (result.EmailVerified)
            {
                string storedName = SupabaseAuthService.Instance.CurrentUserDisplayName;
                if (string.IsNullOrWhiteSpace(storedName) || storedName.Equals("EMPTY", System.StringComparison.OrdinalIgnoreCase))
                {
                    ShowSetDisplayNamePage();
                }
                else
                {
                    OpenMainApplication();
                }
                return;
            }

            SetStatus(verifyAccountStatusText, "Verify your email before continuing.");
            ShowVerifyAccountPage();
        });
    }

    private IEnumerator LogInRoutine()
    {
        ClearStatusTexts();

        string email = logInEmailInputField != null ? logInEmailInputField.text.Trim() : string.Empty;
        string password = logInPasswordInputField != null ? logInPasswordInputField.text : string.Empty;

        if (!IsValidEmail(email))
        {
            SetStatus(authStatusText, "Enter a valid email address.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            SetStatus(authStatusText, "Enter your password.");
            yield break;
        }

        if (SupabaseAuthService.Instance == null)
        {
            SetStatus(authStatusText, "Supabase auth service is not available.");
            yield break;
        }

        if (IsLockedOut(email, out string lockoutMessage))
        {
            SetStatus(authStatusText, lockoutMessage);
            StartLockoutTimer(email);
            yield break;
        }

        SetLoadingState(true, "Signing in...");
        yield return SupabaseAuthService.Instance.SignIn(email, password, result =>
        {
            SetLoadingState(false);

            if (!result.Success)
            {
                IncrementFailedLoginCount(email);
                if (IsLockedOut(email, out string postFailedLockoutMessage))
                {
                    SetStatus(authStatusText, postFailedLockoutMessage);
                    StartLockoutTimer(email);
                }
                else
                {
                    bool isUnconfirmed = result.Message != null && 
                        (result.Message.IndexOf("email not confirmed", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         result.Message.IndexOf("email not verified", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                         result.Message.IndexOf("confirmation required", System.StringComparison.OrdinalIgnoreCase) >= 0);

                    if (isUnconfirmed)
                    {
                        SupabaseAuthService.Instance.SavePendingEmail(email);
                        SetStatus(verifyAccountStatusText, "Verify your email before continuing.");
                        ShowVerifyAccountPage();
                    }
                    else
                    {
                        SetStatus(authStatusText, result.Message);
                    }
                }
                ApplyHistoryAccessState();
                return;
            }

            ResetFailedLoginCount(email);
            ApplyHistoryAccessState();

            if (!result.EmailVerified)
            {
                SetStatus(verifyAccountStatusText, "Your email is not verified yet. Check your inbox and try again.");
                ShowVerifyAccountPage();
                return;
            }

            string storedName = SupabaseAuthService.Instance.CurrentUserDisplayName;
            if (string.IsNullOrWhiteSpace(storedName) || storedName.Equals("EMPTY", System.StringComparison.OrdinalIgnoreCase))
            {
                ShowSetDisplayNamePage();
            }
            else
            {
                OpenMainApplication();
            }
        });
    }

    private IEnumerator CreateAccountRoutine()
    {
        ClearStatusTexts();

        string displayName = createAccountDisplayNameInputField != null ? createAccountDisplayNameInputField.text.Trim() : string.Empty;
        string email = createAccountEmailInputField != null ? createAccountEmailInputField.text.Trim() : string.Empty;
        string password = createAccountPasswordInputField != null ? createAccountPasswordInputField.text : string.Empty;
        string confirmPassword = createAccountConfirmPasswordInputField != null ? createAccountConfirmPasswordInputField.text : string.Empty;

        if (!IsValidEmail(email))
        {
            SetStatus(createAccountStatusText, "Enter a valid email address.");
            yield break;
        }

        if (!string.Equals(password, confirmPassword))
        {
            SetStatus(createAccountStatusText, "Passwords do not match.");
            yield break;
        }

        if (!IsStrongPassword(password))
        {
            SetStatus(createAccountStatusText, "Password must be at least 8 characters and include uppercase, lowercase, and a number.");
            yield break;
        }

        if (SupabaseAuthService.Instance == null)
        {
            SetStatus(createAccountStatusText, "Supabase auth service is not available.");
            yield break;
        }

        SetLoadingState(true, "Creating account...");
        yield return SupabaseAuthService.Instance.SignUp(email, password, displayName, result =>
        {
            SetLoadingState(false);

            if (!result.Success)
            {
                SetStatus(createAccountStatusText, result.Message);
                return;
            }

            if (result.HasSession && result.EmailVerified)
            {
                string storedName = SupabaseAuthService.Instance.CurrentUserDisplayName;
                if (string.IsNullOrWhiteSpace(storedName) || storedName.Equals("EMPTY", System.StringComparison.OrdinalIgnoreCase))
                {
                    ShowSetDisplayNamePage();
                }
                else
                {
                    OpenMainApplication();
                }
            }
            else
            {
                ShowVerifyAccountPage();
                SetStatus(verifyAccountStatusText, result.Message, true);
            }
        });
    }

    private IEnumerator ForgotPasswordRoutine()
    {
        ClearStatusTexts();

        string email = forgotPasswordEmailInputField != null ? forgotPasswordEmailInputField.text.Trim() : string.Empty;
        if (!IsValidEmail(email))
        {
            SetStatus(forgotPasswordStatusText, "Enter a valid email address.");
            yield break;
        }

        if (SupabaseAuthService.Instance == null)
        {
            SetStatus(forgotPasswordStatusText, "Supabase auth service is not available.");
            yield break;
        }

        SetLoadingState(true, "Sending reset email...");
        yield return SupabaseAuthService.Instance.SendPasswordReset(email, result =>
        {
            SetLoadingState(false);

            if (result.Success)
            {
                SupabaseAuthService.Instance.SavePendingEmail(email);
                ShowVerifyPasswordPage();
            }
            else
            {
                SetStatus(forgotPasswordStatusText, result.Message);
            }
        });
    }

    private string GetOTPCodeFromContainer(GameObject container)
    {
        if (container == null)
        {
            return string.Empty;
        }

        string code = string.Empty;
        for (int i = 0; i < container.transform.childCount; i++)
        {
            Transform child = container.transform.GetChild(i);
            TMP_InputField inputField = child.GetComponentInChildren<TMP_InputField>(true);
            if (inputField != null)
            {
                code += inputField.text.Trim();
            }
        }

        return code;
    }

    private IEnumerator VerifyAccountRoutine()
    {
        ClearStatusTexts();

        if (SupabaseAuthService.Instance == null)
        {
            SetStatus(verifyAccountStatusText, "Supabase auth service is not available.");
            yield break;
        }

        string email = SupabaseAuthService.Instance.PendingEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            email = logInEmailInputField != null ? logInEmailInputField.text.Trim() : string.Empty;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            SetStatus(verifyAccountStatusText, "No pending email verification session found. Please log in again.");
            yield break;
        }

        string code = GetOTPCodeFromContainer(verifyAccountOtpContainer);
        Debug.Log($"[AuthUIManager] VerifyAccountRoutine - email: '{email}', code length: {code.Length}, code: '{code}'");
        if (string.IsNullOrWhiteSpace(code) || code.Length < 8)
        {
            SetStatus(verifyAccountStatusText, "Please enter the complete 8-digit verification code sent to your email.");
            yield break;
        }

        SetLoadingState(true, "Verifying code...");
        yield return SupabaseAuthService.Instance.VerifyOTP(email, code, "signup", result =>
        {
            SetLoadingState(false);

            if (result.Success && result.EmailVerified)
            {
                if (SupabaseAuthService.Instance != null)
                {
                    SupabaseAuthService.Instance.SignOut();
                }
                SetStatus(authStatusText, "Account verified successfully. Please log in.", true);
                ShowLoginPage();
                return;
            }

            SetStatus(verifyAccountStatusText, result.Message);
        });
    }

    private IEnumerator VerifyPasswordCodeRoutine()
    {
        ClearStatusTexts();

        if (SupabaseAuthService.Instance == null)
        {
            SetStatus(verifyPasswordStatusText, "Supabase auth service is not available.");
            yield break;
        }

        string email = SupabaseAuthService.Instance.PendingEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            email = forgotPasswordEmailInputField != null ? forgotPasswordEmailInputField.text.Trim() : string.Empty;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            SetStatus(verifyPasswordStatusText, "No pending password reset request found. Please request again.");
            ShowForgotPasswordPage();
            yield break;
        }

        string code = GetOTPCodeFromContainer(verifyPasswordOtpContainer);
        Debug.Log($"[AuthUIManager] VerifyPasswordCodeRoutine - email: '{email}', code length: {code.Length}, code: '{code}'");
        if (string.IsNullOrWhiteSpace(code) || code.Length < 8)
        {
            SetStatus(verifyPasswordStatusText, "Please enter the complete 8-digit reset code sent to your email.");
            yield break;
        }

        SetLoadingState(true, "Verifying reset code...");
        yield return SupabaseAuthService.Instance.VerifyOTP(email, code, "recovery", result =>
        {
            SetLoadingState(false);

            if (result.Success)
            {
                ShowResetPasswordPage();
            }
            else
            {
                SetStatus(verifyPasswordStatusText, result.Message);
            }
        });
    }

    private IEnumerator ResetPasswordRoutine()
    {
        ClearStatusTexts();

        string password = resetPasswordInputField != null ? resetPasswordInputField.text : string.Empty;
        string confirmPassword = resetConfirmPasswordInputField != null ? resetConfirmPasswordInputField.text : string.Empty;

        if (!string.Equals(password, confirmPassword))
        {
            SetStatus(resetPasswordStatusText, "Passwords do not match.");
            yield break;
        }

        if (!IsStrongPassword(password))
        {
            SetStatus(resetPasswordStatusText, "Password must be at least 8 characters and include uppercase, lowercase, and a number.");
            yield break;
        }

        if (SupabaseAuthService.Instance == null)
        {
            SetStatus(resetPasswordStatusText, "Supabase auth service is not available.");
            yield break;
        }

        SetLoadingState(true, "Updating password...");
        yield return SupabaseAuthService.Instance.UpdatePassword(password, result =>
        {
            SetLoadingState(false);

            if (result.Success)
            {
                if (resetPasswordInputField != null) resetPasswordInputField.text = string.Empty;
                if (resetConfirmPasswordInputField != null) resetConfirmPasswordInputField.text = string.Empty;

                SetStatus(authStatusText, "Password reset successfully. Please log in with your new password.", true);
                ShowLoginPage();
            }
            else
            {
                SetStatus(resetPasswordStatusText, result.Message);
            }
        });
    }

    private IEnumerator ResendVerificationRoutine()
    {
        ClearStatusTexts();

        if (SupabaseAuthService.Instance == null)
        {
            SetStatus(verifyAccountStatusText, "Supabase auth service is not available.");
            yield break;
        }

        string email = SupabaseAuthService.Instance.PendingEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            email = logInEmailInputField != null ? logInEmailInputField.text.Trim() : string.Empty;
        }

        if (!IsValidEmail(email))
        {
            SetStatus(verifyAccountStatusText, "No valid email is available to resend verification.");
            yield break;
        }

        SetLoadingState(true, "Resending verification...");
        yield return SupabaseAuthService.Instance.ResendVerification(email, result =>
        {
            SetLoadingState(false);
            SetStatus(verifyAccountStatusText, result.Message, result.Success);

            if (result.Success)
            {
                if (verifyAccountTimerCoroutine != null)
                {
                    StopCoroutine(verifyAccountTimerCoroutine);
                }
                verifyAccountTimerCoroutine = StartCoroutine(OtpTimerRoutine(600f, verifyAccountTimerText, verifyAccountResendCodeButton));
            }
        });
    }


    private void ShowMenu()
    {
        if (launchUiRoot != null)
        {
            launchUiRoot.SetActive(false);
        }

        SetMainApplicationVisible(false);
        SetPageActive(menuRoot, true);
    }

    public void OpenMenuFromButton()
    {
        ShowMenu();
    }

    private void ShowSolarSystemUiOnly()
    {
        SetPageActive(menuRoot, false);

        if (launchUiRoot != null)
        {
            launchUiRoot.SetActive(false);
        }

        if (celestialBodyUiRoot != null)
        {
            celestialBodyUiRoot.SetActive(false);
        }

        if (planetInfoCard != null)
        {
            planetInfoCard.SetActive(false);
        }

        if (imagesGalleryOverlay != null)
        {
            imagesGalleryOverlay.SetActive(false);
        }

        if (imageViewerOverlay != null)
        {
            imageViewerOverlay.SetActive(false);
        }

        if (quizUiRoot != null)
        {
            quizUiRoot.SetActive(false);
        }

        if (quizUiContainerRoot != null && quizUiContainerRoot != quizUiRoot)
        {
            quizUiContainerRoot.SetActive(false);
        }

        if (aiUiRoot != null)
        {
            aiUiRoot.SetActive(false);
        }

        if (arUiRoot != null)
        {
            arUiRoot.SetActive(false);
        }

        if (solarSystemUiRoot != null)
        {
            solarSystemUiRoot.SetActive(true);
        }

        if (solarSystemRoot != null)
        {
            solarSystemRoot.SetActive(true);
        }
    }

    public void ForceShowSolarSystemUiOnly()
    {
        RefreshSceneBindings();
        OpenMainApplication();
        SetPageActive(menuRoot, false);

        if (launchUiRoot != null)
        {
            launchUiRoot.SetActive(false);
        }

        if (celestialBodyUiRoot != null)
        {
            celestialBodyUiRoot.SetActive(false);
        }

        if (planetInfoCard != null)
        {
            planetInfoCard.SetActive(false);
        }

        if (imagesGalleryOverlay != null)
        {
            imagesGalleryOverlay.SetActive(false);
        }

        if (imageViewerOverlay != null)
        {
            imageViewerOverlay.SetActive(false);
        }

        if (quizUiRoot != null)
        {
            quizUiRoot.SetActive(false);
        }

        if (quizUiContainerRoot != null && quizUiContainerRoot != quizUiRoot)
        {
            quizUiContainerRoot.SetActive(false);
        }

        if (aiUiRoot != null)
        {
            aiUiRoot.SetActive(false);
        }

        if (arUiRoot != null)
        {
            arUiRoot.SetActive(false);
        }

        if (solarSystemUiRoot != null)
        {
            solarSystemUiRoot.SetActive(true);
        }

        if (solarSystemRoot != null)
        {
            solarSystemRoot.SetActive(true);
        }

        GameObject loadingPanel = FindObjectByName("ARLoadingPanel");
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        ApplyHistoryAccessState();
    }

    public void RefreshSceneBindings()
    {
        UnregisterListeners();
        ResolveSceneReferences();
        RegisterListeners();
        SolarSystemSceneButtonAutoBinder.BindSceneButtons();
    }

    private void ShowQuizUi()
    {
        Debug.Log($"[AuthUIManager] ShowQuizUi invoked. quizUiRoot={(quizUiRoot != null ? quizUiRoot.activeInHierarchy.ToString() : "null")}, quizTopicPage={(quizTopicPage != null ? quizTopicPage.activeSelf.ToString() : "null")}, quizUiContainerRoot={(quizUiContainerRoot != null ? quizUiContainerRoot.activeInHierarchy.ToString() : "null")}");

        quizReturnTarget = QuizReturnTarget.SolarSystem;
        PrepareQuizUiShell();

        QuizTopicGenerator topicGenerator = FindQuizTopicGeneratorAnyState();
        if (topicGenerator != null)
        {
            topicGenerator.RefreshForOpen();
            return;
        }

        // Set QuizTopicPage active and deactivate all other pages under QuizUI
        SetPageActive(quizTopicPage, true);
        SetPageActive(quizHomePage, false);
        SetPageActive(quizQuestionPage, false);
        SetPageActive(quizResultPage, false);
        SetPageActive(quizHistoryPage, false);
        SetPageActive(quizIntroPage, false);
        SetPageActive(quizBreakdownPage, false);
    }

    public void OpenQuizHomeForTopic(string topicName)
    {
        quizReturnTarget = QuizReturnTarget.FocusedCelestialBody;
        PrepareQuizUiShell();

        QuizTopicGenerator topicGenerator = FindQuizTopicGeneratorAnyState();
        if (topicGenerator != null)
        {
            topicGenerator.OpenQuizHomeForTopic(topicName);
            return;
        }

        SetPageActive(quizTopicPage, false);
        SetPageActive(quizHomePage, true);
        SetPageActive(quizQuestionPage, false);
        SetPageActive(quizResultPage, false);
        SetPageActive(quizHistoryPage, false);
        SetPageActive(quizIntroPage, false);
        SetPageActive(quizBreakdownPage, false);
    }

    private void PrepareQuizUiShell()
    {
        SetPageActive(menuRoot, false);
        if (launchUiRoot != null) launchUiRoot.SetActive(false);
        if (solarSystemUiRoot != null) solarSystemUiRoot.SetActive(false);
        if (celestialBodyUiRoot != null) celestialBodyUiRoot.SetActive(false);
        if (planetInfoCard != null) planetInfoCard.SetActive(false);
        if (imagesGalleryOverlay != null) imagesGalleryOverlay.SetActive(false);
        if (imageViewerOverlay != null) imageViewerOverlay.SetActive(false);
        if (aiUiRoot != null) aiUiRoot.SetActive(false);
        if (arUiRoot != null) arUiRoot.SetActive(false);

        // Disable the main 3D universe visual
        if (solarSystemRoot != null)
        {
            solarSystemRoot.SetActive(false);
        }

        if (quizUiContainerRoot != null && quizUiContainerRoot != quizUiRoot)
        {
            quizUiContainerRoot.SetActive(true);
        }

        if (quizUiRoot != null)
        {
            quizUiRoot.SetActive(true);
        }

        if (QuizFlowController.Instance == null && FindFirstObjectByType<QuizFlowController>() == null)
        {
            GameObject controllerObject = new GameObject("QuizFlowController");
            controllerObject.AddComponent<QuizFlowController>();
        }
    }

    public void OpenQuizFromButton()
    {
        ShowQuizUi();
    }

    public bool IsFocusedCelestialBodyQuizFlowActive()
    {
        return quizReturnTarget == QuizReturnTarget.FocusedCelestialBody;
    }

    public bool TryRestoreFocusedCelestialBodyFromQuiz()
    {
        if (quizReturnTarget != QuizReturnTarget.FocusedCelestialBody)
        {
            return false;
        }

        return RestoreFocusedCelestialBodyAfterQuiz();
    }

    private static QuizTopicGenerator FindQuizTopicGeneratorAnyState()
    {
        QuizTopicGenerator activeGenerator = FindFirstObjectByType<QuizTopicGenerator>();
        if (activeGenerator != null)
        {
            return activeGenerator;
        }

        QuizTopicGenerator[] generators = Resources.FindObjectsOfTypeAll<QuizTopicGenerator>();
        for (int i = 0; i < generators.Length; i++)
        {
            QuizTopicGenerator generator = generators[i];
            if (generator != null && generator.gameObject.scene.IsValid())
            {
                return generator;
            }
        }

        return null;
    }

    private void HandleQuizBackNavigation()
    {
        if (quizReturnTarget == QuizReturnTarget.FocusedCelestialBody && RestoreFocusedCelestialBodyAfterQuiz())
        {
            return;
        }

        quizReturnTarget = QuizReturnTarget.SolarSystem;
        ShowSolarSystemUiOnly();
    }

    private bool RestoreFocusedCelestialBodyAfterQuiz()
    {
        if (quizUiRoot != null)
        {
            quizUiRoot.SetActive(false);
        }

        if (quizUiContainerRoot != null && quizUiContainerRoot != quizUiRoot)
        {
            quizUiContainerRoot.SetActive(false);
        }

        if (aiUiRoot != null)
        {
            aiUiRoot.SetActive(false);
        }

        if (arUiRoot != null)
        {
            arUiRoot.SetActive(false);
        }

        if (imagesGalleryOverlay != null)
        {
            imagesGalleryOverlay.SetActive(false);
        }

        if (imageViewerOverlay != null)
        {
            imageViewerOverlay.SetActive(false);
        }

        if (solarSystemRoot != null)
        {
            solarSystemRoot.SetActive(true);
        }

        PlanetInfoUI planetInfoUi = FindFirstObjectByType<PlanetInfoUI>();
        if (planetInfoUi == null || !planetInfoUi.RestoreFocusedBodyUiFromExternalNavigation())
        {
            return false;
        }

        if (solarSystemUiRoot != null)
        {
            solarSystemUiRoot.SetActive(false);
        }

        if (planetInfoCard != null)
        {
            planetInfoCard.SetActive(false);
        }

        quizReturnTarget = QuizReturnTarget.SolarSystem;
        return true;
    }

    private void ShowQuizHistoryPage()
    {
        SetPageActive(menuRoot, false);
        if (launchUiRoot != null) launchUiRoot.SetActive(false);
        if (solarSystemUiRoot != null) solarSystemUiRoot.SetActive(false);
        if (celestialBodyUiRoot != null) celestialBodyUiRoot.SetActive(false);
        if (planetInfoCard != null) planetInfoCard.SetActive(false);
        if (imagesGalleryOverlay != null) imagesGalleryOverlay.SetActive(false);
        if (imageViewerOverlay != null) imageViewerOverlay.SetActive(false);
        if (aiUiRoot != null) aiUiRoot.SetActive(false);
        if (arUiRoot != null) arUiRoot.SetActive(false);
        if (solarSystemRoot != null) solarSystemRoot.SetActive(false);

        if (quizUiContainerRoot != null && quizUiContainerRoot != quizUiRoot)
        {
            quizUiContainerRoot.SetActive(true);
        }

        if (quizUiRoot != null)
        {
            quizUiRoot.SetActive(true);
        }

        SetPageActive(quizTopicPage, false);
        SetPageActive(quizHomePage, false);
        SetPageActive(quizQuestionPage, false);
        SetPageActive(quizResultPage, false);
        SetPageActive(quizHistoryPage, true);
        SetPageActive(quizIntroPage, false);
        SetPageActive(quizBreakdownPage, false);
    }

    private void ShowAiUi()
    {
        SetPageActive(menuRoot, false);
        if (launchUiRoot != null) launchUiRoot.SetActive(false);
        if (solarSystemUiRoot != null) solarSystemUiRoot.SetActive(false);
        if (celestialBodyUiRoot != null) celestialBodyUiRoot.SetActive(false);
        if (planetInfoCard != null) planetInfoCard.SetActive(false);
        if (imagesGalleryOverlay != null) imagesGalleryOverlay.SetActive(false);
        if (imageViewerOverlay != null) imageViewerOverlay.SetActive(false);
        if (quizUiRoot != null) quizUiRoot.SetActive(false);
        if (quizUiContainerRoot != null && quizUiContainerRoot != quizUiRoot) quizUiContainerRoot.SetActive(false);
        if (arUiRoot != null) arUiRoot.SetActive(false);

        if (solarSystemRoot != null)
        {
            solarSystemRoot.SetActive(false);
        }

        if (aiUiRoot != null)
        {
            RectTransform aiRectTransform = aiUiRoot.transform as RectTransform;
            if (aiRectTransform != null && aiRectTransform.localScale.sqrMagnitude < 0.01f)
            {
                aiRectTransform.localScale = Vector3.one;
            }

            aiUiRoot.SetActive(true);
        }

        GameObject chatPage = FindChildObject(aiUiRoot, "ChatPage") ?? FindObjectByName("ChatPage");
        if (chatPage != null)
        {
            chatPage.SetActive(true);
        }

        ChatUIController.EnsureInstance().OpenChatUi();
    }

    public void OpenAiFromButton()
    {
        ShowAiUi();
    }

    private void ApplyHistoryAccessState()
    {
        bool allowHistory = true;

        foreach (Button button in quizHistoryButtons)
        {
            if (button == null)
            {
                continue;
            }

            button.interactable = allowHistory;

            Graphic graphic = button.targetGraphic != null ? button.targetGraphic : button.GetComponent<Graphic>();
            if (graphic != null)
            {
                Color color = graphic.color;
                color.a = allowHistory ? 1f : 0.45f;
                graphic.color = color;
            }
        }
    }

    private string GetFailedAttemptsKey(string email)
    {
        return "supabase.failed_login_count_" + email.Trim().ToLowerInvariant();
    }

    private string GetLockoutEndTimeKey(string email)
    {
        return "supabase.lockout_end_time_" + email.Trim().ToLowerInvariant();
    }

    private void ResetFailedLoginCount(string email)
    {
        if (string.IsNullOrEmpty(email)) return;
        string countKey = GetFailedAttemptsKey(email);
        string timeKey = GetLockoutEndTimeKey(email);
        PlayerPrefs.DeleteKey(countKey);
        PlayerPrefs.DeleteKey(timeKey);
        PlayerPrefs.Save();
    }

    private void IncrementFailedLoginCount(string email)
    {
        if (string.IsNullOrEmpty(email)) return;
        string countKey = GetFailedAttemptsKey(email);
        int currentCount = PlayerPrefs.GetInt(countKey, 0) + 1;
        PlayerPrefs.SetInt(countKey, currentCount);

        if (currentCount >= 5)
        {
            string timeKey = GetLockoutEndTimeKey(email);
            System.DateTime lockoutEndTime = System.DateTime.UtcNow.AddMinutes(5);
            PlayerPrefs.SetString(timeKey, lockoutEndTime.Ticks.ToString());
        }
        PlayerPrefs.Save();
    }

    private bool IsLockedOut(string email, out string statusMessage)
    {
        statusMessage = string.Empty;
        if (string.IsNullOrEmpty(email)) return false;

        string timeKey = GetLockoutEndTimeKey(email);
        if (PlayerPrefs.HasKey(timeKey))
        {
            string endTimeStr = PlayerPrefs.GetString(timeKey);
            if (long.TryParse(endTimeStr, out long endTimeTicks))
            {
                System.DateTime lockoutEndTime = new System.DateTime(endTimeTicks, System.DateTimeKind.Utc);
                System.DateTime now = System.DateTime.UtcNow;
                if (now < lockoutEndTime)
                {
                    System.TimeSpan remainingTime = lockoutEndTime - now;
                    double totalSecs = remainingTime.TotalSeconds;
                    if (totalSecs < 0) totalSecs = 0;
                    int minutes = Mathf.FloorToInt((float)totalSecs / 60f);
                    int seconds = Mathf.FloorToInt((float)totalSecs % 60f);
                    statusMessage = string.Format("Too many failed attempts. Try again in {0}:{1:00}", minutes, seconds);
                    return true;
                }
                else
                {
                    ResetFailedLoginCount(email);
                }
            }
        }
        return false;
    }

    private void StartLockoutTimer(string email)
    {
        if (lockoutTimerCoroutine != null)
        {
            StopCoroutine(lockoutTimerCoroutine);
        }
        lockoutTimerCoroutine = StartCoroutine(LockoutTimerRoutine(email));
    }

    private IEnumerator LockoutTimerRoutine(string email)
    {
        while (true)
        {
            string currentEmail = logInEmailInputField != null ? logInEmailInputField.text.Trim() : string.Empty;
            if (currentEmail.Equals(email, System.StringComparison.OrdinalIgnoreCase))
            {
                if (IsLockedOut(email, out string lockoutMessage))
                {
                    SetStatus(authStatusText, lockoutMessage);
                }
                else
                {
                    SetStatus(authStatusText, string.Empty);
                    lockoutTimerCoroutine = null;
                    yield break;
                }
            }
            else
            {
                if (IsLockedOut(currentEmail, out string lockoutMessage))
                {
                    SetStatus(authStatusText, lockoutMessage);
                }
                else
                {
                    if (authStatusText != null && authStatusText.text.StartsWith("Too many failed attempts"))
                    {
                        SetStatus(authStatusText, string.Empty);
                    }
                }
            }
            yield return new WaitForSecondsRealtime(1f);
        }
    }

    private void ShowSetDisplayNamePage()
    {
        if (displayNameInputField != null)
        {
            displayNameInputField.text = string.Empty;
        }
        if (displayNameCharCounterText != null)
        {
            displayNameCharCounterText.text = "0 / 20";
        }
        if (confirmIdentityButton != null)
        {
            confirmIdentityButton.interactable = false;
        }
        ShowLaunchPage(setDisplayNamePage);
    }

    private void HandleDisplayNameInputChanged(string val)
    {
        if (displayNameCharCounterText != null)
        {
            displayNameCharCounterText.text = $"{val.Length} / 20";
        }
        if (confirmIdentityButton != null)
        {
            confirmIdentityButton.interactable = val.Trim().Length >= 3;
        }
    }

    private void HandleRandomizeName()
    {
        string[] prefixes = { "Star", "Nova", "Cosmic", "Solar", "Galactic", "Astro", "Nebula", "Orion", "Alpha", "Zenith" };
        string[] suffixes = { "Explorer", "Pilot", "Navigator", "Ranger", "Voyager", "Seeker", "Commander", "Hunter", "Stargazer" };
        
        string randomName = prefixes[Random.Range(0, prefixes.Length)] + 
                            suffixes[Random.Range(0, suffixes.Length)];
        
        if (displayNameInputField != null)
        {
            displayNameInputField.text = randomName;
        }
    }

    private void HandleConfirmIdentity()
    {
        if (displayNameInputField == null) return;
        string newName = displayNameInputField.text.Trim();
        if (string.IsNullOrWhiteSpace(newName)) return;

        StartCoroutine(UpdateDisplayNameRoutine(newName));
    }

    private IEnumerator UpdateDisplayNameRoutine(string newName)
    {
        SetLoadingState(true, "Confirming identity...");
        yield return SupabaseAuthService.Instance.UpdateDisplayName(newName, result =>
        {
            SetLoadingState(false);
            if (result.Success)
            {
                OpenMainApplication();
            }
            else
            {
                SetStatus(authStatusText, result.Message);
            }
        });
    }

    private void ClearStatusTexts()
    {
        SetStatus(authStatusText, string.Empty);
        SetStatus(createAccountStatusText, string.Empty);
        SetStatus(forgotPasswordStatusText, string.Empty);
        SetStatus(verifyAccountStatusText, string.Empty);
        SetStatus(verifyPasswordStatusText, string.Empty);
        SetStatus(resetPasswordStatusText, string.Empty);
    }

    private void SetLoadingState(bool isVisible, string message = "Please wait...")
    {
        if (authLoadingOverlay != null)
        {
            authLoadingOverlay.SetActive(isVisible);
        }

        if (loadingText != null)
        {
            loadingText.text = message;
        }
    }

    private static void SetStatus(TMP_Text textComponent, string message, bool isSuccess = false)
    {
        if (textComponent == null)
        {
            return;
        }

        string normalizedMessage = message ?? string.Empty;
        textComponent.text = normalizedMessage;

        if (isSuccess)
        {
            textComponent.color = new Color(0.3f, 1f, 0.5f); // Beautiful neon/cosmic green
        }
        else
        {
            textComponent.color = new Color(1f, 0.3f, 0.3f); // Warning red
        }

        textComponent.gameObject.SetActive(!string.IsNullOrWhiteSpace(normalizedMessage));
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        int atIndex = email.IndexOf('@');
        int dotIndex = email.LastIndexOf('.');
        return atIndex > 0 && dotIndex > atIndex + 1 && dotIndex < email.Length - 1;
    }

    private static bool IsStrongPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return false;
        }

        bool hasUpper = false;
        bool hasLower = false;
        bool hasDigit = false;

        foreach (char character in password)
        {
            if (char.IsUpper(character))
            {
                hasUpper = true;
            }
            else if (char.IsLower(character))
            {
                hasLower = true;
            }
            else if (char.IsDigit(character))
            {
                hasDigit = true;
            }
        }

        return hasUpper && hasLower && hasDigit;
    }

    private static void SetPageActive(GameObject pageObject, bool isActive)
    {
        if (pageObject != null)
        {
            pageObject.SetActive(isActive);
        }
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

    private static Button FindButton(GameObject root, string objectName)
    {
        return FindComponentInChildrenByName<Button>(root, objectName);
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

            TMP_Text textComponent = button.GetComponentInChildren<TMP_Text>(true);
            if (textComponent != null && string.Equals(textComponent.text.Trim(), childText, System.StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }

    private static TMP_InputField FindInputField(GameObject root, string objectName)
    {
        return FindComponentInChildrenByName<TMP_InputField>(root, objectName);
    }

    private static TMP_Text FindText(GameObject root, string objectName)
    {
        return FindComponentInChildrenByName<TMP_Text>(root, objectName);
    }

    private static GameObject FindChildObject(GameObject root, string objectName)
    {
        Transform childTransform = FindComponentInChildrenByName<Transform>(root, objectName);
        return childTransform != null ? childTransform.gameObject : null;
    }

    private static List<Button> FindButtons(GameObject root, string objectName)
    {
        List<Button> results = new List<Button>();
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return results;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button != null && button.gameObject.name == objectName)
            {
                results.Add(button);
            }
        }

        return results;
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

    private static List<Button> FindButtonsByNameGlobal(string objectName)
    {
        List<Button> results = new List<Button>();
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return results;
        }

        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (Button button in buttons)
        {
            if (button == null || !button.gameObject.scene.IsValid() || (button.gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene() && button.gameObject.scene.name != "DontDestroyOnLoad"))
            {
                continue;
            }

            if (button.gameObject.name == objectName)
            {
                results.Add(button);
            }
        }

        return results;
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
            if (rootObject == null || rootObject.hideFlags != HideFlags.None || !rootObject.scene.IsValid() || (rootObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene() && rootObject.scene.name != "DontDestroyOnLoad"))
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

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "null";
        }

        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = $"{target.name}/{path}";
        }

        return path;
    }

    private static bool IsNumerical(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (char c in text)
        {
            if (!char.IsDigit(c)) return false;
        }
        return true;
    }

    private static string Extract8DigitCode(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        for (int i = 0; i <= text.Length - 8; i++)
        {
            string sub = text.Substring(i, 8);
            if (IsNumerical(sub))
            {
                return sub;
            }
        }

        string allDigits = "";
        foreach (char c in text)
        {
            if (char.IsDigit(c))
            {
                allDigits += c;
            }
        }

        if (allDigits.Length == 8)
        {
            return allDigits;
        }

        return null;
    }
}
