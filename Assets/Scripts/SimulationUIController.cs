using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimulationUIController : MonoBehaviour
{
    public Button pauseButton;
    public Slider speedSlider;
    public TMP_Text pauseLabel;
    public Sprite pauseSprite;
    public Sprite playSprite;

    private bool isPaused;
    private float lastSpeed = 1f;

    private void Awake()
    {
        if (pauseButton != null)
        {
            pauseButton.onClick.AddListener(TogglePause);
        }

        if (speedSlider != null)
        {
            speedSlider.onValueChanged.AddListener(SetSpeed);
            lastSpeed = speedSlider.value;
        }
    }

    private void Start()
    {
        ApplySpeed(lastSpeed);
        UpdatePauseLabel();
    }

    private void OnDestroy()
    {
        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveListener(TogglePause);
        }

        if (speedSlider != null)
        {
            speedSlider.onValueChanged.RemoveListener(SetSpeed);
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        ApplySpeed(isPaused ? 0f : lastSpeed);
        UpdatePauseLabel();
    }

    public void SetSpeed(float value)
    {
        lastSpeed = value;

        if (!isPaused)
        {
            ApplySpeed(lastSpeed);
        }
    }

    private static void ApplySpeed(float speed)
    {
        Time.timeScale = speed;
    }

    private void UpdatePauseLabel()
    {
        if (pauseLabel == null)
        {
            UpdatePauseIcon();
            return;
        }

        pauseLabel.text = isPaused ? "Play" : "Pause";
        UpdatePauseIcon();
    }

    private void UpdatePauseIcon()
    {
        if (pauseButton == null || pauseButton.image == null)
        {
            return;
        }

        Sprite nextSprite = isPaused ? playSprite : pauseSprite;

        if (nextSprite != null)
        {
            pauseButton.image.sprite = nextSprite;
            pauseButton.image.preserveAspect = true;
            pauseButton.image.SetNativeSize();
            pauseButton.image.rectTransform.sizeDelta = new Vector2(100, 100);
        }
    }
}
