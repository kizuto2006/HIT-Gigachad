using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UISettings : MonoBehaviour
{
    [Header("Pannel")]
    [SerializeField] private GameObject panelSettings;

    [Header("Button")]
    [SerializeField] private Button btnBack;
    [SerializeField] private Button btnFullscreen;
    [SerializeField] private Button btnQuality;
    [SerializeField] private Button btnVSync;

    [Header("Value labels")]
    [SerializeField] private TextMeshProUGUI fullscreenValue;
    [SerializeField] private TextMeshProUGUI qualityValue;
    [SerializeField] private TextMeshProUGUI vSyncValue;

    private void Awake()
    {
        if(btnBack != null)
        {
            btnBack.onClick.AddListener(() =>
            {
                OnClickButtonBack();
            });
        }

        if (btnFullscreen != null)
            btnFullscreen.onClick.AddListener(ToggleFullscreen);

        if (btnQuality != null)
            btnQuality.onClick.AddListener(CycleQuality);

        if (btnVSync != null)
            btnVSync.onClick.AddListener(ToggleVSync);

        RefreshLabels();
    }

    public void OnClickButtonBack()
    {
        panelSettings.SetActive(false);

        if(UIController.Instance != null && UIController.Instance.StartUI != null)
            UIController.Instance.StartUI.SetActiveStartPanel(true);
    }

    public void ToggleFullscreen()
    {
        Screen.fullScreen = !Screen.fullScreen;
        PlayerPrefs.SetInt("Fullscreen", Screen.fullScreen ? 1 : 0);
        RefreshLabels();
    }

    public void CycleQuality()
    {
        int nextLevel = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length;
        QualitySettings.SetQualityLevel(nextLevel, true);
        PlayerPrefs.SetInt("QualityLevel", nextLevel);
        RefreshLabels();
    }

    public void ToggleVSync()
    {
        QualitySettings.vSyncCount = QualitySettings.vSyncCount == 0 ? 1 : 0;
        PlayerPrefs.SetInt("VSync", QualitySettings.vSyncCount);
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (fullscreenValue != null)
            fullscreenValue.text = Screen.fullScreen ? "BẬT" : "TẮT";

        if (qualityValue != null)
            qualityValue.text = QualitySettings.names[QualitySettings.GetQualityLevel()].ToUpperInvariant();

        if (vSyncValue != null)
            vSyncValue.text = QualitySettings.vSyncCount > 0 ? "BẬT" : "TẮT";
    }
}
