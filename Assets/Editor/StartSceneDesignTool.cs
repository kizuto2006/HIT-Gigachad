using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

public class StartSceneDesignTool : EditorWindow
{
    [MenuItem("Gigachad/Apply Start UI Design")]
    public static void ApplyDesign()
    {
        Scene startScene = SceneManager.GetActiveScene();
        if (!startScene.name.Equals("Start") && !EditorUtility.DisplayDialog("Warning", "You are not in the 'Start' scene. Do you want to apply this design to the current scene anyway?", "Yes", "No"))
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Apply Start UI Design");

        // 1. Style Buttons
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        int buttonCount = 0;
        foreach (Button btn in buttons)
        {
            // Skip buttons that are not in the active scene or are prefabs
            if (btn.gameObject.scene != startScene) continue;

            Undo.RecordObject(btn.gameObject, "Style Button");
            
            // Change Image Color
            Image img = btn.GetComponent<Image>();
            if (img != null)
            {
                Undo.RecordObject(img, "Change Button Color");
                ColorUtility.TryParseHtmlString("#4F4F4F", out Color darkGrey);
                img.color = darkGrey;
            }

            // Add or modify Outline
            Outline outline = btn.GetComponent<Outline>();
            if (outline == null)
            {
                outline = Undo.AddComponent<Outline>(btn.gameObject);
            }
            else
            {
                Undo.RecordObject(outline, "Change Outline");
            }
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(4, -4);

            // Change Text Font
            TMP_Text tmpText = btn.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
            {
                Undo.RecordObject(tmpText, "Change TMP Text");
                tmpText.color = Color.white;
                TMP_FontAsset pixelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Fonts/SVN-Determination Sans SDF.asset");
                if (pixelFont != null)
                {
                    tmpText.font = pixelFont;
                }
            }
            else
            {
                Text uiText = btn.GetComponentInChildren<Text>(true);
                if (uiText != null)
                {
                    Undo.RecordObject(uiText, "Change UI Text");
                    uiText.color = Color.white;
                    // For legacy text, we need a .ttf font. If we can't find one, we just change the color and style.
                    uiText.fontStyle = FontStyle.Bold;
                    uiText.fontSize = 40; // Make it bigger if it was default
                }
            }
            buttonCount++;
        }
        
        Debug.Log($"Applied pixel design to {buttonCount} buttons.");

        // 2. Setup Background from DesertArena
        SetupDesertBackground(startScene);

        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        EditorSceneManager.MarkSceneDirty(startScene);
        Debug.Log("Start UI Design applied successfully!");
    }

    private static void SetupDesertBackground(Scene startScene)
    {
        // Check if we already have a background
        GameObject existingBg = GameObject.Find("DesertBackground");
        if (existingBg != null)
        {
            if (EditorUtility.DisplayDialog("Background Exists", "A 'DesertBackground' object already exists. Do you want to recreate it?", "Yes", "No"))
            {
                Undo.DestroyObjectImmediate(existingBg);
            }
            else
            {
                return;
            }
        }

        string desertScenePath = "Assets/Scenes/DesertArena.unity";
        if (!System.IO.File.Exists(desertScenePath))
        {
            Debug.LogError($"Could not find scene at {desertScenePath}");
            return;
        }

        // Additive load the desert scene
        Scene desertScene = EditorSceneManager.OpenScene(desertScenePath, OpenSceneMode.Additive);

        GameObject bgContainer = new GameObject("DesertBackground");
        Undo.RegisterCreatedObjectUndo(bgContainer, "Create Desert Background");
        SceneManager.MoveGameObjectToScene(bgContainer, startScene);

        // Find root objects in the Desert Arena scene
        GameObject[] rootObjects = desertScene.GetRootGameObjects();
        foreach (GameObject go in rootObjects)
        {
            // Ignore UI, EventSystem, Cameras, Lights (so we don't mess up the Start scene lighting if it already has one, or maybe we DO want the light?)
            // We'll ignore Camera, Canvas, EventSystem, Player, EnemySpawn
            string name = go.name.ToLower();
            if (name.Contains("camera") || name.Contains("canvas") || name.Contains("eventsystem") || name.Contains("player") || name.Contains("enemy"))
            {
                continue;
            }

            // Duplicate the object
            GameObject copy = Object.Instantiate(go);
            copy.name = go.name;
            Undo.RegisterCreatedObjectUndo(copy, "Copy Environment Object");
            
            // Move to the Start scene and parent it to our container
            SceneManager.MoveGameObjectToScene(copy, startScene);
            copy.transform.SetParent(bgContainer.transform);
        }

        // Also copy the lighting settings (skybox)
        Material desertSkybox = RenderSettings.skybox;
        
        // Close the Desert scene without saving
        EditorSceneManager.CloseScene(desertScene, true);

        // Apply skybox to Start scene
        if (desertSkybox != null)
        {
            RenderSettings.skybox = desertSkybox;
        }

        // Adjust position of background so it sits nicely behind the UI
        // Move it down a bit so the ground is visible
        bgContainer.transform.position = new Vector3(0, -2f, 15f);
        
        // If the Canvas is Screen Space - Overlay, the 3D background will naturally be behind it.
        // We will make sure there is a camera.
        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.Skybox;
        }

        Debug.Log("Desert Background setup complete.");
    }
}
