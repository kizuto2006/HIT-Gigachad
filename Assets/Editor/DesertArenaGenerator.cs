using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

public class DesertArenaGenerator : Editor
{
    const string MAT_DIR    = "Assets/DesertArena_Materials";
    const string TD_PATH    = "Assets/DesertArena_TerrainData.asset";

    [MenuItem("Tools/Convert Ground to Flat Terrain")]
    public static void Generate()
    {
        if (!EditorUtility.DisplayDialog("Convert Ground to Terrain",
            "This will find 'Environment/Ground' in the current scene and replace it with a flat Terrain matching the original color.\nContinue?",
            "Convert", "Cancel")) return;

        try
        {
            EnsureDir(MAT_DIR);

            EditorUtility.DisplayProgressBar("Terrain Conversion", "Writing textures...", 0.3f);
            WriteTextures();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            EditorUtility.DisplayProgressBar("Terrain Conversion", "Replacing Ground with Terrain...", 0.6f);
            ReplaceGroundWithTerrain();

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            EditorUtility.ClearProgressBar();
            Debug.Log("✅ Ground replaced with flat Terrain.");
            EditorUtility.DisplayDialog("Done!", "Ground replaced with a flat Terrain!", "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("Generator failed: " + e);
        }
    }

    static void EnsureDir(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(
                Path.GetDirectoryName(path).Replace("\\", "/"),
                Path.GetFileName(path));
    }

    static void WriteTextures()
    {
        WritePNG("ExactSand", new Color(1.000f, 0.800f, 0.502f, 1.000f), 64);
    }

    static void WritePNG(string id, Color col, int size)
    {
        string path = MAT_DIR + "/Tex_" + id + ".png";
        if (File.Exists(path)) File.Delete(path);

        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, col);
            }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static TerrainLayer TL(string id, float tile)
    {
        string p = MAT_DIR + "/TL_" + id + ".terrainlayer";
        TerrainLayer tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(p);
        if (tl == null) { tl = new TerrainLayer(); AssetDatabase.CreateAsset(tl, p); }

        tl.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(MAT_DIR + "/Tex_" + id + ".png");
        tl.tileSize = new Vector2(tile, tile);
        EditorUtility.SetDirty(tl);
        return tl;
    }

    static void ReplaceGroundWithTerrain()
    {
        GameObject env = GameObject.Find("Environment");
        if (env == null) {
            Debug.LogError("Could not find 'Environment' object.");
            return;
        }
        
        GameObject groundGroup = null;
        foreach (Transform child in env.transform) {
            if (child.name == "Ground") {
                groundGroup = child.gameObject;
                break;
            }
        }
        if (groundGroup == null) {
            Debug.LogError("Could not find 'Ground' inside 'Environment'.");
            return;
        }

        var renderers = groundGroup.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        
        Bounds bounds = renderers[0].bounds;
        for (int i=1; i<renderers.Length; i++) {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float width = bounds.size.x;
        float length = bounds.size.z;
        float height = bounds.size.y;
        
        float tSize = Mathf.Max(width, length);
        if (tSize < 10) tSize = 100;

        TerrainData td = new TerrainData();
        td.heightmapResolution = 129;
        td.size = new Vector3(tSize, Mathf.Max(10f, height * 2f), tSize);
        td.alphamapResolution = 128;
        td.SetDetailResolution(128, 8);

        td.terrainLayers = new TerrainLayer[] {
            TL("ExactSand", 15f)
        };

        // Put the flat terrain at Y = 0 (or bounds.min.y if it's completely different)
        float flatHeight = 0f;
        Vector3 startPos = new Vector3(bounds.center.x - tSize/2f, flatHeight, bounds.center.z - tSize/2f);

        var old = AssetDatabase.LoadAssetAtPath<TerrainData>(TD_PATH);
        if (old) AssetDatabase.DeleteAsset(TD_PATH);
        AssetDatabase.CreateAsset(td, TD_PATH);
        AssetDatabase.SaveAssets();

        GameObject go = Terrain.CreateTerrainGameObject(td);
        go.name = "Terrain_Ground";
        go.transform.parent = env.transform;
        
        go.transform.position = startPos;

        Terrain t = go.GetComponent<Terrain>();
        t.drawInstanced = true;

        Object.DestroyImmediate(groundGroup);
    }
}
