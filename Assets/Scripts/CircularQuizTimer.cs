using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CircularQuizTimer : MonoBehaviour
{
    public Image timerFill;
    public TMP_Text timerText;
    public float duration = 30f;
    public System.Action TimerExpired;

    private float timeRemaining;
    private bool isRunning;
    private bool hasNotifiedExpiry;

    private void OnEnable()
    {
        ResetTimer();
        StartTimer();
    }

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            isRunning = false;
            if (!hasNotifiedExpiry)
            {
                hasNotifiedExpiry = true;
                TimerExpired?.Invoke();
            }
        }

        UpdateVisuals();
    }

    public void StartTimer()
    {
        isRunning = true;
    }

    public void StopTimer()
    {
        isRunning = false;
    }

    public void ResetTimer()
    {
        timeRemaining = duration;
        hasNotifiedExpiry = false;
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (timerFill != null)
        {
            timerFill.fillAmount = duration > 0f ? timeRemaining / duration : 0f;
        }

        if (timerText != null)
        {
            timerText.text = Mathf.CeilToInt(timeRemaining).ToString();
        }
    }
}
