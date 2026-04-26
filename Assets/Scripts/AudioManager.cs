using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Inspector'da sesleri liste halinde g�rmek i�in �zel bir s�n�f
[System.Serializable]
public class Sound
{
    public string name;        // Sese verece�in isim (�rn: "Jump", "Click", "BGM_Main")
    public AudioClip clip;     // Ses dosyas�n�n kendisi
    [Range(0f, 1f)]
    public float volume = 1f;  // Bu sese �zel varsay�lan ses seviyesi
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Ses Kaynakları (Audio Sources)")]
    [Tooltip("Müzik için kullanılacak AudioSource (Loop açık olmalı)")]
    public AudioSource musicSource;
    [Tooltip("One shot SFX için kullanılacak AudioSource(Sayfa çevirme vs.)")]
    public AudioSource oneShotSfxSource;
    [Tooltip("Looped SFX için kullanılacak AudioSource(Yürüme vs.)")]
    public AudioSource loopedSfxSource;

    [Header("Ses Kütüphanesi")]
    public List<Sound> musicSounds;
    public List<Sound> oneShotSfxSounds;
    public List<Sound> loopedSfxSounds;

    [Header("Internal Settings")]
    public float musicFadeDuration = 0.5f;
    public float loopedSfxFadeDuration = 0.5f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }


    public void PlayMusic(string name)
    {
        Sound s = musicSounds.Find(x => x.name == name);

        if (s == null)
        {
            Debug.LogWarning("Müzik bulunamadı: " + name);
            return;
        }

        if (musicSource.clip == s.clip && musicSource.isPlaying) return;

        musicSource.clip = s.clip;
        musicSource.volume = s.volume;
        musicSource.Play();
    }

    public void StopMusic()
    {
        StartCoroutine(FadeOutAndStopRoutine(musicSource, musicFadeDuration));
    }

    public void StopLoopedSFX()
    {
        StartCoroutine(FadeOutAndStopRoutine(loopedSfxSource, loopedSfxFadeDuration));
    }

    private IEnumerator FadeOutAndStopRoutine(AudioSource source, float fadeTime)
    {
        if (source == null || !source.isPlaying) yield break;

        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeTime);
            yield return null;
        }

        source.volume = 0f;
        source.Stop();
        source.volume = startVolume;
    }

    public void PlayOneShotSFX(string name)
    {
        Sound s = oneShotSfxSounds.Find(x => x.name == name);

        if (s == null)
        {
            Debug.LogWarning("SFX bulunamadı: " + name);
            return;
        }

        oneShotSfxSource.PlayOneShot(s.clip, s.volume);
    }

    public void PlayLoopedSFX(string name)
    {
        Sound s = loopedSfxSounds.Find(x => x.name == name);
        if (s == null)
        {
            Debug.LogWarning("Looped SFX bulunamadı: " + name);
            return;
        }
        if (loopedSfxSource.clip == s.clip && loopedSfxSource.isPlaying) return;
        loopedSfxSource.clip = s.clip;
        loopedSfxSource.volume = s.volume;
        loopedSfxSource.Play();
    }

    public void SkipMusicTime(float seconds)
    {
        if (musicSource == null || musicSource.clip == null) return;

        float newTime = musicSource.time + seconds;

        musicSource.time = Mathf.Clamp(newTime, 0f, musicSource.clip.length);
    }
}