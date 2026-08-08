using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the music track that should survive scene transitions.
/// Channel settings are read from UISettings, while temporary ducking only
/// changes the effective AudioSource volume and never changes the saved slider.
/// </summary>
[DisallowMultipleComponent]
public sealed class MusicAudioManager : MonoBehaviour
{
    public static MusicAudioManager Instance { get; private set; }

    [Header("Tracks")]
    [SerializeField] private AudioClip menuSound;
    [SerializeField] private AudioClip desertLoopOne;
    [SerializeField] private AudioClip desertLoopTwo;

    [Header("Temporary Ducking")]
    [SerializeField, Range(0f, 1f)] private float duckMultiplier = 0.3f;

    private AudioSource musicSource;
    private bool isMenuTrack;
    private int musicDuckRequests;

    public int MusicDuckRequestCount => musicDuckRequests;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = GetComponent<AudioSource>();
        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.ignoreListenerPause = true;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void Update()
    {
        ApplyEffectiveVolume();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GigabonkMenu")
        {
            PlayMenuMusic();
        }
        else if (scene.name == "DesertArena")
        {
            PlayDesertMusic();
        }
    }

    public void PlayMenuMusic()
    {
        isMenuTrack = true;
        PlayTrack(menuSound);
    }

    public void StopMenuMusic()
    {
        StopMusic();
    }

    public void StopMusic()
    {
        if (musicSource == null)
            return;

        musicSource.Stop();
        musicSource.clip = null;
    }

    public void PlayDesertMusic()
    {
        isMenuTrack = false;

        AudioClip selectedClip = Random.value < 0.5f ? desertLoopOne : desertLoopTwo;
        if (selectedClip == null)
            selectedClip = desertLoopOne != null ? desertLoopOne : desertLoopTwo;

        PlayTrack(selectedClip);
    }

    public void PushMusicDuck()
    {
        musicDuckRequests++;
        ApplyEffectiveVolume();
    }

    public void PopMusicDuck()
    {
        musicDuckRequests = Mathf.Max(0, musicDuckRequests - 1);
        ApplyEffectiveVolume();
    }

    private void PlayTrack(AudioClip clip)
    {
        if (musicSource == null)
            return;

        if (clip == null)
        {
            musicSource.Stop();
            musicSource.clip = null;
            return;
        }

        if (musicSource.clip != clip)
        {
            musicSource.Stop();
            musicSource.clip = clip;
        }

        musicSource.loop = true;
        ApplyEffectiveVolume();
        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    private void ApplyEffectiveVolume()
    {
        if (musicSource == null)
            return;

        float channelVolume = isMenuTrack
            ? UISettings.MenuSoundVolume
            : UISettings.MusicVolume;
        float duckVolume = musicDuckRequests > 0 ? Mathf.Clamp01(duckMultiplier) : 1f;
        musicSource.volume = Mathf.Clamp01(channelVolume) * duckVolume;
    }
}
