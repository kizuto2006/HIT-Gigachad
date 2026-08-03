using UnityEngine;
using UnityEngine.UI;

public class StartUI : MonoBehaviour
{
    [Header("UI Pannel")]
    [SerializeField] private SelectCharacterUI characterUI;
    [SerializeField] private SelectMapUI mapUI;

    [Header("Panel")]
    [SerializeField] private GameObject startPanel;

    [Header("Start Button")]
    [SerializeField] private Button btnPlay;
    [SerializeField] private Button btnSettings;
    [SerializeField] private Button btnExit;

    private GameObject menuFrame;

    private void Awake()
    {
        menuFrame = GameObject.Find("GigabonkMenuPanel");
        
        if(btnPlay != null)
        {
            btnPlay.onClick.AddListener(() =>
            {
                OnClickButtonPlay();
            });
        }
        if(btnSettings != null)
        {
            btnSettings.onClick.AddListener(() =>
            {
                OnClickButtonSettings();
            });
        }
        if(btnExit != null)
        {
            btnExit.onClick.AddListener(() =>
            {
                OnClickButtonExit();
            });
        }
    }


    public void OnClickButtonPlay()
    {
        var menuFlow = FindFirstObjectByType<GigabonkMenuFlow>();
        if(menuFlow != null)
        {
            menuFlow.BeginPlay();
            return;
        }

        SetActiveStartPanel(false);
        UIController.Instance.SelectCharacterUI.SetActiveCharacter(true);
    }
    public void OnClickButtonExit()
    {
        Application.Quit();
    }
    public void OnClickButtonSettings()
    {
        SetActiveStartPanel(false);
        UIController.Instance.UISettings.gameObject.SetActive(true);
    }
    public void SetActiveStartPanel(bool isActive)
    {
        if(startPanel != null)
            startPanel.SetActive(isActive);

        if(menuFrame != null)
            menuFrame.SetActive(isActive);
    }
}
