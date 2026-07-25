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
        SceneManager.LoadScene(1);
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
