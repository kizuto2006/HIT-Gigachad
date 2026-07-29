using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Combines static environment visuals into spatial chunks while preserving
/// every source GameObject and collider for gameplay.
/// </summary>
public static class EnvironmentChunkBaker
{
    private const string ScenePath = "Assets/Scenes/DesertArena.unity";
    private const string EnvironmentRootName = "Environment";
    private const string GeneratedRootName = "__BakedDecorChunks";
    private const string OutputFolder =
        "Assets/Generated/EnvironmentChunks/DesertArena";
    private const float ChunkSize = 28f;
    private const string BakeRequestFileName =
        "EnvironmentChunkBake.request";
    private const string OptimizeRequestFileName =
        "EnvironmentChunkOptimize.request";

    [InitializeOnLoadMethod]
    private static void RunRequestedBakeAfterReload()
    {
        if (!File.Exists(GetRequestPath(BakeRequestFileName)) &&
            !File.Exists(GetRequestPath(OptimizeRequestFileName)))
        {
            return;
        }

        EditorApplication.delayCall += TryRunRequestedBake;
    }

    private static void TryRunRequestedBake()
    {
        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryRunRequestedBake;
            return;
        }

        string bakeRequestPath = GetRequestPath(BakeRequestFileName);
        string optimizeRequestPath =
            GetRequestPath(OptimizeRequestFileName);
        if (!File.Exists(bakeRequestPath) &&
            !File.Exists(optimizeRequestPath))
        {
            return;
        }

        if (File.Exists(bakeRequestPath))
        {
            File.Delete(bakeRequestPath);
            BakeDesertArena();
        }

        if (File.Exists(optimizeRequestPath))
        {
            File.Delete(optimizeRequestPath);
            OptimizeGeneratedMeshes();
        }
    }

    private static string GetRequestPath(string requestFileName)
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName;
        return Path.Combine(
            projectRoot ?? string.Empty,
            "Temp",
            requestFileName);
    }

    [MenuItem("Tools/Performance/Bake Desert Arena Decor Chunks")]
    public static void BakeDesertArena()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(
                "Environment chunk baking is only available outside Play Mode.");
            return;
        }

        Scene scene = OpenTargetScene(out bool closeSceneAfterBake);
        if (!scene.IsValid())
        {
            return;
        }

        try
        {
            GameObject environmentRoot = FindRoot(scene, EnvironmentRootName);
            if (environmentRoot == null)
            {
                Debug.LogError(
                    $"Could not find '{EnvironmentRootName}' in {ScenePath}.");
                return;
            }

            RestoreInternal(environmentRoot, false);
            List<MeshRenderer> sources =
                CollectSourceRenderers(environmentRoot);
            if (sources.Count == 0)
            {
                Debug.LogWarning(
                    "No environment MeshRenderers were found to bake.");
                return;
            }

            Dictionary<string, ImporterRestoreState> readableImporters =
                MakeSourceMeshesReadable(sources);
            try
            {
                // Model reimports can replace Mesh instances, so collect again.
                sources = CollectSourceRenderers(environmentRoot);
                BakeSources(environmentRoot, sources);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                RestoreImporterReadability(readableImporters);
            }
        }
        finally
        {
            if (closeSceneAfterBake && scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [MenuItem("Tools/Performance/Restore Desert Arena Decor Renderers")]
    public static void RestoreDesertArena()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(
                "Environment chunk restore is only available outside Play Mode.");
            return;
        }

        Scene scene = OpenTargetScene(out bool closeSceneAfterRestore);
        if (!scene.IsValid())
        {
            return;
        }

        try
        {
            GameObject environmentRoot = FindRoot(
                scene,
                EnvironmentRootName);
            if (environmentRoot == null)
            {
                return;
            }

            RestoreInternal(environmentRoot, true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (closeSceneAfterRestore &&
                scene.IsValid() &&
                scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [MenuItem("Tools/Performance/Optimize Baked Decor Mesh Memory")]
    public static void OptimizeGeneratedMeshes()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            Debug.LogWarning(
                "No baked environment mesh folder was found.");
            return;
        }

        string[] meshGuids = AssetDatabase.FindAssets(
            "t:Mesh",
            new[] { OutputFolder });
        int optimizedCount = 0;
        for (int i = 0; i < meshGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(meshGuids[i]);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null || !mesh.isReadable)
            {
                continue;
            }

            mesh.UploadMeshData(true);
            EditorUtility.SetDirty(mesh);
            optimizedCount++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"Disabled runtime CPU mesh copies for {optimizedCount} " +
            "baked environment chunks.");
    }

    private static Scene OpenTargetScene(out bool openedByBaker)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.IsValid() && scene.isLoaded)
        {
            openedByBaker = false;
            return scene;
        }

        openedByBaker = true;
        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
    }

    private static GameObject FindRoot(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == objectName)
            {
                return roots[i];
            }
        }

        return null;
    }

    private static List<MeshRenderer> CollectSourceRenderers(
        GameObject environmentRoot)
    {
        List<MeshRenderer> sources = new List<MeshRenderer>(1024);
        MeshRenderer[] renderers =
            environmentRoot.GetComponentsInChildren<MeshRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null ||
                renderer.transform.name == GeneratedRootName ||
                IsUnderGeneratedRoot(renderer.transform) ||
                !renderer.gameObject.activeInHierarchy ||
                !renderer.enabled ||
                renderer.HasPropertyBlock())
            {
                continue;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null ||
                filter.sharedMesh == null ||
                renderer.sharedMaterials.Length == 0)
            {
                continue;
            }

            sources.Add(renderer);
        }

        return sources;
    }

    private static bool IsUnderGeneratedRoot(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == GeneratedRootName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static Dictionary<string, ImporterRestoreState>
        MakeSourceMeshesReadable(
        List<MeshRenderer> sources)
    {
        Dictionary<string, ImporterRestoreState> importerStates =
            new Dictionary<string, ImporterRestoreState>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < sources.Count; i++)
        {
            MeshFilter filter = sources[i].GetComponent<MeshFilter>();
            string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
            if (string.IsNullOrEmpty(path) ||
                importerStates.ContainsKey(path) ||
                AssetImporter.GetAtPath(path) is not ModelImporter importer)
            {
                continue;
            }

            string metaPath = GetAbsoluteProjectPath(path + ".meta");
            string originalMeta = File.Exists(metaPath)
                ? File.ReadAllText(metaPath)
                : null;
            importerStates.Add(
                path,
                new ImporterRestoreState(
                    importer.isReadable,
                    metaPath,
                    originalMeta));
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }

        return importerStates;
    }

    private static void RestoreImporterReadability(
        Dictionary<string, ImporterRestoreState> importerStates)
    {
        bool restoredRawMeta = false;
        foreach (KeyValuePair<string, ImporterRestoreState> pair
                 in importerStates)
        {
            ImporterRestoreState state = pair.Value;
            if (!string.IsNullOrEmpty(state.OriginalMeta) &&
                !string.IsNullOrEmpty(state.MetaPath))
            {
                string currentMeta = File.Exists(state.MetaPath)
                    ? File.ReadAllText(state.MetaPath)
                    : null;
                if (!string.Equals(
                        currentMeta,
                        state.OriginalMeta,
                        StringComparison.Ordinal))
                {
                    File.WriteAllText(
                        state.MetaPath,
                        state.OriginalMeta);
                    restoredRawMeta = true;
                }

                continue;
            }

            if (AssetImporter.GetAtPath(pair.Key) is ModelImporter importer &&
                importer.isReadable != state.WasReadable)
            {
                importer.isReadable = state.WasReadable;
                importer.SaveAndReimport();
            }
        }

        if (restoredRawMeta)
        {
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }
    }

    private static string GetAbsoluteProjectPath(string assetPath)
    {
        string projectRoot =
            Directory.GetParent(Application.dataPath)?.FullName;
        return Path.GetFullPath(
            Path.Combine(
                projectRoot ?? string.Empty,
                assetPath));
    }

    private static void BakeSources(
        GameObject environmentRoot,
        List<MeshRenderer> sources)
    {
        EnsureCleanOutputFolder();

        GameObject generatedRoot = new GameObject(GeneratedRootName);
        generatedRoot.transform.SetParent(environmentRoot.transform, false);

        Dictionary<GroupKey, List<SourceSubMesh>> groups =
            BuildGroups(
                sources,
                out HashSet<MeshRenderer> bakedSources);
        int meshIndex = 0;
        int sourceSubMeshCount = 0;

        foreach (KeyValuePair<GroupKey, List<SourceSubMesh>> pair in groups)
        {
            GroupKey key = pair.Key;
            List<SourceSubMesh> entries = pair.Value;
            if (entries.Count == 0)
            {
                continue;
            }

            Vector3 chunkCenter = new Vector3(
                (key.ChunkX + 0.5f) * ChunkSize,
                0f,
                (key.ChunkZ + 0.5f) * ChunkSize);
            string category = key.Category.ToString();
            string objectName =
                $"{category}_Chunk_{key.ChunkX}_{key.ChunkZ}_{meshIndex:000}";
            GameObject chunkObject = new GameObject(objectName);
            chunkObject.layer = key.Layer;
            chunkObject.transform.SetParent(generatedRoot.transform, false);
            chunkObject.transform.position = chunkCenter;

            CombineInstance[] combines = new CombineInstance[entries.Count];
            Matrix4x4 worldToChunk =
                chunkObject.transform.worldToLocalMatrix;
            for (int i = 0; i < entries.Count; i++)
            {
                SourceSubMesh entry = entries[i];
                combines[i] = new CombineInstance
                {
                    mesh = entry.Mesh,
                    subMeshIndex = entry.SubMeshIndex,
                    transform =
                        worldToChunk * entry.Renderer.transform.localToWorldMatrix,
                    lightmapScaleOffset =
                        entry.Renderer.lightmapScaleOffset,
                    realtimeLightmapScaleOffset =
                        entry.Renderer.realtimeLightmapScaleOffset
                };
            }

            Mesh combinedMesh = new Mesh
            {
                name = objectName,
                indexFormat = EstimateVertexCount(entries) > 65535
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };
            combinedMesh.CombineMeshes(
                combines,
                true,
                true,
                key.LightmapIndex >= 0);
            combinedMesh.RecalculateBounds();

            string meshPath =
                $"{OutputFolder}/{SanitizeFileName(objectName)}.asset";
            AssetDatabase.CreateAsset(combinedMesh, meshPath);
            combinedMesh.UploadMeshData(true);
            EditorUtility.SetDirty(combinedMesh);

            MeshFilter filter = chunkObject.AddComponent<MeshFilter>();
            filter.sharedMesh = combinedMesh;
            MeshRenderer renderer =
                chunkObject.AddComponent<MeshRenderer>();
            ApplyRendererSettings(renderer, key);

            StaticEditorFlags staticFlags =
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic;
            GameObjectUtility.SetStaticEditorFlags(
                chunkObject,
                staticFlags);

            sourceSubMeshCount += entries.Count;
            meshIndex++;
        }

        MeshRenderer[] sourceArray =
            new MeshRenderer[bakedSources.Count];
        bakedSources.CopyTo(sourceArray);
        for (int i = 0; i < sourceArray.Length; i++)
        {
            sourceArray[i].enabled = false;
        }

        EnvironmentChunkBakeState state =
            environmentRoot.GetComponent<EnvironmentChunkBakeState>();
        if (state == null)
        {
            state =
                environmentRoot.AddComponent<EnvironmentChunkBakeState>();
        }

        state.Configure(sourceArray, generatedRoot.transform);
        EditorUtility.SetDirty(state);

        Debug.Log(
            $"Baked {sourceArray.Length} source renderers / " +
            $"{sourceSubMeshCount} submeshes into {meshIndex} spatial chunks. " +
            "Original colliders remain active.");
    }

    private static Dictionary<GroupKey, List<SourceSubMesh>> BuildGroups(
        List<MeshRenderer> sources,
        out HashSet<MeshRenderer> bakedSources)
    {
        Dictionary<GroupKey, List<SourceSubMesh>> groups =
            new Dictionary<GroupKey, List<SourceSubMesh>>(256);
        bakedSources = new HashSet<MeshRenderer>();

        for (int i = 0; i < sources.Count; i++)
        {
            MeshRenderer renderer = sources[i];
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable)
            {
                Debug.LogWarning(
                    $"Skipping unreadable mesh '{mesh?.name}' on " +
                    $"'{renderer.name}'.",
                    renderer);
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            int subMeshCount = Mathf.Min(
                mesh.subMeshCount,
                materials.Length);
            Vector3 center = renderer.bounds.center;
            int chunkX = Mathf.FloorToInt(center.x / ChunkSize);
            int chunkZ = Mathf.FloorToInt(center.z / ChunkSize);
            PropCategory category = GetCategory(renderer.name);

            for (int subMeshIndex = 0;
                 subMeshIndex < subMeshCount;
                 subMeshIndex++)
            {
                Material material = materials[subMeshIndex];
                if (material == null)
                {
                    continue;
                }

                GroupKey key = new GroupKey(
                    chunkX,
                    chunkZ,
                    category,
                    material,
                    renderer.gameObject.layer,
                    renderer.shadowCastingMode,
                    renderer.receiveShadows,
                    renderer.lightmapIndex,
                    renderer.renderingLayerMask);
                if (!groups.TryGetValue(
                        key,
                        out List<SourceSubMesh> entries))
                {
                    entries = new List<SourceSubMesh>(16);
                    groups.Add(key, entries);
                }

                entries.Add(new SourceSubMesh(
                    renderer,
                    mesh,
                    subMeshIndex));
                bakedSources.Add(renderer);
            }
        }

        return groups;
    }

    private static long EstimateVertexCount(
        List<SourceSubMesh> entries)
    {
        long vertexCount = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            vertexCount += entries[i].Mesh.vertexCount;
        }

        return vertexCount;
    }

    private static void ApplyRendererSettings(
        MeshRenderer renderer,
        GroupKey key)
    {
        renderer.sharedMaterial = key.Material;
        renderer.shadowCastingMode = key.ShadowCastingMode;
        renderer.receiveShadows = key.ReceiveShadows;
        renderer.lightmapIndex = key.LightmapIndex;
        renderer.renderingLayerMask = key.RenderingLayerMask;
        renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
        renderer.motionVectorGenerationMode =
            MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = true;
    }

    private static PropCategory GetCategory(string objectName)
    {
        if (objectName.StartsWith(
                "Rock_",
                StringComparison.OrdinalIgnoreCase))
        {
            return PropCategory.Rock;
        }

        if (objectName.StartsWith(
                "Cactus_",
                StringComparison.OrdinalIgnoreCase))
        {
            return PropCategory.Cactus;
        }

        if (objectName.StartsWith(
                "Tree_",
                StringComparison.OrdinalIgnoreCase))
        {
            return PropCategory.Tree;
        }

        if (objectName.StartsWith(
                "Cliff",
                StringComparison.OrdinalIgnoreCase))
        {
            return PropCategory.Cliff;
        }

        return PropCategory.Decor;
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidCharacter, '_');
        }

        return value;
    }

    private static void EnsureCleanOutputFolder()
    {
        if (AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.DeleteAsset(OutputFolder);
        }

        string[] folders = OutputFolder.Split('/');
        string current = folders[0];
        for (int i = 1; i < folders.Length; i++)
        {
            string next = $"{current}/{folders[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, folders[i]);
            }

            current = next;
        }
    }

    private static void RestoreInternal(
        GameObject environmentRoot,
        bool deleteAssets)
    {
        EnvironmentChunkBakeState state =
            environmentRoot.GetComponent<EnvironmentChunkBakeState>();
        if (state != null)
        {
            MeshRenderer[] sourceRenderers = state.SourceRenderers;
            if (sourceRenderers != null)
            {
                for (int i = 0; i < sourceRenderers.Length; i++)
                {
                    if (sourceRenderers[i] != null)
                    {
                        sourceRenderers[i].enabled = true;
                    }
                }
            }

            if (state.GeneratedRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    state.GeneratedRoot.gameObject);
            }

            UnityEngine.Object.DestroyImmediate(state);
        }
        else
        {
            Transform staleRoot =
                environmentRoot.transform.Find(GeneratedRootName);
            if (staleRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(staleRoot.gameObject);
            }
        }

        if (deleteAssets &&
            AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.DeleteAsset(OutputFolder);
        }
    }

    private enum PropCategory : byte
    {
        Rock,
        Cactus,
        Tree,
        Cliff,
        Decor
    }

    private readonly struct SourceSubMesh
    {
        public readonly MeshRenderer Renderer;
        public readonly Mesh Mesh;
        public readonly int SubMeshIndex;

        public SourceSubMesh(
            MeshRenderer renderer,
            Mesh mesh,
            int subMeshIndex)
        {
            Renderer = renderer;
            Mesh = mesh;
            SubMeshIndex = subMeshIndex;
        }
    }

    private readonly struct ImporterRestoreState
    {
        public readonly bool WasReadable;
        public readonly string MetaPath;
        public readonly string OriginalMeta;

        public ImporterRestoreState(
            bool wasReadable,
            string metaPath,
            string originalMeta)
        {
            WasReadable = wasReadable;
            MetaPath = metaPath;
            OriginalMeta = originalMeta;
        }
    }

    private readonly struct GroupKey : IEquatable<GroupKey>
    {
        public readonly int ChunkX;
        public readonly int ChunkZ;
        public readonly PropCategory Category;
        public readonly Material Material;
        public readonly int Layer;
        public readonly ShadowCastingMode ShadowCastingMode;
        public readonly bool ReceiveShadows;
        public readonly int LightmapIndex;
        public readonly uint RenderingLayerMask;

        public GroupKey(
            int chunkX,
            int chunkZ,
            PropCategory category,
            Material material,
            int layer,
            ShadowCastingMode shadowCastingMode,
            bool receiveShadows,
            int lightmapIndex,
            uint renderingLayerMask)
        {
            ChunkX = chunkX;
            ChunkZ = chunkZ;
            Category = category;
            Material = material;
            Layer = layer;
            ShadowCastingMode = shadowCastingMode;
            ReceiveShadows = receiveShadows;
            LightmapIndex = lightmapIndex;
            RenderingLayerMask = renderingLayerMask;
        }

        public bool Equals(GroupKey other)
        {
            return ChunkX == other.ChunkX &&
                ChunkZ == other.ChunkZ &&
                Category == other.Category &&
                Material == other.Material &&
                Layer == other.Layer &&
                ShadowCastingMode == other.ShadowCastingMode &&
                ReceiveShadows == other.ReceiveShadows &&
                LightmapIndex == other.LightmapIndex &&
                RenderingLayerMask == other.RenderingLayerMask;
        }

        public override bool Equals(object obj)
        {
            return obj is GroupKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ChunkX;
                hash = (hash * 397) ^ ChunkZ;
                hash = (hash * 397) ^ (int)Category;
                hash = (hash * 397) ^
                    (Material != null ? Material.GetInstanceID() : 0);
                hash = (hash * 397) ^ Layer;
                hash = (hash * 397) ^ (int)ShadowCastingMode;
                hash = (hash * 397) ^ ReceiveShadows.GetHashCode();
                hash = (hash * 397) ^ LightmapIndex;
                hash = (hash * 397) ^
                    RenderingLayerMask.GetHashCode();
                return hash;
            }
        }
    }
}
