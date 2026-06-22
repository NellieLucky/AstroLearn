using UnityEngine;
using UnityEngine.UI;

public class ARButtonAutoBinder : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bind()
    {
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (Button button in buttons)
        {
            if (button == null || button.gameObject.name != "ARButton" || !button.gameObject.scene.IsValid())
            {
                continue;
            }

            ARSceneLauncher launcher = button.GetComponent<ARSceneLauncher>();
            if (launcher == null)
            {
                launcher = button.gameObject.AddComponent<ARSceneLauncher>();
            }

            button.onClick.RemoveListener(launcher.OpenArScene);
            button.onClick.AddListener(launcher.OpenArScene);
        }
    }
}
