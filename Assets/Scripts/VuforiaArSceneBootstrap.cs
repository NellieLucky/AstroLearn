using UnityEngine;
using UnityEngine.SceneManagement;
using Vuforia;

public static class VuforiaArSceneBootstrap
{
    private const string ArSceneName = "SolarSystemARScene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != ArSceneName)
        {
            return;
        }

        VuforiaApplication app = VuforiaApplication.Instance;
        if (app == null || app.IsInitialized || app.IsRunning)
        {
            return;
        }

        app.Initialize();
    }
}
