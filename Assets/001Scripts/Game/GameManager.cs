using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Owns the runtime game flow after the player enters a gameplay scene.
/// The instance survives the transition back to the start scene so the
/// death screen can safely request a clean restart.
/// </summary>
public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Scenes")]
    [SerializeField] private string startSceneName = "GigabonkMenu";
    [SerializeField] private string desertSceneName = "DesertArena";

    [Header("Game Over")]
    [SerializeField] private GameObject deadCanvasPrefab;

    private PlayerHealth playerHealth;
    private PlayerCurrency playerCurrency;
    private GameObject deadCanvasInstance;
    private bool handlingDeath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != "DesertArena")
            return;

        if (Instance != null || FindFirstObjectByType<GameManager>() != null)
            return;

        GameObject managerObject = new GameObject("GameManager");
        managerObject.AddComponent<GameManager>();
    }

private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DetachFromParentForPersistence();
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

private void DetachFromParentForPersistence()
    {
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }
    }


    private void Start()
    {
        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void Update()
    {
        if (handlingDeath || SceneManager.GetActiveScene().name != desertSceneName)
            return;

        // Keep a small fallback check for scenes/prefab instances that finish
        // activating the Player after sceneLoaded has already fired.
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.Died += HandlePlayerDied;
        }

        if (playerHealth != null && playerHealth.currentHp <= 0f)
            HandlePlayerDied();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (playerHealth != null)
            playerHealth.Died -= HandlePlayerDied;

        if (Instance == this)
            Instance = null;
    }

private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Time.timeScale = 1f;
        handlingDeath = false;
        if (scene.name == desertSceneName)
        {
            PauseUIController.EnsureForGameplay();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (deadCanvasInstance != null)
        {
            Destroy(deadCanvasInstance);
            deadCanvasInstance = null;
        }

        if (playerHealth != null)
            playerHealth.Died -= HandlePlayerDied;

        playerHealth = null;
        playerCurrency = null;
        if (scene.name == desertSceneName)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
            playerCurrency = FindFirstObjectByType<PlayerCurrency>();
            if (playerHealth != null)
                playerHealth.Died += HandlePlayerDied;
            else
                Debug.LogError("[GameManager] DesertArena has no active PlayerHealth.");
        }
    }

private int AwardRunRewardIfNeeded()
    {
        if (playerCurrency == null)
            playerCurrency = FindFirstObjectByType<PlayerCurrency>();

        if (playerCurrency == null)
            return 0;

        int reward = playerCurrency.AwardRunReward(Time.timeSinceLevelLoad);
        if (reward > 0)
            Debug.Log("[GameManager] Run ended after " + Time.timeSinceLevelLoad.ToString("0.0") + "s; awarded " + reward + " coin(s).");
        return reward;
    }


private void HandlePlayerDied()
    {
        if (handlingDeath)
            return;

        handlingDeath = true;
        MusicAudioManager.Instance?.StopMusic();
        AwardRunRewardIfNeeded();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureEventSystem();
        deadCanvasInstance = GetDeadCanvas();
        if (deadCanvasInstance == null)
        {
            Debug.LogError("[GameManager] Could not get or create DeadCanvas.");
            return;
        }

        deadCanvasInstance.SetActive(false);
        if (deadCanvasInstance.transform is RectTransform canvasTransform)
            canvasTransform.localScale = Vector3.one;

        Canvas canvas = deadCanvasInstance.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
        }

        DeadUIController deadUI = deadCanvasInstance.GetComponent<DeadUIController>();
        if (deadUI == null)
            deadUI = deadCanvasInstance.AddComponent<DeadUIController>();

        deadUI.Configure(ReturnToStartScene);
        deadUI.Show();
    }

    private GameObject GetDeadCanvas()
    {
        // 1. Try to find an existing DeadCanvas in any loaded scene dynamically
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (Canvas c in canvases)
        {
            if (c == null) continue;
            
            GameObject go;
            try { go = c.gameObject; } catch { continue; }
            if (go == null) continue;

            // If it's a scene object (not a prefab asset) and matches the name
            if (go.scene.IsValid() && go.name.Contains("DeadCanvas"))
            {
                return go;
            }
        }

        // 2. Safe check for deadCanvasPrefab
        bool isPrefabAssigned = false;
        try 
        {
            isPrefabAssigned = (deadCanvasPrefab != null); 
        } 
        catch 
        { 
            isPrefabAssigned = false; 
        }

        if (isPrefabAssigned)
        {
            try
            {
                if (deadCanvasPrefab.scene.IsValid())
                {
                    return deadCanvasPrefab;
                }
            }
            catch { }

            try
            {
                UnityEngine.Object rawClone = UnityEngine.Object.Instantiate((UnityEngine.Object)deadCanvasPrefab);
                GameObject clone = rawClone as GameObject;
                if (clone == null && rawClone is Component comp)
                {
                    clone = comp.gameObject;
                }

                if (clone != null)
                    return clone;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[GameManager] Could not instantiate prefab, it might be invalid: " + exception.Message);
            }
        }

        Debug.LogError("[GameManager] DeadCanvas was not found in the scene and prefab could not be used.");
        return null;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

public void ReturnToStartScene()
    {
        if (!handlingDeath && SceneManager.GetActiveScene().name != desertSceneName)
            return;

        if (SceneManager.GetActiveScene().name == desertSceneName)
            AwardRunRewardIfNeeded();

        Time.timeScale = 1f;
        SceneManager.LoadScene(startSceneName);
    }

    public void LoadDesertScene()
    {
        Time.timeScale = 1f;
        MusicAudioManager.Instance?.StopMenuMusic();
        SceneManager.LoadScene(desertSceneName);
    }
}
