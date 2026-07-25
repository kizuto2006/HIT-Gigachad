using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectCharacterUI : MonoBehaviour 
{
    [Header("Panel")]
    [SerializeField] private GameObject panelSelectCharacter;

    [Header("Button")]
    [SerializeField] private Button btnConfirm;
    [SerializeField] private Button btnBack;

    [Header("Info Charactor")]
    [SerializeField] private Sprite Icon;
    [SerializeField] private TextMeshProUGUI textName;
    [SerializeField] private TextMeshProUGUI textDescription;


    private void Awake()
    {
        if(btnConfirm != null)
        {
            btnConfirm.onClick.AddListener(() =>
            {
                OnClickButonConfirm();
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

    public void OnClickButonConfirm()
    {
        panelSelectCharacter.SetActive(false);
        UIController.Instance.SelectMapUI.SetActiveMap(true);
    }

    public void OnClickBack()
    {
        panelSelectCharacter.SetActive(false);
        UIController.Instance.StartUI.SetActiveStartPanel(true);
    }

    public void SetActiveCharacter(bool active)
    {
        panelSelectCharacter.SetActive(active);
    }


}
