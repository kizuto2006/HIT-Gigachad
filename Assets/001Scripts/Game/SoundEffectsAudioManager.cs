using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Central SFX router. UI clicks use Menu Sound, while gameplay sounds use
/// Game Sound. Master Volume remains handled by AudioListener.
/// </summary>
[DisallowMultipleComponent]
public sealed class SoundEffectsAudioManager : MonoBehaviour
{
    public static SoundEffectsAudioManager Instance { get; private set; }

    [Header("SFX Clips")]
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private AudioClip upgradeSound;
    [SerializeField] private AudioClip xpSound;
    [SerializeField] private AudioClip bossAppearSound;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip takeDamageSound;
    [SerializeField] private AudioClip loseSound;
    [SerializeField] private AudioClip warningSound;

    [Header("SFX Source")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField, Min(0.05f)] private float selectableScanInterval = 0.25f;

    private XPSystem subscribedXpSystem;
    private float nextGameplayScanTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveSource();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextGameplayScanTime)
            return;

        nextGameplayScanTime = Time.unscaledTime + selectableScanInterval;
        BindXpSystem();
        RegisterSelectableSounds();
    }

    private void OnDestroy()
    {
        UnbindXpSystem();

        if (Instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UnbindXpSystem();
        nextGameplayScanTime = 0f;
        RegisterSelectableSounds();
        BindXpSystem();
    }

    private void ResolveSource()
    {
        if (sfxSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length > 1)
                sfxSource = sources[1];
        }

        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.ignoreListenerPause = true;
        sfxSource.volume = 1f;
    }

    private void RegisterSelectableSounds()
    {
        Selectable[] selectables = FindObjectsByType<Selectable>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < selectables.Length; i++)
        {
            Selectable selectable = selectables[i];
            if (selectable == null)
                continue;

            if (selectable.GetComponent<UIInteractionSound>() == null)
                selectable.gameObject.AddComponent<UIInteractionSound>();
        }
    }

    private void BindXpSystem()
    {
        XPSystem current = FindFirstObjectByType<XPSystem>();
        if (current == subscribedXpSystem)
            return;

        UnbindXpSystem();
        subscribedXpSystem = current;
        if (subscribedXpSystem == null)
            return;

        subscribedXpSystem.OnXPReceived += HandleXpReceived;
        subscribedXpSystem.OnLevelUp += HandleLevelUp;
    }

    private void UnbindXpSystem()
    {
        if (subscribedXpSystem == null)
            return;

        subscribedXpSystem.OnXPReceived -= HandleXpReceived;
        subscribedXpSystem.OnLevelUp -= HandleLevelUp;
        subscribedXpSystem = null;
    }

    private void HandleXpReceived(int amount)
    {
        PlayXpSound();
    }

    private void HandleLevelUp(int level)
    {
        PlayUpgradeSound();
    }

    public void PlayClickSound()
    {
        Play(clickSound, UISettings.MenuSoundVolume);
    }

    public void PlayUpgradeSound()
    {
        Play(upgradeSound, UISettings.GameSoundVolume);
    }

    public void PlayXpSound()
    {
        Play(xpSound, UISettings.GameSoundVolume);
    }

    public void PlayBossAppearSound()
    {
        Play(bossAppearSound, UISettings.GameSoundVolume);
    }

    public void PlayJumpSound()
    {
        Play(jumpSound, UISettings.GameSoundVolume);
    }

    public void PlayTakeDamageSound()
    {
        Play(takeDamageSound, UISettings.GameSoundVolume);
    }

    public void PlayLoseSound()
    {
        Play(loseSound, UISettings.GameSoundVolume);
    }

    public void PlayWarningSound()
    {
        Play(warningSound, UISettings.GameSoundVolume);
    }

    public void PlayWeaponSound(AudioClip clip, Vector3 position)
    {
        Play(clip, UISettings.GameSoundVolume);
    }

    private void Play(AudioClip clip, float channelVolume)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip, Mathf.Clamp01(channelVolume));
    }
}
