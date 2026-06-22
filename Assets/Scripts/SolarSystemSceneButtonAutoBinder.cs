using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SolarSystemSceneButtonAutoBinder
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BindOnSceneLoad()
    {
        BindSceneButtons();
    }

    public static void BindSceneButtons()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name != "SolarSystemScene")
        {
            return;
        }

        AuthUIManager authUiManager = Object.FindFirstObjectByType<AuthUIManager>();
        if (authUiManager == null)
        {
            return;
        }

        ARSceneLauncher templateArLauncher = FindTemplateArLauncher(activeScene);
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (Button button in buttons)
        {
            if (button == null || !button.gameObject.scene.IsValid() || button.gameObject.scene != activeScene)
            {
                continue;
            }

            switch (button.gameObject.name)
            {
                case "MenuButton":
                    button.onClick.RemoveListener(authUiManager.OpenMenuFromButton);
                    button.onClick.AddListener(authUiManager.OpenMenuFromButton);
                    break;

                case "QuizButton":
                case "QuizTopicButton":
                    button.onClick.RemoveListener(authUiManager.OpenQuizFromButton);
                    button.onClick.AddListener(authUiManager.OpenQuizFromButton);
                    break;

                case "AskAIButton":
                    button.onClick.RemoveListener(authUiManager.OpenAiFromButton);
                    button.onClick.AddListener(authUiManager.OpenAiFromButton);
                    break;

                case "ExploreAR":
                    BindArOpen(button, templateArLauncher);
                    break;
            }

            switch (GetButtonLabel(button))
            {
                case "SOLAR SYSTEM":
                    button.onClick.RemoveListener(authUiManager.ForceShowSolarSystemUiOnly);
                    button.onClick.AddListener(authUiManager.ForceShowSolarSystemUiOnly);
                    break;

                case "TAKE QUIZZES":
                    button.onClick.RemoveListener(authUiManager.OpenQuizFromButton);
                    button.onClick.AddListener(authUiManager.OpenQuizFromButton);
                    break;

                case "EXPLORE AR":
                    BindArOpen(button, templateArLauncher);
                    break;

                case "ASTRO BOT":
                    button.onClick.RemoveListener(authUiManager.OpenAiFromButton);
                    button.onClick.AddListener(authUiManager.OpenAiFromButton);
                    break;
            }
        }
    }

    private static string GetButtonLabel(Button button)
    {
        TMP_Text[] tmpTexts = button.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmpTexts.Length; i++)
        {
            TMP_Text text = tmpTexts[i];
            if (text == null || string.IsNullOrWhiteSpace(text.text))
            {
                continue;
            }

            return NormalizeLabel(text.text);
        }

        Text[] legacyTexts = button.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < legacyTexts.Length; i++)
        {
            Text text = legacyTexts[i];
            if (text == null || string.IsNullOrWhiteSpace(text.text))
            {
                continue;
            }

            return NormalizeLabel(text.text);
        }

        return string.Empty;
    }

    private static string NormalizeLabel(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('\r', ' ').Replace('\n', ' ').Trim().ToUpperInvariant();
    }

    private static void BindArOpen(Button button, ARSceneLauncher templateLauncher)
    {
        if (button == null)
        {
            return;
        }

        ARSceneLauncher launcher = button.GetComponent<ARSceneLauncher>();
        if (launcher == null)
        {
            launcher = button.gameObject.AddComponent<ARSceneLauncher>();
        }

        if (templateLauncher != null)
        {
            launcher.CopyConfigurationFrom(templateLauncher);
        }

        button.onClick.RemoveListener(launcher.OpenArScene);
        button.onClick.AddListener(launcher.OpenArScene);
    }

    private static ARSceneLauncher FindTemplateArLauncher(Scene activeScene)
    {
        ARSceneLauncher fallback = null;
        ARSceneLauncher[] launchers = Resources.FindObjectsOfTypeAll<ARSceneLauncher>();
        for (int i = 0; i < launchers.Length; i++)
        {
            ARSceneLauncher launcher = launchers[i];
            if (launcher == null || !launcher.gameObject.scene.IsValid() || launcher.gameObject.scene != activeScene)
            {
                continue;
            }

            if (launcher.gameObject.name == "ARButton")
            {
                return launcher;
            }

            fallback ??= launcher;
        }

        return fallback;
    }
}
