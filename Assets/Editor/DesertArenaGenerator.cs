using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Desert Arena Generator
/// Menu: Tools > Generate Desert Arena Map
/// </summary>
public class DesertArenaGenerator : Editor
{
    // ─── Terrain ──────────────────────────────────────────────
    const int T_SIZE      = 1000;
    const int T_HEIGHT    = 160;
    const int HM_RES      = 513;
    const int AM_RES      = 512;

    // ─── Population ───────────────────────────────────────────
    const int CACTUS_COUNT    = 300;
    const int ROCK_COUNT      = 150;
    const int BIG_ROCK_COUNT  = 20;
    const int HOUSE_COUNT     = 20;
    const int DEAD_TREE_COUNT = 80;
    const int GIANT_TREE_COUNT= 15;
    const int POT_COUNT       = 150;
    const int SHRUB_COUNT     = 200;
    const int STATUE_COUNT    = 15;

    // ─── Paths ────────────────────────────────────────────────
    const string SCENE_PATH = "Assets/Scenes/DesertArena.unity";
    const string MAT_DIR    = "Assets/DesertArena_Materials";
    const string MESH_DIR   = "Assets/DesertArena_Meshes";
    const string TD_PATH    = "Assets/DesertArena_TerrainData.asset";

    // ─── Wall Settings ────────────────────────────────────────
    const float WALL_HEIGHT     = 40f;
    const float WALL_THICKNESS  = 6f;

    // ─── Cached ───────────────────────────────────────────────
    static Material mCactus, mStone, mSandstone, mWood, mDarkWood, mDeadTree, mPot, mShrub;
    static Mesh meshCactus, meshRock, meshHouse, meshWall, meshDeadTree, meshPot, meshShrub, meshStatue;

    static float[,] s_hm;

    // ══════════════════════════════════════════════════════════
    [MenuItem("Tools/Generate Desert Arena Map")]
    public static void Generate()
    {
        if (!EditorUtility.DisplayDialog("Generate Desert Arena",
            "Creates DesertArena.unity with desert theme.\nContinue?",
            "Generate", "Cancel")) return;

        try
        {
            EnsureDir(MAT_DIR);
            EnsureDir(MESH_DIR);

            Bar("Writing textures...", 0.05f);
            WriteTextures();

            Bar("Importing...", 0.1f);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Bar("Materials...", 0.15f);
            BuildMaterials();
            BuildMeshes();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Bar("Terrain...", 0.25f);
            GameObject terr = BuildTerrain();

            Bar("Cacti...", 0.4f);
            SpawnCacti(terr);

            Bar("Rocks...", 0.5f);
            SpawnRocks(terr);

            Bar("Houses...", 0.6f);
            SpawnHouses(terr);

            Bar("Decorations...", 0.65f);
            SpawnGeneric(terr, "BigRocks", "BigRock", BIG_ROCK_COUNT, meshRock, mStone, 3.0f, 7.0f, false);
            SpawnGeneric(terr, "GiantTrees", "GiantTree", GIANT_TREE_COUNT, meshDeadTree, mDeadTree, 3.5f, 6.0f, true);
            SpawnGeneric(terr, "Statues", "Statue", STATUE_COUNT, meshStatue, mSandstone, 1.5f, 3.0f, true);
            SpawnGeneric(terr, "DeadTrees", "DeadTree", DEAD_TREE_COUNT, meshDeadTree, mDeadTree, 1.0f, 2.5f, true);
            SpawnGeneric(terr, "Pots", "Pot", POT_COUNT, meshPot, mPot, 0.4f, 1.0f, true);
            SpawnGeneric(terr, "Shrubs", "Shrub", SHRUB_COUNT, meshShrub, mShrub, 0.6f, 1.5f, true);

            Bar("Lighting...", 0.85f);
            SetupDesertLighting();

            Bar("Atmosphere...", 0.9f);
            SpawnParticles();

            Bar("Camera...", 0.95f);
            SetupCamera();

            Bar("Saving...", 0.98f);
            EditorSceneManager.SaveScene(scene, SCENE_PATH);

            EditorUtility.ClearProgressBar();
            Debug.Log("✅ Desert Arena → " + SCENE_PATH);
            EditorUtility.DisplayDialog("Done!", "Desert Arena created!\n" + SCENE_PATH, "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("Generator failed: " + e);
            throw;
        }
    }

    static void Bar(string msg, float p) => EditorUtility.DisplayProgressBar("Desert Arena", msg, p);

    static void EnsureDir(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(
                Path.GetDirectoryName(path).Replace("\\", "/"),
                Path.GetFileName(path));
    }

    // ══════════════════════════════════════════════════════════
    // TEXTURES — Sand variations
    // ══════════════════════════════════════════════════════════
    static void WriteTextures()
    {
        WritePNG("SandBase", new Color(0.85f, 0.75f, 0.55f), 64);
        WritePNG("SandDark", new Color(0.75f, 0.65f, 0.45f), 64);
        WritePNG("DryDirt",  new Color(0.65f, 0.55f, 0.40f), 64);
    }

    static void WritePNG(string id, Color col, int size)
    {
        string path = MAT_DIR + "/Tex_" + id + ".png";
        if (File.Exists(path)) File.Delete(path);

        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        int seed = id.GetHashCode();
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.15f + seed, y * 0.15f + seed) * 0.08f - 0.04f;
                tex.SetPixel(x, y, new Color(
                    Mathf.Clamp01(col.r + n),
                    Mathf.Clamp01(col.g + n),
                    Mathf.Clamp01(col.b + n * 0.5f)));
            }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    // ══════════════════════════════════════════════════════════
    // MATERIALS
    // ══════════════════════════════════════════════════════════
    static void BuildMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) lit = Shader.Find("Standard");

        mCactus    = M("Cactus",    lit, new Color(0.35f, 0.55f, 0.25f), 0.1f);
        mStone     = M("Rock",      lit, new Color(0.65f, 0.55f, 0.45f), 0.05f);
        mSandstone = M("Sandstone", lit, new Color(0.80f, 0.68f, 0.50f), 0.1f);
        mWood      = M("Wood",      lit, new Color(0.40f, 0.28f, 0.18f), 0.05f);
        mDarkWood  = M("DarkWood",  lit, new Color(0.20f, 0.14f, 0.09f), 0.05f);
        mDeadTree  = M("DeadTree",  lit, new Color(0.35f, 0.30f, 0.25f), 0.05f);
        mPot       = M("Pot",       lit, new Color(0.65f, 0.40f, 0.20f), 0.2f);
        mShrub     = M("Shrub",     lit, new Color(0.55f, 0.50f, 0.30f), 0.0f);

        AssetDatabase.SaveAssets();
    }

    static Material M(string n, Shader s, Color c, float sm)
    {
        string p = MAT_DIR + "/" + n + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(s); m.name = n; AssetDatabase.CreateAsset(m, p); }
        m.shader = s;
        m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        m.SetFloat("_Smoothness", sm);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ══════════════════════════════════════════════════════════
    // MESHES
    // ══════════════════════════════════════════════════════════
    static void BuildMeshes()
    {
        meshCactus   = SM("Cactus",   BuildCactusMesh());
        meshRock     = SM("Rock",     MakeRock(1f));
        meshHouse    = SM("House",    BuildAdobeHouseMesh());
        meshWall     = SM("Wall",     BuildWallMesh());
        meshDeadTree = SM("DeadTree", BuildDeadTreeMesh());
        meshPot      = SM("Pot",      BuildPotMesh());
        meshShrub    = SM("Shrub",    BuildShrubMesh());
        meshStatue   = SM("Statue",   BuildStatueMesh());
    }

    static Mesh SM(string id, Mesh src)
    {
        string p = MESH_DIR + "/" + id + ".asset";
        Mesh ex = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (ex != null) { AssetDatabase.DeleteAsset(p); }
        src.name = id;
        AssetDatabase.CreateAsset(src, p);
        return src;
    }

    // ══════════════════════════════════════════════════════════
    // HEIGHTMAP
    // ══════════════════════════════════════════════════════════
    static float[,] GenHeightmap()
    {
        float[,] h = new float[HM_RES, HM_RES];
        for (int y = 0; y < HM_RES; y++)
        for (int x = 0; x < HM_RES; x++)
        {
            float nx = (float)x / (HM_RES - 1);
            float ny = (float)y / (HM_RES - 1);

            // Massive Sharp Dunes (Ridged Multifractal style)
            float dune1Raw = Mathf.PerlinNoise(nx * 2.5f, ny * 2.5f) * 2f - 1f;
            float dune1 = Mathf.Pow(1f - Mathf.Abs(dune1Raw), 2.5f) * 0.22f; // Reduced depth

            float dune2Raw = Mathf.PerlinNoise(nx * 5f + 100, ny * 5f + 100) * 2f - 1f;
            float dune2 = Mathf.Pow(1f - Mathf.Abs(dune2Raw), 2f) * 0.08f;

            float microBumps = Mathf.PerlinNoise(nx * 30f + 200, ny * 30f + 200) * 0.01f;
            
            float bh = dune1 + dune2 + microBumps;

            // Distance from center
            float cx = nx - 0.5f, cz = ny - 0.5f;
            float dist = Mathf.Sqrt(cx * cx + cz * cz);
            
            // Minimal flattening to let the hills roll everywhere
            float arena = Mathf.Clamp01(1f - Mathf.InverseLerp(0.0f, 0.2f, dist));
            float baseHeight = Mathf.Max(0.02f, bh * (1f - arena * 0.15f));

            // Canyon walls at the edges
            float canyon = 0f;
            if (dist > 0.35f) {
                float edgeFactor = Mathf.Clamp01((dist - 0.35f) / 0.15f); // 0 at 0.35, 1 at 0.5
                float ruggedNoise = Mathf.PerlinNoise(nx * 15f, ny * 15f) * 0.2f + 0.8f;
                canyon = Mathf.Pow(edgeFactor, 1.8f) * ruggedNoise;
            }

            h[y,x] = Mathf.Clamp01(baseHeight + canyon);
        }
        return h;
    }

    // ══════════════════════════════════════════════════════════
    // TERRAIN
    // ══════════════════════════════════════════════════════════
    static GameObject BuildTerrain()
    {
        s_hm = GenHeightmap();

        TerrainData td = new TerrainData();
        td.heightmapResolution = HM_RES;
        td.size = new Vector3(T_SIZE, T_HEIGHT, T_SIZE);
        td.alphamapResolution = AM_RES;
        td.SetDetailResolution(256, 16);
        td.SetHeights(0, 0, s_hm);

        td.terrainLayers = new TerrainLayer[] {
            TL("SandBase", 15f),
            TL("SandDark", 10f),
            TL("DryDirt",   8f),
        };

        td.SetAlphamaps(0, 0, PaintSplat(s_hm));

        var old = AssetDatabase.LoadAssetAtPath<TerrainData>(TD_PATH);
        if (old) AssetDatabase.DeleteAsset(TD_PATH);
        AssetDatabase.CreateAsset(td, TD_PATH);
        AssetDatabase.SaveAssets();

        GameObject go = Terrain.CreateTerrainGameObject(td);
        go.name = "Terrain";
        go.transform.position = new Vector3(-T_SIZE*0.5f, 0, -T_SIZE*0.5f);

        Terrain t = go.GetComponent<Terrain>();
        t.drawInstanced = true;
        t.heightmapPixelError = 5;
        t.basemapDistance = 1000f;

        return go;
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

    static float[,,] PaintSplat(float[,] hm)
    {
        float[,,] sp = new float[AM_RES, AM_RES, 3];
        for (int ay = 0; ay < AM_RES; ay++)
        for (int ax = 0; ax < AM_RES; ax++)
        {
            float nx = (float)ax / AM_RES;
            float ny = (float)ay / AM_RES;
            
            int hx = Mathf.Clamp(Mathf.RoundToInt(nx*(HM_RES-1)), 1, HM_RES-2);
            int hy = Mathf.Clamp(Mathf.RoundToInt(ny*(HM_RES-1)), 1, HM_RES-2);
            float h = hm[hy, hx];

            float ddx = (hm[hy,hx+1]-hm[hy,hx-1]) * HM_RES * 0.5f;
            float ddy = (hm[hy+1,hx]-hm[hy-1,hx]) * HM_RES * 0.5f;
            float slope = Mathf.Sqrt(ddx*ddx+ddy*ddy);
            
            float steepDirt = Mathf.Clamp01((slope - 0.4f) * 3f);
            
            // Hard threshold noise for distinct patches (loang lổ rõ)
            float macroNoise = Mathf.PerlinNoise(nx * 4f, ny * 4f);
            float microNoise = Mathf.PerlinNoise(nx * 15f, ny * 15f);
            
            // Use SmoothStep to create hard patches instead of soft gradients
            float darkSandPatch = Mathf.SmoothStep(0.35f, 0.55f, macroNoise);
            float dirtPatch = Mathf.SmoothStep(0.5f, 0.7f, microNoise);

            float darkSand = Mathf.Clamp01((h - 0.1f) + darkSandPatch * 0.8f) * (1f - steepDirt);
            float patchyDirt = dirtPatch * 0.6f * (1f - steepDirt);

            float totalDirt = Mathf.Clamp01(steepDirt + patchyDirt);
            float baseSand = Mathf.Max(0, 1f - darkSand - totalDirt);
            
            float sum = baseSand + darkSand + totalDirt;

            sp[ay,ax,0] = baseSand / sum;
            sp[ay,ax,1] = darkSand / sum;
            sp[ay,ax,2] = totalDirt / sum;
        }
        return sp;
    }



    // ══════════════════════════════════════════════════════════
    // CACTI
    // ══════════════════════════════════════════════════════════
    static void SpawnCacti(GameObject terrObj)
    {
        Terrain t = terrObj.GetComponent<Terrain>();
        TerrainData td = t.terrainData;
        Vector3 tp = terrObj.transform.position;
        System.Random rng = new System.Random(1234);
        GameObject par = new GameObject("=== Cacti ===");

        for (int i = 0; i < CACTUS_COUNT; i++)
        {
            float nx = 0.05f + (float)rng.NextDouble() * 0.9f;
            float nz = 0.05f + (float)rng.NextDouble() * 0.9f;
            float wx = tp.x + nx * td.size.x;
            float wz = tp.z + nz * td.size.z;
            float wy = t.SampleHeight(new Vector3(wx, 0, wz));

            float scale = 0.6f + (float)rng.NextDouble() * 1.5f;

            var cactus = new GameObject("Cactus_" + i);
            cactus.transform.position = new Vector3(wx, wy, wz);
            cactus.transform.rotation = Quaternion.Euler(0, rng.Next(360), 0);
            cactus.transform.localScale = Vector3.one * scale;
            cactus.transform.parent = par.transform;

            cactus.AddComponent<MeshFilter>().sharedMesh = meshCactus;
            cactus.AddComponent<MeshRenderer>().sharedMaterial = mCactus;
            
            var col = cactus.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 1.5f, 0);
            col.height = 3f;
            col.radius = 0.4f;
        }
    }

    // ══════════════════════════════════════════════════════════
    // ROCKS
    // ══════════════════════════════════════════════════════════
    static void SpawnRocks(GameObject terrObj)
    {
        Terrain t = terrObj.GetComponent<Terrain>();
        TerrainData td = t.terrainData;
        Vector3 tp = terrObj.transform.position;
        System.Random rng = new System.Random(5678);
        GameObject par = new GameObject("=== Rocks ===");

        for (int i = 0; i < ROCK_COUNT; i++)
        {
            float nx = 0.05f + (float)rng.NextDouble() * 0.9f;
            float nz = 0.05f + (float)rng.NextDouble() * 0.9f;
            float wx = tp.x + nx * td.size.x;
            float wz = tp.z + nz * td.size.z;
            float wy = t.SampleHeight(new Vector3(wx, 0, wz));
            float s = 1f + (float)rng.NextDouble() * 3f;

            var rock = new GameObject("Rock_" + i);
            rock.AddComponent<MeshFilter>().sharedMesh = meshRock;
            rock.AddComponent<MeshRenderer>().sharedMaterial = mStone;
            var mc = rock.AddComponent<MeshCollider>(); mc.convex = true;
            rock.transform.position = new Vector3(wx, wy - 0.2f * s, wz);
            rock.transform.localScale = new Vector3(s, s * (0.5f + (float)rng.NextDouble() * 0.5f), s);
            rock.transform.rotation = Quaternion.Euler(rng.Next(20), rng.Next(360), rng.Next(20));
            rock.transform.parent = par.transform;
        }
    }

    // ══════════════════════════════════════════════════════════
    // HOUSES
    // ══════════════════════════════════════════════════════════
    static void SpawnHouses(GameObject terrObj)
    {
        Terrain t = terrObj.GetComponent<Terrain>();
        TerrainData td = t.terrainData;
        Vector3 tp = terrObj.transform.position;
        System.Random rng = new System.Random(9012);
        GameObject par = new GameObject("=== Adobe Houses ===");

        int placed = 0;
        int tries = 0;
        while(placed < HOUSE_COUNT && tries < 500)
        {
            tries++;
            float nx = 0.2f + (float)rng.NextDouble() * 0.6f;
            float nz = 0.2f + (float)rng.NextDouble() * 0.6f;
            float wx = tp.x + nx * td.size.x;
            float wz = tp.z + nz * td.size.z;
            float wy = t.SampleHeight(new Vector3(wx, 0, wz));

            // Must be relatively flat
            if (td.GetSteepness(nx, nz) > 10f) continue;

            var house = new GameObject("House_" + placed);
            house.transform.position = new Vector3(wx, wy, wz);
            house.transform.rotation = Quaternion.Euler(0, rng.Next(360), 0);
            house.transform.localScale = Vector3.one * (1.5f + (float)rng.NextDouble() * 0.5f);
            house.transform.parent = par.transform;

            house.AddComponent<MeshFilter>().sharedMesh = meshHouse;
            
            var mr = house.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new Material[] { mSandstone, mWood, mDarkWood };

            var mc = house.AddComponent<MeshCollider>(); mc.convex = true;
            
            placed++;
        }
    }

    // ══════════════════════════════════════════════════════════
    // LIGHTING
    // ══════════════════════════════════════════════════════════
    static void SetupDesertLighting()
    {
        var sun = new GameObject("SunLight");
        var sl = sun.AddComponent<Light>();
        sl.type = LightType.Directional;
        sl.color = new Color(1.0f, 0.9f, 0.7f); // warm desert sun
        sl.intensity = 1.8f;
        sl.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var fill = new GameObject("FillLight");
        var fl = fill.AddComponent<Light>();
        fl.type = LightType.Directional;
        fl.color = new Color(0.8f, 0.6f, 0.4f);
        fl.intensity = 0.4f;
        fl.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(30f, 150f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.8f, 0.7f, 0.5f);
        RenderSettings.ambientEquatorColor = new Color(0.6f, 0.5f, 0.3f);
        RenderSettings.ambientGroundColor  = new Color(0.4f, 0.3f, 0.2f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.8f, 0.7f, 0.5f); // dusty fog
        RenderSettings.fogDensity = 0.002f;
    }

    // ══════════════════════════════════════════════════════════
    // PARTICLES (Dust)
    // ══════════════════════════════════════════════════════════
    static void SpawnParticles()
    {
        var go = new GameObject("DesertDust");
        go.transform.position = new Vector3(0, 5f, 0);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime = 10f;
        main.startSpeed = 1f;
        main.startSize = 0.5f;
        main.startColor = new Color(0.8f, 0.7f, 0.5f, 0.3f);
        main.maxParticles = 500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = 50f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(200f, 10f, 200f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-2f, 2f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); // Fixes the mode mismatch error
        vel.z = new ParticleSystem.MinMaxCurve(1f, 3f); // constant wind

        var col2 = ps.colorOverLifetime;
        col2.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]{new(main.startColor.color, 0), new(main.startColor.color, 1)},
            new GradientAlphaKey[]{new(0,0), new(0.3f,0.2f), new(0.3f,0.8f), new(0,1)});
        col2.color = g;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        Shader pShader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (pShader != null)
        {
            var pm = new Material(pShader);
            if (pm.HasProperty("_Color")) pm.SetColor("_Color", main.startColor.color);
            psr.material = pm;
        }
    }

    // ══════════════════════════════════════════════════════════
    // CAMERA
    // ══════════════════════════════════════════════════════════
    static void SetupCamera()
    {
        var cam = new GameObject("Main Camera");
        cam.tag = "MainCamera";
        var c = cam.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.8f, 0.7f, 0.55f); // dusty sky
        c.fieldOfView = 60;
        c.farClipPlane = 1500f;
        cam.transform.position = new Vector3(0, 40, -60);
        cam.transform.rotation = Quaternion.Euler(20, 0, 0);
        cam.AddComponent<AudioListener>();
    }

    // ══════════════════════════════════════════════════════════
    // PROCEDURAL MESH GENERATORS
    // ══════════════════════════════════════════════════════════

    static void AddIco(List<Vector3> v, List<int> t, Vector3 pos, float r)
    {
        int si = v.Count;
        float t_val = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] baseV = {
            N(-1, t_val, 0), N(1, t_val, 0), N(-1, -t_val, 0), N(1, -t_val, 0),
            N(0, -1, t_val), N(0, 1, t_val), N(0, -1, -t_val), N(0, 1, -t_val),
            N(t_val, 0, -1), N(t_val, 0, 1), N(-t_val, 0, -1), N(-t_val, 0, 1)
        };
        foreach (var vert in baseV) v.Add(pos + vert * r);
        int[] tri = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
        foreach (var idx in tri) t.Add(si + idx);
    }

    static Mesh BuildCactusMesh()
    {
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        int seg = 6;
        float r1 = 0.35f, r2 = 0.25f;

        AddCylinder(v, t, Vector3.zero, r1, 3.0f, seg);
        AddIco(v, t, new Vector3(0, 3.0f, 0), r1);

        AddCylinder(v, t, new Vector3(-r1, 1.4f, 0), r2, 0.8f, seg, Quaternion.Euler(0, 0, 90));
        AddIco(v, t, new Vector3(-r1 - 0.8f, 1.4f, 0), r2);
        AddCylinder(v, t, new Vector3(-r1 - 0.8f, 1.4f, 0), r2, 1.2f, seg);
        AddIco(v, t, new Vector3(-r1 - 0.8f, 2.6f, 0), r2);

        AddCylinder(v, t, new Vector3(r1, 2.0f, 0), r2, 0.7f, seg, Quaternion.Euler(0, 0, -90));
        AddIco(v, t, new Vector3(r1 + 0.7f, 2.0f, 0), r2);
        AddCylinder(v, t, new Vector3(r1 + 0.7f, 2.0f, 0), r2, 1.5f, seg);
        AddIco(v, t, new Vector3(r1 + 0.7f, 3.5f, 0), r2);

        Mesh m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0);
        
        Vector3[] oldV = m.vertices;
        int[] oldT = m.triangles;
        Vector3[] newV = new Vector3[oldT.Length];
        int[] newT = new int[oldT.Length];
        for (int i = 0; i < oldT.Length; i++) {
            newV[i] = oldV[oldT[i]];
            newT[i] = i;
        }
        m.vertices = newV; m.triangles = newT;
        m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }

    static void AddCylinder(List<Vector3> v, List<int> t, Vector3 pos, float r, float h, int seg, Quaternion? rot = null)
    {
        int si = v.Count;
        Quaternion q = rot ?? Quaternion.identity;
        v.Add(pos + q * Vector3.zero);
        v.Add(pos + q * new Vector3(0, h, 0));
        for(int i=0; i<seg; i++) { float a = (float)i/seg * Mathf.PI*2; v.Add(pos + q * new Vector3(Mathf.Cos(a)*r, 0, Mathf.Sin(a)*r)); }
        for(int i=0; i<seg; i++) { float a = (float)i/seg * Mathf.PI*2; v.Add(pos + q * new Vector3(Mathf.Cos(a)*r, h, Mathf.Sin(a)*r)); }
        for(int i=0; i<seg; i++) { int n=(i+1)%seg; t.Add(si); t.Add(si+2+n); t.Add(si+2+i); t.Add(si+1); t.Add(si+2+seg+i); t.Add(si+2+seg+n);
            t.Add(si+2+i); t.Add(si+2+seg+i); t.Add(si+2+n); t.Add(si+2+n); t.Add(si+2+seg+i); t.Add(si+2+seg+n); }
    }

    static Mesh MakeRock(float s)
    {
        float t_val=(1+Mathf.Sqrt(5))/2;
        List<Vector3> v=new List<Vector3>{N(-1,t_val,0)*s,N(1,t_val,0)*s,N(-1,-t_val,0)*s,N(1,-t_val,0)*s,
            N(0,-1,t_val)*s,N(0,1,t_val)*s,N(0,-1,-t_val)*s,N(0,1,-t_val)*s,
            N(t_val,0,-1)*s,N(t_val,0,1)*s,N(-t_val,0,-1)*s,N(-t_val,0,1)*s};
        List<int> tri=new List<int>{0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,
            3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1};
        var rng=new System.Random(77);
        for(int i=0;i<v.Count;i++){var vx=v[i];if(vx.y<0)vx.y*=0.28f;vx+=new Vector3(((float)rng.NextDouble()-0.5f)*s*0.28f,((float)rng.NextDouble()-0.5f)*s*0.14f,((float)rng.NextDouble()-0.5f)*s*0.28f);v[i]=vx;}
        var m=new Mesh();m.SetVertices(v);m.SetTriangles(tri,0);m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    static Vector3 N(float x,float y,float z)=>new Vector3(x,y,z).normalized;

    static Mesh BuildAdobeHouseMesh()
    {
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();

        // Material 0: Adobe walls (Sandstone)
        int start0 = v.Count;
        AB(v, t, new Vector3(0, 1.5f, 0), new Vector3(4, 3, 4)); // Main body
        AB(v, t, new Vector3(1.5f, 1f, 2.5f), new Vector3(2, 2, 2)); // Extension
        
        // Add roof trim
        AB(v, t, new Vector3(0, 3.1f, 0), new Vector3(4.2f, 0.2f, 4.2f));
        AB(v, t, new Vector3(1.5f, 2.1f, 2.5f), new Vector3(2.2f, 0.2f, 2.2f));

        // Submesh 0
        int count0 = t.Count;
        
        // Material 1: Wood details (Poles sticking out)
        int start1 = t.Count;
        AddCylinder(v, t, new Vector3(-2.2f, 2.8f, -1.8f), 0.1f, 4.4f, 4, Quaternion.Euler(0,0,90));
        AddCylinder(v, t, new Vector3(-2.2f, 2.8f,  0.0f), 0.1f, 4.4f, 4, Quaternion.Euler(0,0,90));
        AddCylinder(v, t, new Vector3(-2.2f, 2.8f,  1.8f), 0.1f, 4.4f, 4, Quaternion.Euler(0,0,90));
        int count1 = t.Count - start1;

        // Material 2: Dark Wood (Door/Windows)
        int start2 = t.Count;
        AB(v, t, new Vector3(0, 1f, -2.05f), new Vector3(1.2f, 2f, 0.1f)); // Door
        AB(v, t, new Vector3(-2.05f, 1.5f, 0), new Vector3(0.1f, 0.8f, 0.8f)); // Window
        int count2 = t.Count - start2;

        Mesh m = new Mesh(); m.SetVertices(v);
        m.subMeshCount = 3;
        m.SetTriangles(t.GetRange(0, count0), 0);
        m.SetTriangles(t.GetRange(start1, count1), 1);
        m.SetTriangles(t.GetRange(start2, count2), 2);
        m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }

    static Mesh BuildWallMesh()
    {
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        var rng = new System.Random(111);
        
        float w = 1f, d = 1f, h = 1f; // unit box, scaled in world
        int segs = 6;
        for (int i = 0; i <= segs; i++) {
            float x = ((float)i / segs - 0.5f) * w;
            float th = h + ((float)rng.NextDouble() * 0.1f - 0.05f); // slight height variation
            v.Add(new Vector3(x, -0.5f, d/2)); v.Add(new Vector3(x, th-0.5f, d/2));
            v.Add(new Vector3(x, -0.5f, -d/2)); v.Add(new Vector3(x, th-0.5f, -d/2));
        }
        for (int i = 0; i < segs; i++) {
            int b = i * 4;
            t.AddRange(new[] { b, b+1, b+4, b+1, b+5, b+4, b+2, b+6, b+3, b+3, b+6, b+7, b+1, b+3, b+5, b+3, b+7, b+5 }); // front, back, top
        }
        Mesh m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }

    static void AB(List<Vector3>v,List<int>t,Vector3 c,Vector3 s)
    {int si=v.Count;var h=s*0.5f;
        v.Add(c+new Vector3(-h.x,-h.y,-h.z));v.Add(c+new Vector3(h.x,-h.y,-h.z));
        v.Add(c+new Vector3(h.x,h.y,-h.z));v.Add(c+new Vector3(-h.x,h.y,-h.z));
        v.Add(c+new Vector3(-h.x,-h.y,h.z));v.Add(c+new Vector3(h.x,-h.y,h.z));
        v.Add(c+new Vector3(h.x,h.y,h.z));v.Add(c+new Vector3(-h.x,h.y,h.z));
        int[] f={0,2,1,0,3,2,4,5,6,4,6,7,3,7,6,3,6,2,0,1,5,0,5,4,0,4,7,0,7,3,1,2,6,1,6,5};
        foreach(int fi in f)t.Add(si+fi);}

    // ══════════════════════════════════════════════════════════
    // NEW DECORATIONS
    // ══════════════════════════════════════════════════════════

    static Mesh BuildDeadTreeMesh()
    {
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        int seg = 5;
        // Trunk
        AddCylinder(v, t, Vector3.zero, 0.15f, 1.5f, seg);
        // Branch 1
        AddCylinder(v, t, new Vector3(0, 1.0f, 0), 0.1f, 1.2f, seg, Quaternion.Euler(0, 0, 45));
        // Branch 2
        AddCylinder(v, t, new Vector3(0, 1.3f, 0), 0.08f, 1.0f, seg, Quaternion.Euler(30, 120, -30));
        
        Mesh m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0);
        FlatShade(m); return m;
    }

    static Mesh BuildPotMesh()
    {
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        int seg = 8;
        // Base sphere
        AddIco(v, t, new Vector3(0, 0.3f, 0), 0.3f);
        // Neck
        AddCylinder(v, t, new Vector3(0, 0.5f, 0), 0.15f, 0.3f, seg);
        // Rim
        AddCylinder(v, t, new Vector3(0, 0.75f, 0), 0.2f, 0.05f, seg);
        
        Mesh m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0);
        FlatShade(m); return m;
    }

    static Mesh BuildShrubMesh()
    {
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        var rng = new System.Random(88);
        for(int i=0; i<8; i++) {
            float ang = i * 45f + (float)rng.NextDouble() * 10f;
            float r = 0.3f + (float)rng.NextDouble() * 0.3f;
            float h = 0.4f + (float)rng.NextDouble() * 0.4f;
            Vector3 top = new Vector3(Mathf.Cos(ang*Mathf.Deg2Rad)*r, h, Mathf.Sin(ang*Mathf.Deg2Rad)*r);
            Vector3 bl = new Vector3(Mathf.Cos((ang-20)*Mathf.Deg2Rad)*0.1f, 0, Mathf.Sin((ang-20)*Mathf.Deg2Rad)*0.1f);
            Vector3 br = new Vector3(Mathf.Cos((ang+20)*Mathf.Deg2Rad)*0.1f, 0, Mathf.Sin((ang+20)*Mathf.Deg2Rad)*0.1f);
            int si = v.Count;
            v.Add(top); v.Add(bl); v.Add(br);
            t.Add(si); t.Add(si+1); t.Add(si+2);
            t.Add(si); t.Add(si+2); t.Add(si+1); // double sided
        }
        Mesh m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }

    static Mesh BuildStatueMesh()
    {
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        // Pedestal
        AB(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 2f));
        AB(v, t, new Vector3(0, 1.25f, 0), new Vector3(1.6f, 0.5f, 1.6f));
        // Body
        AB(v, t, new Vector3(0, 3.0f, 0), new Vector3(1.2f, 3f, 0.8f));
        // Head
        AB(v, t, new Vector3(0, 5.2f, 0), new Vector3(0.8f, 1.2f, 0.9f));
        // Arms
        AB(v, t, new Vector3(-0.85f, 3.5f, 0), new Vector3(0.5f, 2f, 0.5f));
        AB(v, t, new Vector3(0.85f, 3.5f, 0), new Vector3(0.5f, 2f, 0.5f));
        
        Mesh m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); 
        FlatShade(m); return m;
    }

    static void FlatShade(Mesh m)
    {
        Vector3[] oldV = m.vertices;
        int[] oldT = m.triangles;
        Vector3[] newV = new Vector3[oldT.Length];
        int[] newT = new int[oldT.Length];
        for (int i = 0; i < oldT.Length; i++) {
            newV[i] = oldV[oldT[i]];
            newT[i] = i;
        }
        m.vertices = newV;
        m.triangles = newT;
        m.RecalculateNormals();
        m.RecalculateBounds();
    }

    static void SpawnGeneric(GameObject terrObj, string groupName, string prefix, int count, Mesh mesh, Material mat, float minScale, float maxScale, bool applyRot)
    {
        Terrain t = terrObj.GetComponent<Terrain>();
        TerrainData td = t.terrainData;
        Vector3 tp = terrObj.transform.position;
        System.Random rng = new System.Random(groupName.GetHashCode());
        GameObject par = new GameObject("=== " + groupName + " ===");

        for (int i = 0; i < count; i++)
        {
            float nx = 0.05f + (float)rng.NextDouble() * 0.9f;
            float nz = 0.05f + (float)rng.NextDouble() * 0.9f;
            float wx = tp.x + nx * td.size.x;
            float wz = tp.z + nz * td.size.z;
            float wy = t.SampleHeight(new Vector3(wx, 0, wz));

            float scale = minScale + (float)rng.NextDouble() * (maxScale - minScale);

            var obj = new GameObject(prefix + "_" + i);
            obj.transform.position = new Vector3(wx, wy, wz);
            
            if (applyRot) {
                obj.transform.rotation = Quaternion.Euler(0, rng.Next(360), 0);
            } else {
                obj.transform.rotation = Quaternion.Euler(rng.Next(15), rng.Next(360), rng.Next(15));
            }
            
            obj.transform.localScale = Vector3.one * scale;
            obj.transform.parent = par.transform;

            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = mat;
            
            var col = obj.AddComponent<BoxCollider>();
        }
    }
}
