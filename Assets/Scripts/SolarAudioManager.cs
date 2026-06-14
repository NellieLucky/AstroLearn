using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlanetMusicEntry
{
    public string bodyName;
    public AudioClip clip;
}

public class SolarAudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    public AudioSource backgroundMusicSource;
    public AudioSource planetMusicSource;

    [Header("Default Music")]
    public AudioClip solarSystemBackgroundMusic;

    [Header("Planet Music")]
    public List<PlanetMusicEntry> planetMusic = new List<PlanetMusicEntry>();

    private Dictionary<string, AudioClip> musicLookup;

    private void Awake()
    {
        musicLookup = new Dictionary<string, AudioClip>();

        foreach (PlanetMusicEntry entry in planetMusic)
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.bodyName) && entry.clip != null)
            {
                musicLookup[entry.bodyName] = entry.clip;
            }
        }
    }

    private void Start()
    {
        PlayBackgroundMusic();
    }

    public void PlayBackgroundMusic()
    {
        if (backgroundMusicSource == null || solarSystemBackgroundMusic == null)
        {
            return;
        }

        if (backgroundMusicSource.clip != solarSystemBackgroundMusic)
        {
            backgroundMusicSource.clip = solarSystemBackgroundMusic;
        }

        if (!backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.Play();
        }
    }

    public void PlayPlanetMusic(string bodyName)
    {
        if (planetMusicSource == null || string.IsNullOrWhiteSpace(bodyName))
        {
            return;
        }

        if (musicLookup != null && musicLookup.TryGetValue(bodyName, out AudioClip clip) && clip != null)
        {
            if (backgroundMusicSource != null && backgroundMusicSource.isPlaying)
            {
                backgroundMusicSource.Pause();
            }

            bool clipChanged = planetMusicSource.clip != clip;
            if (clipChanged)
            {
                planetMusicSource.clip = clip;
            }

            if (!planetMusicSource.isPlaying || clipChanged)
            {
                planetMusicSource.Play();
            }
        }
    }

    public void StopPlanetMusic()
    {
        if (planetMusicSource != null)
        {
            planetMusicSource.Stop();
            planetMusicSource.clip = null;
        }

        if (backgroundMusicSource != null && solarSystemBackgroundMusic != null)
        {
            if (backgroundMusicSource.clip != solarSystemBackgroundMusic)
            {
                backgroundMusicSource.clip = solarSystemBackgroundMusic;
            }

            if (!backgroundMusicSource.isPlaying)
            {
                backgroundMusicSource.Play();
            }
            else
            {
                backgroundMusicSource.UnPause();
            }
        }
    }

    public bool IsPlayingPlanetMusic(string bodyName)
    {
        return
            planetMusicSource != null &&
            planetMusicSource.isPlaying &&
            planetMusicSource.clip != null &&
            !string.IsNullOrWhiteSpace(bodyName) &&
            musicLookup != null &&
            musicLookup.TryGetValue(bodyName, out AudioClip clip) &&
            planetMusicSource.clip == clip;
    }

    public void TogglePlanetMusic(string bodyName)
    {
        if (string.IsNullOrWhiteSpace(bodyName))
        {
            return;
        }

        if (IsPlayingPlanetMusic(bodyName))
        {
            StopPlanetMusic();
            return;
        }

        PlayPlanetMusic(bodyName);
    }
}
