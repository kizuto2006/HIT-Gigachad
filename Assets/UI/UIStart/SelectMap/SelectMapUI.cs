using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SelectMapUI : MonoBehaviour 
{
    [Header("Panel")]
    [SerializeField] private GameObject panelSelectMap;

    [Header("Button")]
    [SerializeField] private Button btnConfirm;
    [SerializeField] private Button btnBack;

    private void Awake()
    {
        if (btnConfirm != null)
        {
            btnConfirm.onClick.AddListener(() =>
            {
                OnClickConfirm();
            });
        }
        if (btnBack != null)
        {
            btnBack.onClick.AddListener(() =>
            {
                OnClickBack();
            });
        }
    }


    public void OnClickConfirm()
    {
        var menuFlow = FindFirstObjectByType<GigabonkMenuFlow>();
        if(menuFlow != null)
            menuFlow.ExitMenu();

        MusicAudioManager.Instance?.StopMenuMusic();
        SceneManager.LoadScene("DesertArena");
    }

    public void OnClickBack()
    {
        panelSelectMap.SetActive(false);
        UIController.Instance.SelectCharacterUI.SetActiveCharacter(true);
    }

    public void SetActiveMap(bool active)
    {
        panelSelectMap.SetActive(active);
    }
}
