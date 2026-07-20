using UnityEngine;
using UnityEngine.UI;

public class UISettings : MonoBehaviour
{
    [Header("Pannel")]
    [SerializeField] private GameObject panelSettings;

    [Header("Button")]
    [SerializeField] private Button btnBack;

    private void Awake()
    {
        if(btnBack != null)
        {
            btnBack.onClick.AddListener(() =>
            {
                OnClickButtonBack();
            });
        }
    }

    public void OnClickButtonBack()
    {
        panelSettings.SetActive(false);
    }
}
