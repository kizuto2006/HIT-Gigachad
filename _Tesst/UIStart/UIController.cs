using Unity.VisualScripting;
using UnityEngine;

public class UIController : MonoBehaviour
{
    public static UIController Instance;

    [Header("Script UI")]
    [SerializeField] private StartUI startUI;
    [SerializeField] private SelectCharacterUI selectCharacterUI;
    [SerializeField] private SelectMapUI selectMapUI;
    [SerializeField] private UISettings settingsUI;

    public StartUI StartUI { get { return startUI; } }
    public SelectCharacterUI SelectCharacterUI { get { return selectCharacterUI; } }
    public SelectMapUI SelectMapUI { get { return selectMapUI; } }
    public UISettings UISettings { get { return settingsUI; } }

    private void Awake()
    {
        if(Instance == null) 
            Instance = this;

        StartGameUI();
    }

    
    public void StartGameUI()        
    {
        startUI.SetActiveStartPanel(true);
        selectCharacterUI.SetActiveCharacter(false);
        selectMapUI.SetActiveMap(false);
        settingsUI.gameObject.SetActive(false);
    }
}
