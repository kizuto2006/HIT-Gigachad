using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour
{
    private const string AudioResourceRoot = "Audio/";
    private const string MenuSceneName = "GigabonkMenu";
    private const string GameplaySceneName = "DesertArena";

    public static AudioManager Instance { get; private set; }

    [Header("Music")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameplayMusic;

    [Header("UI SFX")]
    [SerializeField] private AudioClip buttonHoverSound;
    [SerializeField] private AudioClip buttonClickSound;

    [Header("Gameplay SFX")]
    [SerializeField] private AudioClip enemyHitSound;
    [SerializeField] private AudioClip playerHurtSound;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landSound;
    [SerializeField] private AudioClip bossWarningSound;
    [SerializeField] private AudioClip bossSpawnSound;

    [Header("Mixing")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioMixerGroup musicOutput;
    [SerializeField] private AudioMixerGroup sfxOutput;

    [Header("Playback")]
    [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float uiVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float gameplaySfxVolume = 1f;
    [SerializeField, Min(0f)] private float minimumEnemyHitInterval = 0.04f;

    private AudioSource musicSource;
    private AudioSource uiSource;
    private AudioSource gameplaySfxSource;
    private float lastEnemyHitTime = float.NegativeInfinity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null || FindFirstObjectByType<AudioManager>() != null)
        {
            return;
        }

        AudioManager prefab = Resources.Load<AudioManager>(AudioResourceRoot + "AudioManager");
        if (prefab != null)
        {
            Instantiate(prefab);
            return;
        }

        GameObject managerObject = new GameObject("AudioManager");
        managerObject.AddComponent<AudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadResourcesFallbacks();
        ResolveMixerGroups();
        CreateAudioSources();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MenuSceneName)
        {
            PlayMusic(menuMusic);
        }
        else if (scene.name == GameplaySceneName)
        {
            PlayMusic(gameplayMusic);
        }

        UIButtonSfx.AttachToScene(scene);
    }

    public void PlayMenuMusic()
    {
        PlayMusic(menuMusic);
    }

    public void PlayGameplayMusic()
    {
        PlayMusic(gameplayMusic);
    }

    public void PlayButtonHover()
    {
        PlayOneShot(uiSource, buttonHoverSound, uiVolume);
    }

    public void PlayButtonClick()
    {
        PlayOneShot(uiSource, buttonClickSound, uiVolume);
    }

    public void PlayEnemyHit()
    {
        float now = Time.unscaledTime;
        if (now < lastEnemyHitTime + minimumEnemyHitInterval)
        {
            return;
        }

        lastEnemyHitTime = now;
        PlayGameplaySfx(enemyHitSound);
    }

    public void PlayPlayerHurt()
    {
        PlayGameplaySfx(playerHurtSound);
    }

    public void PlayJump()
    {
        PlayGameplaySfx(jumpSound);
    }

    public void PlayLand()
    {
        PlayGameplaySfx(landSound);
    }

    public void PlayBossWarning()
    {
        PlayGameplaySfx(bossWarningSound);
    }

    public void PlayBossSpawn()
    {
        PlayGameplaySfx(bossSpawnSound);
    }

    private void PlayMusic(AudioClip clip)
    {
        if (musicSource == null || clip == null)
        {
            return;
        }

        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            return;
        }

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    private void PlayGameplaySfx(AudioClip clip)
    {
        PlayOneShot(gameplaySfxSource, clip, gameplaySfxVolume);
    }

    private static void PlayOneShot(AudioSource source, AudioClip clip, float volume)
    {
        if (source != null && clip != null)
        {
            source.PlayOneShot(clip, volume);
        }
    }

    private void CreateAudioSources()
    {
        musicSource = CreateSource("Music Source", musicOutput, true);
        uiSource = CreateSource("UI SFX Source", sfxOutput, false);
        gameplaySfxSource = CreateSource("Gameplay SFX Source", sfxOutput, false);
    }

    private AudioSource CreateSource(string sourceName, AudioMixerGroup output, bool loop)
    {
        Transform existingChild = transform.Find(sourceName);
        GameObject sourceObject;
        if (existingChild != null)
        {
            sourceObject = existingChild.gameObject;
        }
        else
        {
            sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
        }

        AudioSource source = sourceObject.GetComponent<AudioSource>();
        if (source == null)
        {
            source = sourceObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = output;
        return source;
    }

    private void ResolveMixerGroups()
    {
        if (audioMixer == null)
        {
            audioMixer = Resources.Load<AudioMixer>(AudioResourceRoot + "GameAudioMixer");
        }

        if (audioMixer == null)
        {
            return;
        }

        if (musicOutput == null)
        {
            musicOutput = FindMixerGroup("Music");
        }

        if (sfxOutput == null)
        {
            sfxOutput = FindMixerGroup("SFX");
        }
    }

    private AudioMixerGroup FindMixerGroup(string groupName)
    {
        AudioMixerGroup[] groups = audioMixer.FindMatchingGroups(groupName);
        return groups.Length > 0 ? groups[0] : null;
    }

    private void LoadResourcesFallbacks()
    {
        menuMusic = LoadClipIfMissing(menuMusic, "MenuMusic");
        gameplayMusic = LoadClipIfMissing(gameplayMusic, "GameplayMusic");
        buttonHoverSound = LoadClipIfMissing(buttonHoverSound, "ButtonHover");
        buttonClickSound = LoadClipIfMissing(buttonClickSound, "ButtonClick");
        enemyHitSound = LoadClipIfMissing(enemyHitSound, "EnemyHit");
        playerHurtSound = LoadClipIfMissing(playerHurtSound, "PlayerHurt");
        jumpSound = LoadClipIfMissing(jumpSound, "Jump");
        landSound = LoadClipIfMissing(landSound, "Land");
        bossWarningSound = LoadClipIfMissing(bossWarningSound, "BossWarning");
        bossSpawnSound = LoadClipIfMissing(bossSpawnSound, "BossSpawn");
    }

    private static AudioClip LoadClipIfMissing(AudioClip clip, string resourceName)
    {
        return clip != null
            ? clip
            : Resources.Load<AudioClip>(AudioResourceRoot + resourceName);
    }
}
