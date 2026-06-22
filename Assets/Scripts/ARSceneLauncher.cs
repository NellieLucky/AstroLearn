using UnityEngine;
using UnityEngine.SceneManagement;

public class ARSceneLauncher : MonoBehaviour
{
    [SerializeField] private string arSceneName = "SolarSystemARScene";
    [SerializeField] private LoadSceneMode loadSceneMode = LoadSceneMode.Single;

    public void OpenArScene()
    {
        if (string.IsNullOrWhiteSpace(arSceneName))
        {
            Debug.LogWarning("[ARSceneLauncher] AR scene name is empty.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(arSceneName))
        {
            Debug.LogWarning($"[ARSceneLauncher] Scene '{arSceneName}' is not in Build Settings.");
            return;
        }

        SceneManager.LoadScene(arSceneName, loadSceneMode);
    }
}
