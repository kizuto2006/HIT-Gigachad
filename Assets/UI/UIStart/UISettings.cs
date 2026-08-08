using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UISettings : MonoBehaviour
{
    private const string MasterVolumeKey = "Settings.MasterVolume";
    private const string MusicVolumeKey = "Settings.MusicVolume";
    private const string GameSoundVolumeKey = "Settings.GameSoundVolume";
    private const string MenuSoundVolumeKey = "Settings.MenuSoundVolume";

    private const float DefaultMasterVolume = 0.75f;
    private const float DefaultMusicVolume = 0.65f;
    private const float DefaultGameSoundVolume = 0.80f;
    private const float DefaultMenuSoundVolume = 0.70f;

    [Header("Panel")]
    [SerializeField] private GameObject panelSettings;

    [Header("Navigation")]
    [SerializeField] private Button btnBack;

    [Header("Volume Controls")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider gameSoundSlider;
    [SerializeField] private Slider menuSoundSlider;
    [SerializeField] private TMP_Text masterVolumeValue;
    [SerializeField] private TMP_Text musicVolumeValue;
    [SerializeField] private TMP_Text gameSoundValue;
    [SerializeField] private TMP_Text menuSoundValue;

    private void Awake()
    {
        if (panelSettings == null)
            panelSettings = gameObject;

        ResolveReferences();
        RegisterListeners();
        LoadSavedValues();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RegisterListeners();
        LoadSavedValues();
    }

    private void OnDestroy()
    {
        UnregisterListeners();
        SaveValues();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            SaveValues();
    }

    private void OnApplicationQuit()
    {
        SaveValues();
    }

    public void SetPanelRoot(GameObject panelRoot)
    {
        if (panelRoot != null)
            panelSettings = panelRoot;
    }

    public void OnClickButtonBack()
    {
        SaveValues();

        if (panelSettings != null)
            panelSettings.SetActive(false);

        UIController menuController = UIController.Instance;
        if (menuController != null &&
            menuController.UISettings != null &&
            menuController.UISettings.gameObject == panelSettings &&
            menuController.StartUI != null)
        {
            menuController.StartUI.SetActiveStartPanel(true);
        }
    }

    public static float MasterVolume => PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume);
    public static float MusicVolume => PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
    public static float GameSoundVolume => PlayerPrefs.GetFloat(GameSoundVolumeKey, DefaultGameSoundVolume);
    public static float MenuSoundVolume => PlayerPrefs.GetFloat(MenuSoundVolumeKey, DefaultMenuSoundVolume);

    private void ResolveReferences()
    {
        if (btnBack == null)
            btnBack = FindComponent<Button>("btnBack");

        if (masterVolumeSlider == null)
            masterVolumeSlider = FindComponent<Slider>("MASTER VOLUME Row/Slider");
        if (musicVolumeSlider == null)
            musicVolumeSlider = FindComponent<Slider>("MUSIC VOLUME Row/Slider");
        if (gameSoundSlider == null)
            gameSoundSlider = FindComponent<Slider>("GAME SOUND Row/Slider");
        if (menuSoundSlider == null)
            menuSoundSlider = FindComponent<Slider>("MENU SOUND Row/Slider");

        if (masterVolumeValue == null)
            masterVolumeValue = FindComponent<TMP_Text>("MASTER VOLUME Row/Value");
        if (musicVolumeValue == null)
            musicVolumeValue = FindComponent<TMP_Text>("MUSIC VOLUME Row/Value");
        if (gameSoundValue == null)
            gameSoundValue = FindComponent<TMP_Text>("GAME SOUND Row/Value");
        if (menuSoundValue == null)
            menuSoundValue = FindComponent<TMP_Text>("MENU SOUND Row/Value");
    }

    private void RegisterListeners()
    {
        if (btnBack != null)
        {
            btnBack.onClick.RemoveListener(OnClickButtonBack);
            btnBack.onClick.AddListener(OnClickButtonBack);
        }

        RegisterSlider(masterVolumeSlider, OnMasterVolumeChanged);
        RegisterSlider(musicVolumeSlider, OnMusicVolumeChanged);
        RegisterSlider(gameSoundSlider, OnGameSoundChanged);
        RegisterSlider(menuSoundSlider, OnMenuSoundChanged);
    }

    private void UnregisterListeners()
    {
        if (btnBack != null)
            btnBack.onClick.RemoveListener(OnClickButtonBack);

        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        if (gameSoundSlider != null)
            gameSoundSlider.onValueChanged.RemoveListener(OnGameSoundChanged);
        if (menuSoundSlider != null)
            menuSoundSlider.onValueChanged.RemoveListener(OnMenuSoundChanged);
    }

    private static void RegisterSlider(Slider slider, UnityEngine.Events.UnityAction<float> listener)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.interactable = true;
        slider.onValueChanged.RemoveListener(listener);
        slider.onValueChanged.AddListener(listener);
    }

    private void LoadSavedValues()
    {
        SetSliderValue(masterVolumeSlider, MasterVolume);
        SetSliderValue(musicVolumeSlider, MusicVolume);
        SetSliderValue(gameSoundSlider, GameSoundVolume);
        SetSliderValue(menuSoundSlider, MenuSoundVolume);

        ApplyMasterVolume(MasterVolume);
        RefreshValueLabels();
    }

    private void SaveValues()
    {
        if (masterVolumeSlider != null)
            PlayerPrefs.SetFloat(MasterVolumeKey, masterVolumeSlider.value);
        if (musicVolumeSlider != null)
            PlayerPrefs.SetFloat(MusicVolumeKey, musicVolumeSlider.value);
        if (gameSoundSlider != null)
            PlayerPrefs.SetFloat(GameSoundVolumeKey, gameSoundSlider.value);
        if (menuSoundSlider != null)
            PlayerPrefs.SetFloat(MenuSoundVolumeKey, menuSoundSlider.value);

        PlayerPrefs.Save();
    }

    private static void SetSliderValue(Slider slider, float value)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
    }

    private void OnMasterVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
        ApplyMasterVolume(value);
        RefreshValueLabels();
    }

    private void OnMusicVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
        RefreshValueLabels();
    }

    private void OnGameSoundChanged(float value)
    {
        PlayerPrefs.SetFloat(GameSoundVolumeKey, Mathf.Clamp01(value));
        RefreshValueLabels();
    }

    private void OnMenuSoundChanged(float value)
    {
        PlayerPrefs.SetFloat(MenuSoundVolumeKey, Mathf.Clamp01(value));
        RefreshValueLabels();
    }

    private static void ApplyMasterVolume(float value)
    {
        AudioListener.volume = Mathf.Clamp01(value);
    }

    private void RefreshValueLabels()
    {
        SetValueLabel(masterVolumeValue, masterVolumeSlider);
        SetValueLabel(musicVolumeValue, musicVolumeSlider);
        SetValueLabel(gameSoundValue, gameSoundSlider);
        SetValueLabel(menuSoundValue, menuSoundSlider);
    }

    private static void SetValueLabel(TMP_Text label, Slider slider)
    {
        if (label != null && slider != null)
            label.text = $"{Mathf.RoundToInt(slider.value * 100f)}%";
    }

    private T FindComponent<T>(string path) where T : Component
    {
        Transform child = transform.Find(path);
        return child != null ? child.GetComponent<T>() : null;
    }
}
