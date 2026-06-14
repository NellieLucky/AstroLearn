using UnityEngine;
using UnityEngine.UI;

public class MusicIconAnimator : MonoBehaviour
{
    public Image targetImage;
    public Sprite[] frames;
    public float frameRate = 12f;
    public bool playOnStart = false;
    public Sprite idleSprite;
    public bool skipFirstFrameWhilePlaying = true;

    private int currentFrame;
    private float timer;
    private bool isPlaying;
    private Sprite fallbackIdleSprite;

    private void Awake()
    {
        if (targetImage != null)
        {
            fallbackIdleSprite = targetImage.sprite;
        }
    }

    private void Start()
    {
        isPlaying = playOnStart;

        if (targetImage != null)
        {
            if (isPlaying && frames != null && frames.Length > 0)
            {
                currentFrame = GetPlaybackStartFrameIndex();
                SetTargetSprite(frames[currentFrame]);
            }
            else
            {
                SetTargetSprite(GetIdleSprite());
            }
        }
    }

    private void Update()
    {
        if (!isPlaying || targetImage == null || frames == null || frames.Length == 0)
        {
            return;
        }

        timer += Time.deltaTime;
        if (timer >= 1f / frameRate)
        {
            timer = 0f;
            currentFrame = GetNextPlaybackFrameIndex(currentFrame);
            SetTargetSprite(frames[currentFrame]);
        }
    }

    public void Play()
    {
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        isPlaying = true;
        currentFrame = GetPlaybackStartFrameIndex();
        timer = 0f;
        SetTargetSprite(frames[currentFrame]);
    }

    public void Stop()
    {
        isPlaying = false;
        currentFrame = 0;
        timer = 0f;

        if (targetImage != null)
        {
            SetTargetSprite(GetIdleSprite());
        }
    }

    public void SetPlaying(bool playing)
    {
        if (playing)
        {
            Play();
        }
        else
        {
            Stop();
        }
    }

    private Sprite GetIdleSprite()
    {
        if (idleSprite != null)
        {
            return idleSprite;
        }

        if (fallbackIdleSprite != null)
        {
            return fallbackIdleSprite;
        }

        return frames != null && frames.Length > 0 ? frames[0] : null;
    }

    private void SetTargetSprite(Sprite sprite)
    {
        if (targetImage == null)
        {
            return;
        }

        targetImage.sprite = sprite;
        targetImage.overrideSprite = sprite;
        targetImage.SetAllDirty();
    }

    private int GetPlaybackStartFrameIndex()
    {
        if (frames == null || frames.Length == 0)
        {
            return 0;
        }

        if (skipFirstFrameWhilePlaying && frames.Length > 1)
        {
            return 1;
        }

        return 0;
    }

    private int GetNextPlaybackFrameIndex(int frameIndex)
    {
        if (frames == null || frames.Length == 0)
        {
            return 0;
        }

        int nextIndex = (frameIndex + 1) % frames.Length;

        if (skipFirstFrameWhilePlaying && frames.Length > 1 && nextIndex == 0)
        {
            nextIndex = 1;
        }

        return nextIndex;
    }
}
