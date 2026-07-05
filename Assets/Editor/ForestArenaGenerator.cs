using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Dark Fantasy Forest Arena Generator
/// Menu: Tools > Generate Forest Arena Map
/// </summary>
public class ForestArenaGenerator : Editor
{
    // ─── Terrain ──────────────────────────────────────────────
    const int T_SIZE      = 500;
    const int T_HEIGHT    = 80;
    const int HM_RES      = 513;
    const int AM_RES      = 512;

    // ─── Population ───────────────────────────────────────────
    const int TREE_COUNT  = 180;
    const int RUIN_COUNT  = 10;
    const int SHRINE_COUNT= 8;
    const int POT_COUNT   = 24;
    const int ROCK_COUNT  = 40;
    const int BUSH_COUNT  = 45;

    // ─── Paths ────────────────────────────────────────────────
    const string SCENE_PATH = "Assets/Scenes/ForestArena.unity";
    const string MAT_DIR    = "Assets/ForestArena_Materials";
    const string MESH_DIR   = "Assets/ForestArena_Meshes";
    const string TD_PATH    = "Assets/ForestArena_TerrainData.asset";

    // ─── Wall Settings ────────────────────────────────────────
    const float WALL_HEIGHT     = 14f;
    const float WALL_THICKNESS  = 2.5f;
    const float TOWER_HEIGHT    = 20f;
    const float TOWER_RADIUS    = 3.5f;

    // ─── Cached ───────────────────────────────────────────────
    static Material mTrunk, mLeaf, mLeafDark, mLeafDead;
    static Material mStone, mStoneMoss, mShrineStone, mShrineGlow;
    static Material mPotSilver, mPotGlow;
    static Material mRock, mBush, mWater;
    static Material mWallStone, mWallDark;

    static Mesh meshIco, meshCone, meshBush, meshRock;
    static Mesh meshPot, meshPyramid, meshWall, meshPillar, meshArch;

    static float[,] s_hm;

    // Mountain positions (normalised 0–1)
    static readonly (float x, float z, float str, float rad)[] MTNS = {
        (0.12f,0.12f,1.00f,0.18f),
        (0.88f,0.10f,0.95f,0.16f),
        (0.10f,0.88f,0.90f,0.15f),
        (0.88f,0.88f,0.85f,0.17f),
        (0.50f,0.05f,0.75f,0.14f),
        (0.05f,0.50f,0.70f,0.12f),
    };

    // ══════════════════════════════════════════════════════════
    [MenuItem("Tools/Generate Forest Arena Map")]
    public static void Generate()
    {
        if (!EditorUtility.DisplayDialog("Generate Dark Fantasy Arena",
            "Creates ForestArena.unity with dark fantasy theme.\nContinue?",
            "Generate", "Cancel")) return;

        try
        {
            EnsureDir(MAT_DIR);
            EnsureDir(MESH_DIR);

            Bar("Writing textures...", 0.03f);
            WriteTextures();

            Bar("Importing...", 0.06f);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Bar("Materials...", 0.09f);
            BuildMaterials();
            BuildMeshes();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Bar("Terrain...", 0.15f);
            GameObject terr = BuildTerrain();

            Bar("Trees...", 0.35f);
            SpawnTrees(terr);

            Bar("Rocks & bushes...", 0.45f);
            SpawnRocksAndBushes(terr);

            Bar("Ruins...", 0.55f);
            SpawnRuins(terr);

            Bar("Shrines...", 0.63f);
            SpawnShrines(terr);

            Bar("Floating pots...", 0.70f);
            SpawnPots(terr);

            Bar("Fortress walls...", 0.78f);
            BuildFortressWalls(terr);

            Bar("Water...", 0.82f);
            SpawnWater(terr);

            Bar("Lighting...", 0.87f);
            SetupDarkFantasyLighting();

            Bar("Atmosphere...", 0.91f);
            SpawnParticles();

            Bar("Camera...", 0.95f);
            SetupCamera();

            Bar("Saving...", 0.98f);
            EditorSceneManager.SaveScene(scene, SCENE_PATH);

            EditorUtility.ClearProgressBar();
            Debug.Log("✅ Dark Fantasy Arena → " + SCENE_PATH);
            EditorUtility.DisplayDialog("Done!", "Dark Fantasy Arena created!\n" + SCENE_PATH, "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("Generator failed: " + e);
            throw;
        }
    }

    static void Bar(string msg, float p) => EditorUtility.DisplayProgressBar("Dark Fantasy Arena", msg, p);

    static void EnsureDir(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(
                Path.GetDirectoryName(path).Replace("\\", "/"),
                Path.GetFileName(path));
    }

    // ══════════════════════════════════════════════════════════
    // TEXTURES — only 3 small PNGs (32×32), no snow
    //   Grass (dark), Dirt, Cliff
    // ══════════════════════════════════════════════════════════
    static void WriteTextures()
    {
        // Dark murky grass
        WritePNG("Grass", new Color(0.10f, 0.22f, 0.08f), 32);
        // Dark soil/dirt
        WritePNG("Dirt",  new Color(0.18f, 0.12f, 0.07f), 32);
        // Dark cliff rock
        WritePNG("Cliff", new Color(0.20f, 0.18f, 0.16f), 32);
    }

    static void WritePNG(string id, Color col, int size)
    {
        string path = MAT_DIR + "/Tex_" + id + ".png";
        if (File.Exists(path)) File.Delete(path); // always regenerate for color change

        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        int seed = id.GetHashCode();
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.25f + seed, y * 0.25f + seed) * 0.06f - 0.03f;
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
    // MATERIALS — dark fantasy palette, minimal count
    // ══════════════════════════════════════════════════════════
    static void BuildMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");

        // Trees — dark, desaturated greens
        mTrunk    = M("Trunk",    lit, C(0.15f,0.08f,0.04f), 0.08f);
        mLeaf     = M("Leaf",     lit, C(0.08f,0.22f,0.06f), 0.15f);
        mLeafDark = M("LeafDark", lit, C(0.04f,0.14f,0.05f), 0.15f);
        mLeafDead = M("LeafDead", lit, C(0.22f,0.18f,0.06f), 0.12f);

        // Stone — cold grey
        mStone     = M("Stone",     lit, C(0.25f,0.22f,0.20f), 0.10f);
        mStoneMoss = M("StoneMoss", lit, C(0.12f,0.20f,0.10f), 0.15f);

        // Shrines — eerie purple/blue glow
        mShrineStone = M("ShrStone", lit, C(0.18f,0.15f,0.22f), 0.20f);
        mShrineGlow  = M("ShrGlow",  lit, C(0.40f,0.15f,0.80f), 0.50f);
        Emit(mShrineGlow, new Color(0.5f, 0.1f, 0.9f) * 3f);

        // Pots — ghostly silver/blue glow
        mPotSilver = M("PotSilv", lit, C(0.50f,0.52f,0.58f), 0.65f);
        mPotSilver.SetFloat("_Metallic", 0.85f);
        mPotGlow   = M("PotGlow", lit, C(0.30f,0.60f,0.90f), 0.40f);
        Emit(mPotGlow, new Color(0.2f, 0.5f, 1f) * 3.5f);

        // Environment
        mRock = M("Rock", lit, C(0.20f,0.18f,0.16f), 0.08f);
        mBush = M("Bush", lit, C(0.06f,0.18f,0.07f), 0.15f);

        // Water — dark swamp
        mWater = M("Water", lit, new Color(0.04f,0.10f,0.12f,0.72f), 0.88f);
        mWater.SetFloat("_Surface", 1);
        mWater.SetOverrideTag("RenderType", "Transparent");
        mWater.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mWater.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mWater.SetInt("_ZWrite", 0);
        mWater.renderQueue = 3000;
        Emit(mWater, new Color(0.02f, 0.06f, 0.08f) * 0.3f);

        // Fortress walls — dark heavy stone
        mWallStone = M("WallStone", lit, C(0.16f,0.14f,0.13f), 0.08f);
        mWallDark  = M("WallDark",  lit, C(0.10f,0.08f,0.07f), 0.06f);

        AssetDatabase.SaveAssets();
    }

    static Color C(float r, float g, float b) => new Color(r, g, b);

    static Material M(string n, Shader s, Color c, float sm)
    {
        string p = MAT_DIR + "/" + n + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(s); m.name = n; AssetDatabase.CreateAsset(m, p); }
        m.shader = s;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", sm);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Emit(Material m, Color c)
    {
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    // ══════════════════════════════════════════════════════════
    // MESHES
    // ══════════════════════════════════════════════════════════
    static void BuildMeshes()
    {
        meshIco     = SM("Ico",     MakeIco(1.8f, 1));
        meshCone    = SM("Cone",    MakeCone(1.2f, 3f, 8));
        meshBush    = SM("Bush",    MakeIco(0.8f, 0));
        meshRock    = SM("Rock",    MakeRock(1f));
        meshPot     = SM("Pot",     MakePot());
        meshPyramid = SM("Pyramid", MakePyramid());
        meshWall    = SM("Wall",    MakeWallMesh());
        meshPillar  = SM("Pillar",  MakePillar());
        meshArch    = SM("Arch",    MakeArch());
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

            // Rolling base
            float bh =
                Mathf.PerlinNoise(nx*2.1f+50, ny*2.1f+50) * 0.28f +
                Mathf.PerlinNoise(nx*4.3f+80, ny*4.3f+80) * 0.10f +
                Mathf.PerlinNoise(nx*9f+20,   ny*9f+20)   * 0.04f +
                Mathf.PerlinNoise(nx*22f+10,  ny*22f+10)  * 0.012f;

            // Mountains
            float mh = 0f;
            foreach (var m in MTNS)
            {
                float dx = nx - m.x, dz = ny - m.z;
                float d = Mathf.Sqrt(dx*dx + dz*dz);
                if (d < m.rad * 2.5f)
                {
                    float f = Mathf.Pow(Mathf.Clamp01(1f - d/m.rad), 1.6f) * m.str;
                    float ridge = (1f - Mathf.Abs(Mathf.PerlinNoise(nx*14+m.x*100, ny*14+m.z*100)*2-1)) * 0.12f *
                        Mathf.Clamp01(1f - d/m.rad);
                    mh = Mathf.Max(mh, f + ridge);
                }
            }

            // Arena depression
            float cx = nx-0.5f, cz = ny-0.5f;
            float cd = Mathf.Sqrt(cx*cx+cz*cz);
            float arena = Mathf.Clamp01(1f - Mathf.InverseLerp(0.14f, 0.25f, cd));

            float total = bh*(1f-arena*0.75f) + mh*(1f-arena);
            h[y,x] = Mathf.Clamp01(Mathf.Max(total, 0.04f));
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

        // 3 layers only — fewer splats = less GPU work
        td.terrainLayers = new TerrainLayer[] {
            TL("Grass", 18f),
            TL("Dirt",  12f),
            TL("Cliff",  9f),
        };

        td.SetAlphamaps(0, 0, PaintSplat(s_hm));

        // Save
        var old = AssetDatabase.LoadAssetAtPath<TerrainData>(TD_PATH);
        if (old) AssetDatabase.DeleteAsset(TD_PATH);
        AssetDatabase.CreateAsset(td, TD_PATH);
        AssetDatabase.SaveAssets();

        GameObject go = Terrain.CreateTerrainGameObject(td);
        go.name = "Terrain";
        go.transform.position = new Vector3(-T_SIZE*0.5f, 0, -T_SIZE*0.5f);

        Terrain t = go.GetComponent<Terrain>();
        t.drawInstanced = true;
        t.heightmapPixelError = 6;
        t.basemapDistance = 800f;
        t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;

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
        // 0=Grass 1=Dirt 2=Cliff
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

            float cliff = Mathf.Clamp01((slope-1.0f)*0.9f);
            float dirt = Mathf.Clamp01((h-0.30f)*3f)*0.5f * (1f-cliff);

            // Random dirt patches
            float dn = Mathf.PerlinNoise(nx*10+55, ny*10+55);
            dirt += Mathf.Clamp01((dn-0.58f)*3f)*0.35f*(1f-cliff);

            float grass = Mathf.Max(0, 1f - cliff - dirt);

            float sum = grass+dirt+cliff;
            sp[ay,ax,0] = grass/sum;
            sp[ay,ax,1] = dirt/sum;
            sp[ay,ax,2] = cliff/sum;
        }
        return sp;
    }

    // ══════════════════════════════════════════════════════════
    // FORTRESS WALLS — visible stone walls + corner towers
    // ══════════════════════════════════════════════════════════
    static void BuildFortressWalls(GameObject terrainObj)
    {
        Terrain terr = terrainObj.GetComponent<Terrain>();
        TerrainData td = terr.terrainData;
        Vector3 tp = terrainObj.transform.position;
        GameObject par = new GameObject("=== Fortress Walls ===");

        float half = T_SIZE * 0.5f;
        // Margin inward from terrain edge so wall sits ON terrain
        float margin = 8f;
        float innerHalf = half - margin;

        // 4 wall segments (North, South, East, West)
        // Each wall = a series of wall sections + merlons on top
        BuildWallSegment(par, terr, tp, td,
            new Vector3(-innerHalf, 0, innerHalf),
            new Vector3(innerHalf, 0, innerHalf), "North");

        BuildWallSegment(par, terr, tp, td,
            new Vector3(-innerHalf, 0, -innerHalf),
            new Vector3(innerHalf, 0, -innerHalf), "South");

        BuildWallSegment(par, terr, tp, td,
            new Vector3(innerHalf, 0, -innerHalf),
            new Vector3(innerHalf, 0, innerHalf), "East");

        BuildWallSegment(par, terr, tp, td,
            new Vector3(-innerHalf, 0, -innerHalf),
            new Vector3(-innerHalf, 0, innerHalf), "West");

        // Corner towers
        float[] cx = { -innerHalf, innerHalf, innerHalf, -innerHalf };
        float[] cz = { innerHalf, innerHalf, -innerHalf, -innerHalf };
        string[] cnames = { "NW", "NE", "SE", "SW" };
        for (int i = 0; i < 4; i++)
        {
            float wy = SampleH(terr, tp, td,
                (cx[i]+half)/T_SIZE, (cz[i]+half)/T_SIZE);
            BuildCornerTower(par, new Vector3(cx[i], wy, cz[i]), cnames[i]);
        }
    }

    static void BuildWallSegment(GameObject par, Terrain terr, Vector3 tp,
        TerrainData td, Vector3 startLocal, Vector3 endLocal, string label)
    {
        int segments = 20;
        float half = T_SIZE * 0.5f;
        GameObject wallGroup = new GameObject("Wall_" + label);
        wallGroup.transform.parent = par.transform;

        for (int i = 0; i < segments; i++)
        {
            float t0 = (float)i / segments;
            float t1 = (float)(i + 1) / segments;
            Vector3 p0 = Vector3.Lerp(startLocal, endLocal, t0);
            Vector3 p1 = Vector3.Lerp(startLocal, endLocal, t1);
            Vector3 mid = (p0 + p1) * 0.5f;

            // Sample terrain height at this position
            float nx = (mid.x + half) / T_SIZE;
            float nz = (mid.z + half) / T_SIZE;
            float wy = SampleH(terr, tp, td, nx, nz);

            float segLen = Vector3.Distance(p0, p1);
            float yRot = Mathf.Atan2(p1.x - p0.x, p1.z - p0.z) * Mathf.Rad2Deg;

            // Main wall block
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "WallBlock_" + i;
            block.transform.parent = wallGroup.transform;
            block.transform.position = new Vector3(mid.x, wy + WALL_HEIGHT*0.5f - 1f, mid.z);
            block.transform.localScale = new Vector3(WALL_THICKNESS, WALL_HEIGHT, segLen + 0.2f);
            block.transform.rotation = Quaternion.Euler(0, yRot, 0);
            block.GetComponent<Renderer>().sharedMaterial = mWallStone;

            // Merlons (battlements) on top — every other segment
            if (i % 2 == 0)
            {
                GameObject merlon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                merlon.name = "Merlon_" + i;
                merlon.transform.parent = wallGroup.transform;
                merlon.transform.position = new Vector3(mid.x, wy + WALL_HEIGHT + 0.8f, mid.z);
                merlon.transform.localScale = new Vector3(WALL_THICKNESS + 0.5f, 1.8f, segLen * 0.45f);
                merlon.transform.rotation = Quaternion.Euler(0, yRot, 0);
                merlon.GetComponent<Renderer>().sharedMaterial = mWallDark;
            }

            // Wall base (wider foundation)
            GameObject foundation = GameObject.CreatePrimitive(PrimitiveType.Cube);
            foundation.name = "Foundation_" + i;
            foundation.transform.parent = wallGroup.transform;
            foundation.transform.position = new Vector3(mid.x, wy + 0.6f, mid.z);
            foundation.transform.localScale = new Vector3(WALL_THICKNESS + 1.5f, 1.4f, segLen + 0.3f);
            foundation.transform.rotation = Quaternion.Euler(0, yRot, 0);
            foundation.GetComponent<Renderer>().sharedMaterial = mWallDark;
        }
    }

    static void BuildCornerTower(GameObject par, Vector3 pos, string label)
    {
        GameObject tower = new GameObject("Tower_" + label);
        tower.transform.parent = par.transform;
        tower.transform.position = pos;

        // Tower body (cylinder)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "TowerBody";
        body.transform.parent = tower.transform;
        body.transform.localPosition = new Vector3(0, TOWER_HEIGHT * 0.5f - 1f, 0);
        body.transform.localScale = new Vector3(TOWER_RADIUS * 2, TOWER_HEIGHT * 0.5f, TOWER_RADIUS * 2);
        body.GetComponent<Renderer>().sharedMaterial = mWallStone;

        // Tower top (wider disc)
        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        top.name = "TowerTop";
        top.transform.parent = tower.transform;
        top.transform.localPosition = new Vector3(0, TOWER_HEIGHT - 0.5f, 0);
        top.transform.localScale = new Vector3(TOWER_RADIUS * 2.4f, 0.5f, TOWER_RADIUS * 2.4f);
        top.GetComponent<Renderer>().sharedMaterial = mWallDark;

        // Cone roof
        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        roof.name = "TowerRoof";
        roof.transform.parent = tower.transform;
        roof.transform.localPosition = new Vector3(0, TOWER_HEIGHT + 2.5f, 0);
        roof.transform.localScale = new Vector3(TOWER_RADIUS * 2.2f, 2.5f, TOWER_RADIUS * 2.2f);
        roof.GetComponent<Renderer>().sharedMaterial = mWallDark;

        // 4 small merlons around top
        for (int m = 0; m < 4; m++)
        {
            float a = m * 90f * Mathf.Deg2Rad;
            float mx = Mathf.Cos(a) * TOWER_RADIUS * 1.1f;
            float mz = Mathf.Sin(a) * TOWER_RADIUS * 1.1f;

            GameObject merlon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            merlon.name = "TowerMerlon_" + m;
            merlon.transform.parent = tower.transform;
            merlon.transform.localPosition = new Vector3(mx, TOWER_HEIGHT + 0.5f, mz);
            merlon.transform.localScale = new Vector3(1.2f, 2f, 1.2f);
            merlon.GetComponent<Renderer>().sharedMaterial = mWallStone;
        }

        // Eerie torch light on each tower
        GameObject lo = new GameObject("TorchLight");
        lo.transform.parent = tower.transform;
        lo.transform.localPosition = new Vector3(0, TOWER_HEIGHT + 1f, 0);
        Light l = lo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.6f, 0.3f, 0.1f);  // warm torch
        l.intensity = 4f;
        l.range = 18f;
    }

    static float SampleH(Terrain terr, Vector3 tp, TerrainData td, float nx, float nz)
    {
        nx = Mathf.Clamp01(nx);
        nz = Mathf.Clamp01(nz);
        float wx = tp.x + nx * td.size.x;
        float wz = tp.z + nz * td.size.z;
        return terr.SampleHeight(new Vector3(wx, 0, wz));
    }

    // ══════════════════════════════════════════════════════════
    // TREES
    // ══════════════════════════════════════════════════════════
    static void SpawnTrees(GameObject terrObj)
    {
        Terrain t = terrObj.GetComponent<Terrain>();
        TerrainData td = t.terrainData;
        Vector3 tp = terrObj.transform.position;
        System.Random rng = new System.Random(1234);
        Material[] lm = { mLeaf, mLeafDark, mLeafDead };
        GameObject par = new GameObject("=== Trees ===");

        int placed = 0, tries = 0;
        while (placed < TREE_COUNT && tries < TREE_COUNT * 8)
        {
            tries++;
            float nx = (float)rng.NextDouble();
            float nz = (float)rng.NextDouble();
            if (td.GetSteepness(nx, nz) > 28f) continue;

            float cx = nx-0.5f, cz = nz-0.5f;
            if (Mathf.Sqrt(cx*cx+cz*cz) < 0.10f) continue;

            // Don't place too close to walls
            if (nx < 0.04f || nx > 0.96f || nz < 0.04f || nz > 0.96f) continue;

            float wx = tp.x + nx*td.size.x;
            float wz = tp.z + nz*td.size.z;
            float wy = t.SampleHeight(new Vector3(wx,0,wz));

            float scale = 0.75f + (float)rng.NextDouble()*0.6f;
            bool pine = rng.NextDouble() > 0.45;

            GameObject tree = MakeTree(pine, lm[rng.Next(3)], scale);
            tree.name = "Tree_"+placed;
            tree.transform.position = new Vector3(wx, wy, wz);
            tree.transform.rotation = Quaternion.Euler(0, rng.Next(360), 0);
            tree.transform.parent = par.transform;
            placed++;
        }
    }

    static GameObject MakeTree(bool pine, Material lm, float s)
    {
        GameObject r = new GameObject();
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name="Trunk";
        trunk.transform.parent = r.transform;
        trunk.transform.localPosition = Vector3.up*(2f*s);
        trunk.transform.localScale = new Vector3(0.28f*s, 2f*s, 0.28f*s);
        trunk.GetComponent<Renderer>().sharedMaterial = mTrunk;
        Object.DestroyImmediate(trunk.GetComponent<Collider>());

        if (pine)
        {
            for (int i=0;i<3;i++)
            {
                float ls=(1.5f-i*0.35f)*s;
                AddMesh(r, meshCone, lm, Vector3.up*(3.2f+i*1.2f)*s, ls);
            }
        }
        else
        {
            AddMesh(r, meshIco, lm, Vector3.up*4.5f*s, 2f*s);
            AddMesh(r, meshIco, lm, new Vector3(0.45f,4f,0.3f)*s, 1.3f*s);
        }

        var col = r.AddComponent<BoxCollider>();
        col.center = Vector3.up*(2f*s);
        col.size = new Vector3(s, 4f*s, s);
        return r;
    }

    static void AddMesh(GameObject p, Mesh mesh, Material mat, Vector3 lpos, float lscale)
    {
        var c = new GameObject("Canopy");
        c.transform.parent = p.transform;
        c.transform.localPosition = lpos;
        c.transform.localScale = Vector3.one*lscale;
        c.AddComponent<MeshFilter>().sharedMesh = mesh;
        c.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // ══════════════════════════════════════════════════════════
    // ROCKS & BUSHES
    // ══════════════════════════════════════════════════════════
    static void SpawnRocksAndBushes(GameObject terrObj)
    {
        Terrain t = terrObj.GetComponent<Terrain>();
        TerrainData td = t.terrainData;
        Vector3 tp = terrObj.transform.position;
        System.Random rng = new System.Random(5678);
        GameObject par = new GameObject("=== Rocks & Bushes ===");

        for (int i=0;i<ROCK_COUNT;i++)
        {
            float nx=(float)rng.NextDouble(), nz=(float)rng.NextDouble();
            float wx=tp.x+nx*td.size.x, wz=tp.z+nz*td.size.z;
            float wy=t.SampleHeight(new Vector3(wx,0,wz));
            float s=0.5f+(float)rng.NextDouble()*1.8f;

            var rock = new GameObject("Rock_"+i);
            rock.AddComponent<MeshFilter>().sharedMesh = meshRock;
            rock.AddComponent<MeshRenderer>().sharedMaterial = mRock;
            var mc = rock.AddComponent<MeshCollider>(); mc.convex=true;
            rock.transform.position = new Vector3(wx, wy-0.15f*s, wz);
            rock.transform.localScale = new Vector3(s, s*(0.5f+(float)rng.NextDouble()*0.5f), s);
            rock.transform.rotation = Quaternion.Euler(rng.Next(12), rng.Next(360), rng.Next(8));
            rock.transform.parent = par.transform;
        }

        int bp=0, bt=0;
        while (bp<BUSH_COUNT && bt<BUSH_COUNT*6)
        {
            bt++;
            float nx=(float)rng.NextDouble(), nz=(float)rng.NextDouble();
            if (td.GetSteepness(nx,nz)>25f) continue;
            float wx=tp.x+nx*td.size.x, wz=tp.z+nz*td.size.z;
            float wy=t.SampleHeight(new Vector3(wx,0,wz));
            float s=0.55f+(float)rng.NextDouble()*0.8f;

            var bush = new GameObject("Bush_"+bp);
            bush.AddComponent<MeshFilter>().sharedMesh = meshBush;
            bush.AddComponent<MeshRenderer>().sharedMaterial = mBush;
            bush.transform.position = new Vector3(wx, wy, wz);
            bush.transform.localScale = new Vector3(s*1.4f, s, s*1.4f);
            bush.transform.rotation = Quaternion.Euler(0, rng.Next(360), 0);
            bush.transform.parent = par.transform;
            bp++;
        }
    }

    // ══════════════════════════════════════════════════════════
    // RUINS
    // ══════════════════════════════════════════════════════════
    static void SpawnRuins(GameObject terrObj)
    {
        Terrain t = terrObj.GetComponent<Terrain>();
        TerrainData td = t.terrainData;
        Vector3 tp = terrObj.transform.position;
        System.Random rng = new System.Random(2222);
        GameObject par = new GameObject("=== Ruins ===");

        int placed=0, tries=0;
        while (placed<RUIN_COUNT && tries<RUIN_COUNT*10)
        {
            tries++;
            float nx=0.10f+(float)rng.NextDouble()*0.80f;
            float nz=0.10f+(float)rng.NextDouble()*0.80f;
            if (td.GetSteepness(nx,nz)>18f) continue;

            float wx=tp.x+nx*td.size.x, wz=tp.z+nz*td.size.z;
            float wy=t.SampleHeight(new Vector3(wx,0,wz));

            GameObject ruin = MakeRuin(rng);
            ruin.name="Ruin_"+placed;
            ruin.transform.position = new Vector3(wx,wy,wz);
            ruin.transform.rotation = Quaternion.Euler(0, rng.Next(360), 0);
            ruin.transform.parent = par.transform;
            placed++;
        }
    }

    static GameObject MakeRuin(System.Random rng)
    {
        GameObject r = new GameObject();
        switch(rng.Next(4))
        {
            case 0:
                AddPart(r, meshWall, mStone, Vector3.zero, Vector3.one, Quaternion.Euler(0,0,rng.Next(-5,5)));
                AddPart(r, meshBush, mStoneMoss, new Vector3(0,1.4f,0.3f), Vector3.one*0.5f, Quaternion.identity);
                break;
            case 1:
                for(int p=0;p<3;p++)
                {
                    float ph=0.7f+(float)rng.NextDouble()*0.5f;
                    AddPart(r, meshPillar, mStone, new Vector3((p-1)*2.4f,0,0), new Vector3(1,ph,1), Quaternion.identity);
                }
                break;
            case 2:
                AddPart(r, meshArch, mStone, Vector3.zero, Vector3.one*1.1f, Quaternion.identity);
                AddPart(r, meshBush, mStoneMoss, new Vector3(0.4f,2f,0), Vector3.one*0.4f, Quaternion.identity);
                break;
            default:
                int bc=3+rng.Next(4);
                for(int b=0;b<bc;b++)
                {
                    float bx=((float)rng.NextDouble()-0.5f)*4.5f;
                    float bz=((float)rng.NextDouble()-0.5f)*4.5f;
                    float bs=0.35f+(float)rng.NextDouble()*0.85f;
                    var bl = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bl.name="Block";
                    bl.transform.parent=r.transform;
                    bl.transform.localPosition=new Vector3(bx,bs*0.4f,bz);
                    bl.transform.localScale=new Vector3(bs,bs*0.55f,bs);
                    bl.transform.localRotation=Quaternion.Euler(rng.Next(-10,10),rng.Next(360),rng.Next(-10,10));
                    bl.GetComponent<Renderer>().sharedMaterial=mStone;
                }
                break;
        }
        return r;
    }

    static void AddPart(GameObject p, Mesh mesh, Material mat, Vector3 lp, Vector3 ls, Quaternion lr)
    {
        var go = new GameObject("Part");
        go.transform.parent=p.transform;
        go.transform.localPosition=lp; go.transform.localScale=ls; go.transform.localRotation=lr;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial=mat;
        var mc=go.AddComponent<MeshCollider>(); mc.convex=true;
    }

    // ══════════════════════════════════════════════════════════
    // SHRINES (dark ritual stones)
    // ══════════════════════════════════════════════════════════
    static void SpawnShrines(GameObject terrObj)
    {
        Terrain t = terrObj.GetComponent<Terrain>();
        TerrainData td = t.terrainData;
        Vector3 tp = terrObj.transform.position;
        System.Random rng = new System.Random(3333);
        GameObject par = new GameObject("=== Shrines ===");

        for (int i=0;i<SHRINE_COUNT;i++)
        {
            float angle = (float)i/SHRINE_COUNT*Mathf.PI*2f;
            float rad = 0.18f+(float)rng.NextDouble()*0.1f;
            float nx = Mathf.Clamp(0.5f+Mathf.Cos(angle)*rad, 0.06f, 0.94f);
            float nz = Mathf.Clamp(0.5f+Mathf.Sin(angle)*rad, 0.06f, 0.94f);
            float wx = tp.x+nx*td.size.x, wz=tp.z+nz*td.size.z;
            float wy = t.SampleHeight(new Vector3(wx,0,wz));

            var s = MakeShrine();
            s.name="Shrine_"+i;
            s.transform.position=new Vector3(wx,wy,wz);
            s.transform.rotation=Quaternion.Euler(0,rng.Next(360),0);
            s.transform.parent=par.transform;
        }
    }

    static GameObject MakeShrine()
    {
        var root = new GameObject();

        Prim(root, PrimitiveType.Cylinder, "Base",
            new Vector3(0,0.12f,0), new Vector3(2.4f,0.12f,2.4f), mShrineStone);
        Prim(root, PrimitiveType.Cylinder, "Pillar",
            new Vector3(0,1.4f,0), new Vector3(0.45f,1.2f,0.45f), mShrineStone);
        AddPart(root, meshPyramid, mShrineStone,
            new Vector3(0,2.65f,0), Vector3.one*0.75f, Quaternion.identity);

        var orb = Prim(root, PrimitiveType.Sphere, "Orb",
            new Vector3(0,3.35f,0), Vector3.one*0.35f, mShrineGlow);
        Object.DestroyImmediate(orb.GetComponent<Collider>());

        var lo = new GameObject("ShrineLight");
        lo.transform.parent=root.transform;
        lo.transform.localPosition=new Vector3(0,3.4f,0);
        Light l = lo.AddComponent<Light>();
        l.type=LightType.Point;
        l.color=new Color(0.5f,0.15f,0.8f); // eerie purple
        l.intensity=4f; l.range=10f;

        for(int c=0;c<4;c++)
        {
            float a=c*90f*Mathf.Deg2Rad;
            Prim(root, PrimitiveType.Cube, "CS_"+c,
                new Vector3(Mathf.Cos(a)*0.95f, 0.38f, Mathf.Sin(a)*0.95f),
                new Vector3(0.32f,0.76f,0.32f), mShrineStone);
        }
        return root;
    }

    static GameObject Prim(GameObject p, PrimitiveType type, string n, Vector3 lp, Vector3 ls, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name=n; go.transform.parent=p.transform;
        go.transform.localPosition=lp; go.transform.localScale=ls;
        go.GetComponent<Renderer>().sharedMaterial=m;
        return go;
    }

    // ══════════════════════════════════════════════════════════
    // FLOATING POTS (ghostly blue)
    // ══════════════════════════════════════════════════════════
    static void SpawnPots(GameObject terrObj)
    {
        Terrain t = terrObj.GetComponent<Terrain>();
        TerrainData td = t.terrainData;
        Vector3 tp = terrObj.transform.position;
        System.Random rng = new System.Random(4444);
        GameObject par = new GameObject("=== Pots ===");

        for (int i=0;i<POT_COUNT;i++)
        {
            float nx=0.08f+(float)rng.NextDouble()*0.84f;
            float nz=0.08f+(float)rng.NextDouble()*0.84f;
            float wx=tp.x+nx*td.size.x, wz=tp.z+nz*td.size.z;
            float wy=t.SampleHeight(new Vector3(wx,0,wz));
            float fh=1.1f+(float)rng.NextDouble()*1.5f;
            float sc=0.35f+(float)rng.NextDouble()*0.3f;

            var pot = new GameObject("Pot_"+i);
            pot.transform.position=new Vector3(wx,wy,wz);
            pot.transform.parent=par.transform;

            var body = new GameObject("Body");
            body.transform.parent=pot.transform;
            body.transform.localPosition=Vector3.up*fh;
            body.transform.localScale=Vector3.one*sc;
            body.AddComponent<MeshFilter>().sharedMesh=meshPot;
            body.AddComponent<MeshRenderer>().sharedMaterial=mPotSilver;

            var aura = Prim(pot, PrimitiveType.Sphere, "Aura",
                Vector3.up*fh, Vector3.one*sc*1.3f, mPotGlow);
            Object.DestroyImmediate(aura.GetComponent<Collider>());

            var lo = new GameObject("PotLight");
            lo.transform.parent=pot.transform;
            lo.transform.localPosition=Vector3.up*fh;
            var l = lo.AddComponent<Light>();
            l.type=LightType.Point;
            l.color=new Color(0.2f,0.5f,1f);
            l.intensity=2.5f; l.range=5f;

            pot.AddComponent<FloatingPotAnimator>();
        }
    }

    // ══════════════════════════════════════════════════════════
    // WATER (dark swamp pools)
    // ══════════════════════════════════════════════════════════
    static void SpawnWater(GameObject terrObj)
    {
        Terrain t = terrObj.GetComponent<Terrain>();
        TerrainData td = t.terrainData;
        Vector3 tp = terrObj.transform.position;
        System.Random rng = new System.Random(7777);
        GameObject par = new GameObject("=== Water ===");

        for (int i=0;i<3;i++)
        {
            float nx=0.25f+(float)rng.NextDouble()*0.50f;
            float nz=0.25f+(float)rng.NextDouble()*0.50f;
            float wx=tp.x+nx*td.size.x, wz=tp.z+nz*td.size.z;
            float wy=t.SampleHeight(new Vector3(wx,0,wz));
            float ps=5f+(float)rng.NextDouble()*8f;

            var pond = Prim(par, PrimitiveType.Cylinder, "Pond_"+i,
                new Vector3(wx, wy+0.06f, wz), new Vector3(ps,0.02f,ps), mWater);
            Object.DestroyImmediate(pond.GetComponent<Collider>());
        }
    }

    // ══════════════════════════════════════════════════════════
    // DARK FANTASY LIGHTING
    // ══════════════════════════════════════════════════════════
    static void SetupDarkFantasyLighting()
    {
        // Moon-like directional light — cold, dim
        var moon = new GameObject("MoonLight");
        var ml = moon.AddComponent<Light>();
        ml.type = LightType.Directional;
        ml.color = new Color(0.35f, 0.38f, 0.55f);  // cold blue-grey
        ml.intensity = 0.8f;
        ml.shadows = LightShadows.Soft;
        ml.shadowStrength = 0.8f;
        moon.transform.rotation = Quaternion.Euler(35f, 160f, 0f);

        // Faint warm rim light from opposite side
        var rim = new GameObject("RimLight");
        var rl = rim.AddComponent<Light>();
        rl.type = LightType.Directional;
        rl.color = new Color(0.30f, 0.15f, 0.08f);  // faint ember
        rl.intensity = 0.25f;
        rl.shadows = LightShadows.None;
        rim.transform.rotation = Quaternion.Euler(20f, -40f, 0f);

        // Dark ambient
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = new Color(0.06f, 0.05f, 0.12f); // near-black purple sky
        RenderSettings.ambientEquatorColor = new Color(0.05f, 0.08f, 0.06f); // very dark green
        RenderSettings.ambientGroundColor  = new Color(0.03f, 0.02f, 0.02f); // near-black

        // Dense fog — dark, blueish
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.06f, 0.07f, 0.10f);  // very dark blue fog
        RenderSettings.fogDensity = 0.006f;  // thicker fog for moody atmosphere
    }

    // ══════════════════════════════════════════════════════════
    // PARTICLES (reduced for performance)
    // ══════════════════════════════════════════════════════════
    static void SpawnParticles()
    {
        // Dark mist/fog particles
        MakePS("DarkMist", new Vector3(0,3f,0), new Vector3(160,6,160),
            8f, new Color(0.15f,0.15f,0.20f,0.2f), 0.35f, 12f);

        // Eerie purple fireflies
        MakePS("SoulWisps", new Vector3(0,5f,0), new Vector3(120,8,120),
            10f, new Color(0.5f,0.15f,0.8f,0.6f), 0.06f, 7f);
    }

    static void MakePS(string name, Vector3 pos, Vector3 box,
        float rate, Color col, float size, float life)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime = life;
        main.startSpeed = 0.2f;
        main.startSize = size;
        main.startColor = col;
        main.maxParticles = 200;   // reduced from 600
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var em = ps.emission;
        em.rateOverTime = rate;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = box;

        var ns = ps.noise;
        ns.enabled = true;
        ns.strength = 1.5f;
        ns.frequency = 0.15f;

        var col2 = ps.colorOverLifetime;
        col2.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]{new(col,0),new(col,1)},
            new GradientAlphaKey[]{
                new(0,0), new(col.a,0.25f), new(col.a,0.75f), new(0,1)});
        col2.color = g;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        Shader pShader = Shader.Find("Particles/Standard Unlit")
                      ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (pShader != null)
        {
            var pm = new Material(pShader);
            if (pm.HasProperty("_Color")) pm.SetColor("_Color", col);
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
        c.backgroundColor = new Color(0.02f, 0.02f, 0.04f); // near-black sky
        c.fieldOfView = 60;
        c.farClipPlane = 800f;
        cam.transform.position = new Vector3(0, 38, -52);
        cam.transform.rotation = Quaternion.Euler(28, 0, 0);
        cam.AddComponent<AudioListener>();
    }

    // ══════════════════════════════════════════════════════════
    // PROCEDURAL MESH GENERATORS
    // ══════════════════════════════════════════════════════════

    static Mesh MakeIco(float rad, int sub)
    {
        float t=(1+Mathf.Sqrt(5))/2;
        List<Vector3> v=new List<Vector3>{N(-1,t,0)*rad,N(1,t,0)*rad,N(-1,-t,0)*rad,N(1,-t,0)*rad,
            N(0,-1,t)*rad,N(0,1,t)*rad,N(0,-1,-t)*rad,N(0,1,-t)*rad,
            N(t,0,-1)*rad,N(t,0,1)*rad,N(-t,0,-1)*rad,N(-t,0,1)*rad};
        List<int> tri=new List<int>{0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,
            3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1};
        for(int s=0;s<sub;s++){List<int> nt=new();var c=new Dictionary<long,int>();
            for(int i=0;i<tri.Count;i+=3){int a=Mid(tri[i],tri[i+1],v,c,rad),b=Mid(tri[i+1],tri[i+2],v,c,rad),cc=Mid(tri[i+2],tri[i],v,c,rad);
                nt.AddRange(new[]{tri[i],a,cc,tri[i+1],b,a,tri[i+2],cc,b,a,b,cc});}tri=nt;}
        var rng=new System.Random(42);
        for(int i=0;i<v.Count;i++)v[i]+=v[i].normalized*((float)rng.NextDouble()-0.5f)*rad*0.28f;
        var m=new Mesh();m.SetVertices(v);m.SetTriangles(tri,0);m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    static Vector3 N(float x,float y,float z)=>new Vector3(x,y,z).normalized;
    static int Mid(int i1,int i2,List<Vector3> v,Dictionary<long,int> c,float r)
    {long k=((long)Mathf.Min(i1,i2)<<32)+Mathf.Max(i1,i2);if(c.TryGetValue(k,out int ret))return ret;
        int idx=v.Count;v.Add(((v[i1]+v[i2])*0.5f).normalized*r);c[k]=idx;return idx;}

    static Mesh MakeCone(float r,float h,int segs)
    {List<Vector3>v=new(){new(0,h,0),Vector3.zero};List<int>t=new();
        for(int i=0;i<segs;i++){float a=(float)i/segs*Mathf.PI*2;float rv=r*(1+Mathf.Sin(a*3)*0.12f);v.Add(new(Mathf.Cos(a)*rv,0,Mathf.Sin(a)*rv));}
        for(int i=0;i<segs;i++){int nx=(i+1)%segs;t.Add(0);t.Add(2+nx);t.Add(2+i);t.Add(1);t.Add(2+i);t.Add(2+nx);}
        var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;}

    static Mesh MakeRock(float s)
    {var m=MakeIco(s,1);List<Vector3>v=new(m.vertices);var rng=new System.Random(77);
        for(int i=0;i<v.Count;i++){var vx=v[i];if(vx.y<0)vx.y*=0.28f;vx+=new Vector3(((float)rng.NextDouble()-0.5f)*s*0.28f,((float)rng.NextDouble()-0.5f)*s*0.14f,((float)rng.NextDouble()-0.5f)*s*0.28f);v[i]=vx;}
        m.SetVertices(v);m.RecalculateNormals();m.RecalculateBounds();return m;}

    static Mesh MakePot()
    {var m=MakeIco(0.5f,1);List<Vector3>v=new(m.vertices);
        for(int i=0;i<v.Count;i++){var vx=v[i];float yf=Mathf.InverseLerp(-0.5f,0.5f,vx.y);float wm=0.55f+yf*0.55f;vx.x*=wm;vx.z*=wm;if(vx.y<-0.18f)vx.y=-0.18f;v[i]=vx;}
        m.SetVertices(v);m.RecalculateNormals();m.RecalculateBounds();return m;}

    static Mesh MakePyramid()
    {var m=new Mesh();m.vertices=new[]{new Vector3(-0.5f,0,-0.5f),new(0.5f,0,-0.5f),new(0.5f,0,0.5f),new(-0.5f,0,0.5f),new(0,1,0),Vector3.zero};
        m.triangles=new[]{0,4,1,1,4,2,2,4,3,3,4,0,0,1,5,1,2,5,2,3,5,3,0,5};m.RecalculateNormals();m.RecalculateBounds();return m;}

    static Mesh MakeWallMesh()
    {List<Vector3>v=new();List<int>t=new();float W=3,D=0.5f;int segs=8;var rng=new System.Random(111);
        for(int i=0;i<=segs;i++){float x=((float)i/segs-0.5f)*W;float th=2+(float)rng.NextDouble()*1.5f;
            v.Add(new(x,0,D/2));v.Add(new(x,th,D/2));v.Add(new(x,0,-D/2));v.Add(new(x,th,-D/2));}
        for(int i=0;i<segs;i++){int b=i*4;t.AddRange(new[]{b,b+1,b+4,b+1,b+5,b+4,b+2,b+6,b+3,b+3,b+6,b+7});}
        var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;}

    static Mesh MakePillar()
    {int segs=6;float r=0.38f,h=4;List<Vector3>v=new(){Vector3.zero,new(0,h,0)};List<int>t=new();
        for(int i=0;i<segs;i++){float a=(float)i/segs*Mathf.PI*2;v.Add(new(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r));}
        for(int i=0;i<segs;i++){float a=(float)i/segs*Mathf.PI*2;v.Add(new(Mathf.Cos(a)*r*0.82f,h,Mathf.Sin(a)*r*0.82f));}
        for(int i=0;i<segs;i++){int nx=(i+1)%segs;t.Add(0);t.Add(2+nx);t.Add(2+i);t.Add(1);t.Add(2+segs+i);t.Add(2+segs+nx);
            t.Add(2+i);t.Add(2+segs+i);t.Add(2+nx);t.Add(2+nx);t.Add(2+segs+i);t.Add(2+segs+nx);}
        var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;}

    static Mesh MakeArch()
    {List<Vector3>v=new();List<int>t=new();
        AB(v,t,new(-1.4f,2,0),new(.5f,4,.5f));AB(v,t,new(1.4f,2,0),new(.5f,4,.5f));AB(v,t,new(0,4.2f,0),new(3.3f,.55f,.5f));
        var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;}

    static void AB(List<Vector3>v,List<int>t,Vector3 c,Vector3 s)
    {int si=v.Count;var h=s*0.5f;
        v.Add(c+new Vector3(-h.x,-h.y,-h.z));v.Add(c+new Vector3(h.x,-h.y,-h.z));
        v.Add(c+new Vector3(h.x,h.y,-h.z));v.Add(c+new Vector3(-h.x,h.y,-h.z));
        v.Add(c+new Vector3(-h.x,-h.y,h.z));v.Add(c+new Vector3(h.x,-h.y,h.z));
        v.Add(c+new Vector3(h.x,h.y,h.z));v.Add(c+new Vector3(-h.x,h.y,h.z));
        int[] f={0,2,1,0,3,2,4,5,6,4,6,7,3,7,6,3,6,2,0,1,5,0,5,4,0,4,7,0,7,3,1,2,6,1,6,5};
        foreach(int fi in f)t.Add(si+fi);}
}
