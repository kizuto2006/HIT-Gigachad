using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Linq;

public class StyleBorders
{
    [MenuItem("Tools/Style Selected UI Trees")]
    public static void ApplyStyleToSelectedTrees()
    {
        string[] targetNames = new string[] 
        {
            "Border", "BackGroundCharacter", "BackGroundMap ", "BackGroundMap", 
            "BackGroundInfo ", "BackGroundInfo", "BG"
        };

        int count = 0;
        foreach (GameObject root in Selection.gameObjects)
        {
            Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allTransforms)
            {
                if (targetNames.Contains(t.name) || targetNames.Contains(t.name + " "))
                {
                    Image img = t.GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = new Color(0.4f, 0.4f, 0.4f, 1f);
                        
                        Outline outline = t.GetComponent<Outline>();
                        if (outline == null)
                        {
                            outline = t.gameObject.AddComponent<Outline>();
                        }
                        outline.effectColor = Color.black;
                        outline.effectDistance = new Vector2(4, -4);
                        
                        count++;
                        EditorUtility.SetDirty(t.gameObject);
                    }
                }
            }
        }
        
        Debug.Log($"Applied style to {count} objects in the selected trees.");
    }
}
