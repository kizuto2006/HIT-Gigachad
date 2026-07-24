using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class EnemyMeshFlipbookBakerWindow : EditorWindow
{
    private GameObject sourceModel;
    private AnimationClip animationClip;
    private Material[] materialOverrides = Array.Empty<Material>();
    private int poseCount = 8;
    private float playbackFramesPerSecond = 8f;
    private int phaseBuckets = 4;
    private string outputRoot = "Assets/Generated/MeshFlipbook";
    private string outputName = "Enemy";
    private GameObject targetEnemyPrefab;
    private bool replaceTargetVisual;
    private string targetVisualPath = "Visual";
    private Vector2 scrollPosition;

    [MenuItem("Tools/Gigachad/Megabonk Style/Open Mesh Flipbook Baker")]
    public static void Open()
    {
        GetWindow<EnemyMeshFlipbookBakerWindow>("Mesh Flipbook Baker");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.LabelField("Megabonk-Style Mesh Flipbook Baker", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Bake animation thành một số mesh pose tĩnh. Runtime đổi sharedMesh bằng manager tập trung, " +
            "không dùng Animator, SkinnedMeshRenderer hoặc VAT texture.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        sourceModel = (GameObject)EditorGUILayout.ObjectField(
            "Source Model",
            sourceModel,
            typeof(GameObject),
            false);
        if (EditorGUI.EndChangeCheck())
        {
            AutoConfigureFromSource();
        }

        animationClip = (AnimationClip)EditorGUILayout.ObjectField(
            "Animation Clip",
            animationClip,
            typeof(AnimationClip),
            false);
        poseCount = EditorGUILayout.IntSlider(
            new GUIContent("Pose Count", "Số mesh pose trong một vòng animation. 8 phù hợp enemy thường."),
            poseCount,
            2,
            24);
        playbackFramesPerSecond = EditorGUILayout.Slider(
            new GUIContent("Playback FPS", "Tốc độ đổi pose ở runtime."),
            playbackFramesPerSecond,
            2f,
            20f);
        phaseBuckets = EditorGUILayout.IntSlider(
            new GUIContent("Phase Buckets", "Số nhóm lệch phase. Ít nhóm giúp batching tốt hơn."),
            phaseBuckets,
            1,
            8);

        DrawMaterialOverrides();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        outputRoot = EditorGUILayout.TextField("Output Root", outputRoot);
        outputName = EditorGUILayout.TextField("Output Name", outputName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Optional Prefab Integration", EditorStyles.boldLabel);
        targetEnemyPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Enemy Prefab",
            targetEnemyPrefab,
            typeof(GameObject),
            false);
        using (new EditorGUI.DisabledScope(targetEnemyPrefab == null))
        {
            replaceTargetVisual = EditorGUILayout.Toggle("Replace Target Visual", replaceTargetVisual);
            targetVisualPath = EditorGUILayout.TextField("Visual Path", targetVisualPath);
        }

        string validationError = ValidateInput();
        if (!string.IsNullOrEmpty(validationError))
        {
            EditorGUILayout.HelpBox(validationError, MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(validationError)))
        {
            if (GUILayout.Button("Bake Mesh Flipbook", GUILayout.Height(36f)))
            {
                Bake();
            }
        }

        EditorGUILayout.HelpBox(
            "Kết quả ưu tiên số lượng enemy: animation cố ý giật, Unlit, không shadow, không blend clip và không animation event.",
            MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void AutoConfigureFromSource()
    {
        if (sourceModel == null)
        {
            animationClip = null;
            materialOverrides = Array.Empty<Material>();
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath(sourceModel);
        AnimationClip detectedClip = GenericEnemyMeshFlipbookBaker.FindFirstRuntimeClip(sourcePath);
        if (detectedClip != null)
        {
            animationClip = detectedClip;
        }

        outputName = GenericEnemyMeshFlipbookBaker.MakeSafeAssetName(sourceModel.name);
        ResizeMaterialOverrides();
    }

    private void DrawMaterialOverrides()
    {
        if (sourceModel == null)
        {
            return;
        }

        ResizeMaterialOverrides();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Material Overrides", EditorStyles.boldLabel);
        int slot = 0;
        SkinnedMeshRenderer[] renderers = sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Mesh mesh = renderers[rendererIndex].sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                materialOverrides[slot] = (Material)EditorGUILayout.ObjectField(
                    $"{renderers[rendererIndex].name} [{subMesh}]",
                    materialOverrides[slot],
                    typeof(Material),
                    false);
                slot++;
            }
        }
    }

    private void ResizeMaterialOverrides()
    {
        int slotCount = 0;
        if (sourceModel != null)
        {
            SkinnedMeshRenderer[] renderers = sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].sharedMesh != null)
                {
                    slotCount += renderers[i].sharedMesh.subMeshCount;
                }
            }
        }

        if (materialOverrides == null)
        {
            materialOverrides = new Material[slotCount];
        }
        else if (materialOverrides.Length != slotCount)
        {
            Array.Resize(ref materialOverrides, slotCount);
        }
    }

    private string ValidateInput()
    {
        if (sourceModel == null || animationClip == null)
        {
            return "Hãy chọn Source Model và Animation Clip.";
        }
        if (!EditorUtility.IsPersistent(sourceModel))
        {
            return "Source Model phải là asset trong Project.";
        }
        if (sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
        {
            return "Source Model không có SkinnedMeshRenderer.";
        }
        if (string.IsNullOrWhiteSpace(outputRoot) || !outputRoot.StartsWith("Assets", StringComparison.Ordinal))
        {
            return "Output Root phải nằm trong Assets.";
        }
        if (replaceTargetVisual && targetEnemyPrefab == null)
        {
            return "Hãy chọn Enemy Prefab hoặc tắt Replace Target Visual.";
        }
        return null;
    }

    private void Bake()
    {
        try
        {
            MeshFlipbookBakeResult result = GenericEnemyMeshFlipbookBaker.Bake(new MeshFlipbookBakeRequest
            {
                sourceModel = sourceModel,
                animationClip = animationClip,
                materialOverrides = materialOverrides,
                poseCount = poseCount,
                playbackFramesPerSecond = playbackFramesPerSecond,
                phaseBuckets = phaseBuckets,
                outputRoot = outputRoot,
                outputName = outputName,
                targetEnemyPrefab = targetEnemyPrefab,
                replaceTargetVisual = replaceTargetVisual,
                targetVisualPath = targetVisualPath
            });
            Selection.activeObject = result.prefab;
            EditorGUIUtility.PingObject(result.prefab);
            ShowNotification(new GUIContent($"Baked {result.frames.Length} mesh poses"));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Mesh Flipbook Bake Failed", exception.Message, "OK");
        }
    }
}

public sealed class MeshFlipbookBakeRequest
{
    public GameObject sourceModel;
    public AnimationClip animationClip;
    public Material[] materialOverrides;
    public int poseCount = 8;
    public float playbackFramesPerSecond = 8f;
    public int phaseBuckets = 4;
    public string outputRoot = "Assets/Generated/MeshFlipbook";
    public string outputName = "Enemy";
    public GameObject targetEnemyPrefab;
    public bool replaceTargetVisual;
    public string targetVisualPath = "Visual";
}

public sealed class MeshFlipbookBakeResult
{
    public GameObject prefab;
    public Mesh[] frames;
    public Material[] materials;
}

public static class GenericEnemyMeshFlipbookBaker
{
    private const string ShaderName = "Gigachad/Megabonk/Toon Lit";

    private sealed class RendererBakeInfo
    {
        public SkinnedMeshRenderer renderer;
        public Mesh sampledMesh;
        public int vertexOffset;
    }

    public static MeshFlipbookBakeResult Bake(MeshFlipbookBakeRequest request)
    {
        ValidateRequest(request);
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            throw new InvalidOperationException($"Không tìm thấy shader {ShaderName}.");
        }

        string safeName = MakeSafeAssetName(request.outputName);
        string outputFolder = request.outputRoot.TrimEnd('/') + "/" + safeName;
        EnsureFolder(outputFolder);

        GameObject instance = UnityEngine.Object.Instantiate(request.sourceModel);
        instance.name = safeName + "_FlipbookBakeSource";
        instance.hideFlags = HideFlags.HideAndDontSave;

        List<RendererBakeInfo> infos = null;
        try
        {
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.enabled = false;
            }

            infos = BuildBakeInfos(instance.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            int vertexCount = GetVertexCount(infos);
            BuildSourceGeometry(infos, vertexCount, out Vector2[] sourceUV, out List<int[]> subMeshTriangles);

            Mesh[] frames = new Mesh[request.poseCount];
            for (int frame = 0; frame < request.poseCount; frame++)
            {
                float normalizedTime = frame / (float)request.poseCount;
                request.animationClip.SampleAnimation(
                    instance,
                    request.animationClip.length * normalizedTime);

                Vector3[] vertices = new Vector3[vertexCount];
                Vector3[] normals = new Vector3[vertexCount];
                SamplePose(instance, infos, vertices, normals);

                string framePath = outputFolder + "/" + safeName + $"_Pose_{frame:D2}.asset";
                frames[frame] = CreateOrUpdatePoseMesh(
                    safeName + $"_Pose_{frame:D2}",
                    vertices,
                    normals,
                    sourceUV,
                    subMeshTriangles,
                    framePath);
            }

            Material[] materials = CreateOrUpdateMaterials(
                safeName,
                outputFolder,
                shader,
                infos,
                request.materialOverrides);
            string prefabPath = outputFolder + "/" + safeName + "_Flipbook.prefab";
            GameObject prefab = CreateOrUpdatePrefab(
                prefabPath,
                safeName,
                frames,
                materials,
                request.playbackFramesPerSecond,
                request.phaseBuckets);

            if (request.replaceTargetVisual && request.targetEnemyPrefab != null)
            {
                IntegrateIntoTargetPrefab(
                    request.targetEnemyPrefab,
                    request.targetVisualPath,
                    prefab);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Mesh Flipbook bake hoàn tất: {safeName}, {vertexCount} vertices, " +
                $"{frames.Length} poses, {request.playbackFramesPerSecond:0.#} FPS.");

            return new MeshFlipbookBakeResult
            {
                prefab = prefab,
                frames = frames,
                materials = materials
            };
        }
        finally
        {
            if (infos != null)
            {
                for (int i = 0; i < infos.Count; i++)
                {
                    UnityEngine.Object.DestroyImmediate(infos[i].sampledMesh);
                }
            }
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    public static AnimationClip FindFirstRuntimeClip(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return null;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip != null && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            {
                return clip;
            }
        }
        return null;
    }

    public static string MakeSafeAssetName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Enemy";
        }

        string safe = value.Trim();
        char[] invalidCharacters = System.IO.Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidCharacters.Length; i++)
        {
            safe = safe.Replace(invalidCharacters[i], '_');
        }
        return safe.Replace('/', '_').Replace('\\', '_');
    }

    private static void ValidateRequest(MeshFlipbookBakeRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
        if (request.sourceModel == null || request.animationClip == null)
        {
            throw new InvalidOperationException("Source Model và Animation Clip là bắt buộc.");
        }
        if (request.poseCount < 2 || request.poseCount > 64)
        {
            throw new InvalidOperationException("Pose Count phải nằm trong khoảng 2-64.");
        }
        if (request.playbackFramesPerSecond <= 0f)
        {
            throw new InvalidOperationException("Playback FPS phải lớn hơn 0.");
        }
        if (request.phaseBuckets < 1 || request.phaseBuckets > request.poseCount)
        {
            throw new InvalidOperationException("Phase Buckets phải từ 1 đến Pose Count.");
        }
        if (string.IsNullOrWhiteSpace(request.outputRoot) ||
            !request.outputRoot.StartsWith("Assets", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Output Root phải nằm trong Assets.");
        }
    }

    private static List<RendererBakeInfo> BuildBakeInfos(SkinnedMeshRenderer[] renderers)
    {
        List<RendererBakeInfo> infos = new List<RendererBakeInfo>();
        int vertexOffset = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Mesh sourceMesh = renderers[i].sharedMesh;
            if (sourceMesh == null || sourceMesh.vertexCount == 0)
            {
                continue;
            }

            infos.Add(new RendererBakeInfo
            {
                renderer = renderers[i],
                sampledMesh = new Mesh { name = renderers[i].name + "_FlipbookSample" },
                vertexOffset = vertexOffset
            });
            vertexOffset += sourceMesh.vertexCount;
        }

        if (infos.Count == 0)
        {
            throw new InvalidOperationException("Không tìm thấy SkinnedMeshRenderer có mesh hợp lệ.");
        }
        return infos;
    }

    private static int GetVertexCount(List<RendererBakeInfo> infos)
    {
        RendererBakeInfo last = infos[infos.Count - 1];
        return last.vertexOffset + last.renderer.sharedMesh.vertexCount;
    }

    private static void BuildSourceGeometry(
        List<RendererBakeInfo> infos,
        int vertexCount,
        out Vector2[] sourceUV,
        out List<int[]> subMeshTriangles)
    {
        sourceUV = new Vector2[vertexCount];
        subMeshTriangles = new List<int[]>();
        for (int infoIndex = 0; infoIndex < infos.Count; infoIndex++)
        {
            RendererBakeInfo info = infos[infoIndex];
            Mesh sourceMesh = info.renderer.sharedMesh;
            Vector2[] rendererUV = sourceMesh.uv;
            if (rendererUV != null && rendererUV.Length == sourceMesh.vertexCount)
            {
                Array.Copy(rendererUV, 0, sourceUV, info.vertexOffset, rendererUV.Length);
            }

            for (int subMesh = 0; subMesh < sourceMesh.subMeshCount; subMesh++)
            {
                int[] triangles = sourceMesh.GetTriangles(subMesh);
                for (int triangleIndex = 0; triangleIndex < triangles.Length; triangleIndex++)
                {
                    triangles[triangleIndex] += info.vertexOffset;
                }
                subMeshTriangles.Add(triangles);
            }
        }
    }

    private static void SamplePose(
        GameObject instance,
        List<RendererBakeInfo> infos,
        Vector3[] vertices,
        Vector3[] normals)
    {
        for (int infoIndex = 0; infoIndex < infos.Count; infoIndex++)
        {
            RendererBakeInfo info = infos[infoIndex];
            info.renderer.BakeMesh(info.sampledMesh, true);
            Vector3[] sampledVertices = info.sampledMesh.vertices;
            Vector3[] sampledNormals = info.sampledMesh.normals;
            int expectedCount = info.renderer.sharedMesh.vertexCount;
            if (sampledVertices.Length != expectedCount || sampledNormals.Length != expectedCount)
            {
                throw new InvalidOperationException($"{info.renderer.name} trả về vertex/normal count không hợp lệ.");
            }

            Matrix4x4 rendererToRoot =
                instance.transform.worldToLocalMatrix * info.renderer.transform.localToWorldMatrix;
            Matrix4x4 normalToRoot = rendererToRoot.inverse.transpose;
            for (int vertex = 0; vertex < sampledVertices.Length; vertex++)
            {
                vertices[info.vertexOffset + vertex] =
                    rendererToRoot.MultiplyPoint3x4(sampledVertices[vertex]);
                normals[info.vertexOffset + vertex] =
                    normalToRoot.MultiplyVector(sampledNormals[vertex]).normalized;
            }
        }
    }

    private static Mesh CreateOrUpdatePoseMesh(
        string meshName,
        Vector3[] vertices,
        Vector3[] normals,
        Vector2[] sourceUV,
        List<int[]> subMeshTriangles,
        string assetPath)
    {
        Mesh created = new Mesh
        {
            name = meshName,
            indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
        };
        created.vertices = vertices;
        created.normals = normals;
        created.uv = sourceUV;
        created.subMeshCount = subMeshTriangles.Count;
        for (int subMesh = 0; subMesh < subMeshTriangles.Count; subMesh++)
        {
            created.SetTriangles(subMeshTriangles[subMesh], subMesh, false);
        }
        created.RecalculateBounds();

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(created, assetPath);
            return created;
        }

        EditorUtility.CopySerialized(created, existing);
        UnityEngine.Object.DestroyImmediate(created);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static Material[] CreateOrUpdateMaterials(
        string safeName,
        string outputFolder,
        Shader shader,
        List<RendererBakeInfo> infos,
        Material[] materialOverrides)
    {
        List<Material> output = new List<Material>();
        Dictionary<int, Material> cache = new Dictionary<int, Material>();
        int uniqueIndex = 0;
        for (int infoIndex = 0; infoIndex < infos.Count; infoIndex++)
        {
            Mesh mesh = infos[infoIndex].renderer.sharedMesh;
            Material[] sourceMaterials = infos[infoIndex].renderer.sharedMaterials;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int slot = output.Count;
                Material source = materialOverrides != null &&
                    slot < materialOverrides.Length &&
                    materialOverrides[slot] != null
                        ? materialOverrides[slot]
                        : sourceMaterials.Length == 0
                            ? null
                            : sourceMaterials[Mathf.Min(subMesh, sourceMaterials.Length - 1)];
                int cacheKey = source != null ? source.GetInstanceID() : 0;
                if (!cache.TryGetValue(cacheKey, out Material material))
                {
                    string materialPath = outputFolder + "/" + safeName + $"_Material_{uniqueIndex}.mat";
                    material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null)
                    {
                        material = new Material(shader) { name = safeName + $"_Flipbook_{uniqueIndex}" };
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    else
                    {
                        material.shader = shader;
                    }

                    material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                    material.SetColor("_BaseColor", Color.white);
                    if (source != null)
                    {
                        if (source.HasProperty("_BaseMap"))
                        {
                            material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
                            material.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                            material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
                        }
                        if (source.HasProperty("_BaseColor"))
                        {
                            material.SetColor("_BaseColor", source.GetColor("_BaseColor"));
                        }
                    }
                    material.enableInstancing = true;
                    EditorUtility.SetDirty(material);
                    cache.Add(cacheKey, material);
                    uniqueIndex++;
                }
                output.Add(material);
            }
        }
        return output.ToArray();
    }

    private static GameObject CreateOrUpdatePrefab(
        string prefabPath,
        string safeName,
        Mesh[] frames,
        Material[] materials,
        float playbackFramesPerSecond,
        int phaseBuckets)
    {
        GameObject temporaryRoot = new GameObject(safeName + "_Flipbook");
        try
        {
            MeshFilter meshFilter = temporaryRoot.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = frames[0];
            MeshRenderer renderer = temporaryRoot.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            EnemyMeshFlipbookAnimator flipbook = temporaryRoot.AddComponent<EnemyMeshFlipbookAnimator>();
            flipbook.Configure(frames, playbackFramesPerSecond, phaseBuckets);
            return PrefabUtility.SaveAsPrefabAsset(temporaryRoot, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(temporaryRoot);
        }
    }

    private static void IntegrateIntoTargetPrefab(
        GameObject targetPrefab,
        string targetVisualPath,
        GameObject visualPrefab)
    {
        string targetPath = AssetDatabase.GetAssetPath(targetPrefab);
        GameObject targetRoot = PrefabUtility.LoadPrefabContents(targetPath);
        try
        {
            Transform visualRoot = targetRoot.transform.Find(targetVisualPath);
            if (visualRoot == null)
            {
                throw new InvalidOperationException(
                    $"Không tìm thấy Visual Path '{targetVisualPath}' trong {targetPrefab.name}.");
            }

            for (int child = visualRoot.childCount - 1; child >= 0; child--)
            {
                UnityEngine.Object.DestroyImmediate(visualRoot.GetChild(child).gameObject);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
            instance.transform.SetParent(visualRoot, false);
            PrefabUtility.SaveAsPrefabAsset(targetRoot, targetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(targetRoot);
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        string normalized = folderPath.Replace('\\', '/').TrimEnd('/');
        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
