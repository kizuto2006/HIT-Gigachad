using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class EnemyVATBakerWindow : EditorWindow
{
    private GameObject sourceModel;
    private AnimationClip animationClip;
    private Material[] materialOverrides = Array.Empty<Material>();
    private float materialBrightness = 1f;
    private float nearToVatHeight = 0.5f;
    private float vatToFarHeight = 0.2f;
    private float farCullHeight = 0.03f;

    private GameObject targetEnemyPrefab;
    private string outputRoot = "Assets/Generated/VAT";
    private string outputName = "Enemy";
    private int sampleRate = 30;
    private bool replaceTargetVisual;
    private string targetVisualPath = "Visual";
    private Vector2 scrollPosition;

    [MenuItem("Tools/Gigachad/VAT/Open Generic Baker")]
    public static void Open()
    {
        GetWindow<EnemyVATBakerWindow>("Enemy VAT Baker");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Generic Enemy VAT Baker", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Bake animation của SkinnedMeshRenderer thành texture position/normal. " +
            "Tool hỗ trợ nhiều SkinnedMeshRenderer, submesh và material trong cùng model.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        sourceModel = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Source Model", "FBX hoặc prefab chứa rig và SkinnedMeshRenderer."),
            sourceModel,
            typeof(GameObject),
            false);
        if (EditorGUI.EndChangeCheck())
        {
            AutoConfigureFromSource();
        }

        animationClip = (AnimationClip)EditorGUILayout.ObjectField(
            new GUIContent("Animation Clip", "Có thể là clip nằm trong source FBX hoặc animation FBX khác."),
            animationClip,
            typeof(AnimationClip),
            false);
        sampleRate = EditorGUILayout.IntSlider(
            new GUIContent("Sample Rate", "Số frame bake mỗi giây. 30 phù hợp với phần lớn enemy."),
            sampleRate,
            1,
            60);

        DrawMaterialOverrides();
        materialBrightness = EditorGUILayout.Slider(
            new GUIContent("Material Brightness", "Để 1.0 để giữ đúng Base Color gốc; chỉ chỉnh khi cần bù sáng thủ công."),
            materialBrightness,
            0.5f,
            2f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hybrid LOD", EditorStyles.boldLabel);
        nearToVatHeight = EditorGUILayout.Slider(
            new GUIContent("Near → VAT", "LOD0 dùng SkinnedMeshRenderer; dưới tỷ lệ màn hình này chuyển sang VAT PBR."),
            nearToVatHeight,
            0.25f,
            0.9f);
        vatToFarHeight = EditorGUILayout.Slider(
            new GUIContent("VAT → Far", "LOD1 và LOD2 cùng dùng VAT Unlit để giữ màu ổn định như bản ở xa."),
            vatToFarHeight,
            0.05f,
            0.5f);
        farCullHeight = EditorGUILayout.Slider(
            new GUIContent("Far Cull", "LOD2 VAT Unlit sẽ bị cull dưới tỷ lệ màn hình này."),
            farCullHeight,
            0.005f,
            0.1f);
        vatToFarHeight = Mathf.Min(vatToFarHeight, nearToVatHeight - 0.01f);
        farCullHeight = Mathf.Min(farCullHeight, vatToFarHeight - 0.01f);


        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        outputRoot = EditorGUILayout.TextField("Output Root", outputRoot);
        outputName = EditorGUILayout.TextField("Output Name", outputName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Optional Prefab Integration", EditorStyles.boldLabel);
        targetEnemyPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Enemy Prefab", "Để trống nếu chỉ muốn tạo VAT prefab độc lập."),
            targetEnemyPrefab,
            typeof(GameObject),
            false);
        using (new EditorGUI.DisabledScope(targetEnemyPrefab == null))
        {
            replaceTargetVisual = EditorGUILayout.Toggle(
                new GUIContent("Replace Target Visual", "Xóa các child hiện tại dưới Visual Path và gắn VAT prefab."),
                replaceTargetVisual);
            targetVisualPath = EditorGUILayout.TextField("Visual Path", targetVisualPath);
        }

        EditorGUILayout.Space();
        DrawEstimate();

        string validationError = ValidateInput();
        if (!string.IsNullOrEmpty(validationError))
        {
            EditorGUILayout.HelpBox(validationError, MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(validationError)))
        {
            if (GUILayout.Button("Bake VAT", GUILayout.Height(36f)))
            {
                Bake();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Hybrid output dùng màu Unlit đồng nhất: LOD0 Skinned, LOD1 VAT, LOD2 VAT rồi cull. " +
            "Normal map, emission, transparency và blend nhiều animation vẫn cần mở rộng riêng.",
            MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    private void AutoConfigureFromSource()
    {
        if (sourceModel == null)
        {
            animationClip = null;
            return;
        }

        string sourcePath = AssetDatabase.GetAssetPath(sourceModel);
        AnimationClip detectedClip = GenericEnemyVATBaker.FindFirstRuntimeClip(sourcePath);
        if (detectedClip != null)
        {
            animationClip = detectedClip;
        }

        outputName = GenericEnemyVATBaker.MakeSafeAssetName(sourceModel.name);
        ResizeMaterialOverrides();
        Repaint();
    }

    private void DrawEstimate()
    {
        if (sourceModel == null || animationClip == null)
        {
            return;
        }

        SkinnedMeshRenderer[] renderers = sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        int vertexCount = 0;
        int subMeshCount = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].sharedMesh == null)
            {
                continue;
            }

            vertexCount += renderers[i].sharedMesh.vertexCount;
            subMeshCount += renderers[i].sharedMesh.subMeshCount;
        }

        int frames = Mathf.Max(2, Mathf.CeilToInt(animationClip.length * sampleRate));
        long textureBytes = (long)vertexCount * frames * 8L * 2L;
        EditorGUILayout.HelpBox(
            $"Estimate: {vertexCount:N0} vertices, {frames:N0} frames, {subMeshCount} submeshes, " +
            $"~{EditorUtility.FormatBytes(textureBytes)} cho position + normal.",
            MessageType.None);
    }

    private string ValidateInput()
    {
        if (sourceModel == null)
        {
            return "Hãy chọn Source Model.";
        }

        if (!EditorUtility.IsPersistent(sourceModel))
        {
            return "Source Model phải là asset trong Project, không phải object trong scene.";
        }

        if (animationClip == null)
        {
            return "Hãy chọn Animation Clip.";
        }

        if (sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
        {
            return "Source Model không có SkinnedMeshRenderer.";
        }

        if (string.IsNullOrWhiteSpace(outputRoot) || !outputRoot.StartsWith("Assets", StringComparison.Ordinal))
        {
            return "Output Root phải nằm trong Assets.";
        }

        if (string.IsNullOrWhiteSpace(outputName))
        {
            return "Output Name không được để trống.";
        }

        if (targetEnemyPrefab != null && !PrefabUtility.IsPartOfPrefabAsset(targetEnemyPrefab))
        {
            return "Enemy Prefab phải là prefab asset trong Project.";
        }

        if (replaceTargetVisual && targetEnemyPrefab == null)
        {
            return "Hãy chọn Enemy Prefab hoặc tắt Replace Target Visual.";
        }

        if (replaceTargetVisual && string.IsNullOrWhiteSpace(targetVisualPath))
        {
            return "Visual Path không được để trống.";
        }

        return null;
    }

    private void Bake()
    {
        try
        {
            VATBakeRequest request = new VATBakeRequest
            {
                sourceModel = sourceModel,
                animationClip = animationClip,
                materialOverrides = materialOverrides,
                materialBrightness = materialBrightness,
                nearToVatHeight = nearToVatHeight,
                vatToFarHeight = vatToFarHeight,
                farCullHeight = farCullHeight,

                sampleRate = sampleRate,
                outputRoot = outputRoot,
                outputName = outputName,
                targetEnemyPrefab = targetEnemyPrefab,
                replaceTargetVisual = replaceTargetVisual,
                targetVisualPath = targetVisualPath
            };

            VATBakeResult result = GenericEnemyVATBaker.Bake(request);
            Selection.activeObject = result.vatPrefab;
            EditorGUIUtility.PingObject(result.vatPrefab);
            ShowNotification(new GUIContent($"Baked {result.vertexCount:N0} vertices / {result.frameCount} frames"));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("VAT Bake Failed", exception.Message, "OK");
        }
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
        EditorGUILayout.HelpBox(
            "Gán material gốc cho từng submesh nếu FBX đang dùng material mặc định/rỗng. " +
            "Để trống để tool tự lấy material từ Source Model.",
            MessageType.None);

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
}

public sealed class VATBakeRequest
{
    public GameObject sourceModel;
    public AnimationClip animationClip;
    public Material[] materialOverrides;
    public float materialBrightness = 1f;
    public float nearToVatHeight = 0.5f;
    public float vatToFarHeight = 0.2f;
    public float farCullHeight = 0.03f;

    public int sampleRate = 30;
    public string outputRoot = "Assets/Generated/VAT";
    public string outputName = "Enemy";
    public GameObject targetEnemyPrefab;
    public bool replaceTargetVisual;
    [Range(0f, 0.5f)] public float loopBlendFraction;
    public string targetVisualPath = "Visual";
}

public sealed class VATBakeResult
{
    public GameObject vatPrefab;
    public Mesh mesh;
    public Texture2D positionTexture;
    public Texture2D normalTexture;
    public int vertexCount;
    public int frameCount;
}

public static class GenericEnemyVATBaker
{
    private const string ShaderName = "Gigachad/VAT/Enemy Unlit";
    private const string NearShaderName = "Gigachad/Hybrid/Enemy Skinned Unlit";


    private sealed class RendererBakeInfo
    {
        public SkinnedMeshRenderer renderer;
        public Mesh sampledMesh;
        public int vertexOffset;
    }

    public static VATBakeResult Bake(VATBakeRequest request)
    {
        ValidateRequest(request);

        Shader shader = Shader.Find(ShaderName);
        Shader nearShader = Shader.Find(NearShaderName);

        if (shader == null)
        {
            throw new InvalidOperationException($"Không tìm thấy shader {ShaderName}.");
        }

        if (nearShader == null)
        {
            throw new InvalidOperationException($"Không tìm thấy shader {NearShaderName}.");
        }


        string safeName = MakeSafeAssetName(request.outputName);
        string outputFolder = request.outputRoot.TrimEnd('/') + "/" + safeName;
        EnsureFolder(outputFolder);

        GameObject instance = UnityEngine.Object.Instantiate(request.sourceModel);
        instance.name = safeName + "_VAT_BakeSource";
        instance.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.enabled = false;
            }

            SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            List<RendererBakeInfo> bakeInfos = BuildBakeInfos(renderers);
            int vertexCount = GetVertexCount(bakeInfos);
            int frameCount = Mathf.Max(2, Mathf.CeilToInt(request.animationClip.length * request.sampleRate));

            if (vertexCount > SystemInfo.maxTextureSize || frameCount > SystemInfo.maxTextureSize)
            {
                throw new InvalidOperationException(
                    $"VAT texture {vertexCount}x{frameCount} vượt maxTextureSize {SystemInfo.maxTextureSize}. " +
                    "Hãy giảm vertex hoặc sample rate.");
            }

            Color[] positionPixels = new Color[vertexCount * frameCount];
            Color[] normalPixels = new Color[vertexCount * frameCount];
            Vector3[] firstFrameVertices = new Vector3[vertexCount];
            Vector3[] firstFrameNormals = new Vector3[vertexCount];
            Vector3 boundsMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 boundsMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            try
            {
                SampleFrames(
                    request,
                    instance,
                    bakeInfos,
                    vertexCount,
                    frameCount,
                    positionPixels,
                    normalPixels,
                    firstFrameVertices,
                    firstFrameNormals,
                    ref boundsMin,
                    ref boundsMax);

                string positionPath = outputFolder + "/" + safeName + "_Position.asset";
                string normalPath = outputFolder + "/" + safeName + "_Normal.asset";
                string meshPath = outputFolder + "/" + safeName + "_Mesh.asset";
                string prefabPath = outputFolder + "/" + safeName + "_VAT.prefab";

                Texture2D positionTexture = CreateOrUpdateTexture(
                    safeName + "_Position",
                    vertexCount,
                    frameCount,
                    positionPixels,
                    positionPath);
                Texture2D normalTexture = CreateOrUpdateTexture(
                    safeName + "_Normal",
                    vertexCount,
                    frameCount,
                    normalPixels,
                    normalPath);
                Mesh mesh = CreateOrUpdateCombinedMesh(
                    safeName,
                    bakeInfos,
                    firstFrameVertices,
                    firstFrameNormals,
                    boundsMin,
                    boundsMax,
                    meshPath);
                Material[] materials = CreateOrUpdateMaterials(
                    safeName,
                    outputFolder,
                    shader,
                    bakeInfos,
                    positionTexture,
                    normalTexture,
                    frameCount,
                    request.materialOverrides,
                    request.materialBrightness,
                    request.animationClip.length);
                Material[] nearMaterials = CreateOrUpdateNearMaterials(
                    safeName,
                    outputFolder,
                    nearShader,
                    materials);
                GameObject vatPrefab = CreateOrUpdateVATPrefab(
                    prefabPath,
                    safeName,
                    request.sourceModel,
                    request.animationClip,
                    nearMaterials,
                    mesh,
                    materials,
                    materials,
                    request.nearToVatHeight,
                    request.vatToFarHeight,
                    request.farCullHeight);

                if (request.replaceTargetVisual && request.targetEnemyPrefab != null)
                {
                    IntegrateIntoTargetPrefab(
                        request.targetEnemyPrefab,
                        request.targetVisualPath,
                        vatPrefab);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log(
                    $"Generic VAT bake hoàn tất: {safeName}, {vertexCount} vertices, " +
                    $"{frameCount} frames, {materials.Length} material slots, output={outputFolder}");

                return new VATBakeResult
                {
                    vatPrefab = vatPrefab,
                    mesh = mesh,
                    positionTexture = positionTexture,
                    normalTexture = normalTexture,
                    vertexCount = vertexCount,
                    frameCount = frameCount
                };
            }
            finally
            {
                for (int i = 0; i < bakeInfos.Count; i++)
                {
                    UnityEngine.Object.DestroyImmediate(bakeInfos[i].sampledMesh);
                }
            }
        }
        finally
        {
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

        char[] invalidCharacters = System.IO.Path.GetInvalidFileNameChars();
        string safe = value.Trim();
        for (int i = 0; i < invalidCharacters.Length; i++)
        {
            safe = safe.Replace(invalidCharacters[i], '_');
        }
        return safe.Replace('/', '_').Replace('\\', '_');
    }

    private static void ValidateRequest(VATBakeRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.sourceModel == null || request.animationClip == null)
        {
            throw new InvalidOperationException("Source Model và Animation Clip là bắt buộc.");
        }

        if (!EditorUtility.IsPersistent(request.sourceModel))
        {
            throw new InvalidOperationException("Source Model phải là asset trong Project.");
        }

        if (request.sampleRate < 1 || request.sampleRate > 120)
        {
            throw new InvalidOperationException("Sample Rate phải nằm trong khoảng 1-120.");
        }

        if (string.IsNullOrWhiteSpace(request.outputRoot) ||
            !request.outputRoot.StartsWith("Assets", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Output Root phải nằm trong Assets.");
        }

        if (request.sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
        {
            throw new InvalidOperationException("Source Model không có SkinnedMeshRenderer.");
        }
    }

    private static List<RendererBakeInfo> BuildBakeInfos(SkinnedMeshRenderer[] renderers)
    {
        List<RendererBakeInfo> infos = new List<RendererBakeInfo>();
        int vertexOffset = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].sharedMesh == null || renderers[i].sharedMesh.vertexCount == 0)
            {
                continue;
            }

            RendererBakeInfo info = new RendererBakeInfo
            {
                renderer = renderers[i],
                sampledMesh = new Mesh { name = renderers[i].name + "_VATSample" },
                vertexOffset = vertexOffset
            };
            infos.Add(info);
            vertexOffset += renderers[i].sharedMesh.vertexCount;
        }

        if (infos.Count == 0)
        {
            throw new InvalidOperationException("Không có SkinnedMeshRenderer nào chứa mesh hợp lệ.");
        }

        return infos;
    }

    private static int GetVertexCount(List<RendererBakeInfo> infos)
    {
        RendererBakeInfo last = infos[infos.Count - 1];
        return last.vertexOffset + last.renderer.sharedMesh.vertexCount;
    }

private static void SampleFrames(
        VATBakeRequest request,
        GameObject instance,
        List<RendererBakeInfo> infos,
        int vertexCount,
        int frameCount,
        Color[] positionPixels,
        Color[] normalPixels,
        Vector3[] firstFrameVertices,
        Vector3[] firstFrameNormals,
        ref Vector3 boundsMin,
        ref Vector3 boundsMax)
    {
        Transform[] poseTransforms = instance.GetComponentsInChildren<Transform>(true);
        Vector3[] loopStartPositions = new Vector3[poseTransforms.Length];
        Quaternion[] loopStartRotations = new Quaternion[poseTransforms.Length];
        Vector3[] loopStartScales = new Vector3[poseTransforms.Length];

        for (int frame = 0; frame < frameCount; frame++)
        {
            float normalizedTime = frame / (float)frameCount;
            float sampleTime = request.animationClip.length * normalizedTime;
            request.animationClip.SampleAnimation(instance, sampleTime);

            if (frame == 0)
            {
                for (int transformIndex = 0; transformIndex < poseTransforms.Length; transformIndex++)
                {
                    Transform poseTransform = poseTransforms[transformIndex];
                    loopStartPositions[transformIndex] = poseTransform.localPosition;
                    loopStartRotations[transformIndex] = poseTransform.localRotation;
                    loopStartScales[transformIndex] = poseTransform.localScale;
                }
            }
            else if (request.loopBlendFraction > 0f)
            {
                float blendStart = 1f - Mathf.Clamp(request.loopBlendFraction, 0f, 0.5f);
                float loopBlend = Mathf.InverseLerp(blendStart, 1f, normalizedTime);
                loopBlend = Mathf.SmoothStep(0f, 1f, loopBlend);

                if (loopBlend > 0f)
                {
                    for (int transformIndex = 0; transformIndex < poseTransforms.Length; transformIndex++)
                    {
                        Transform poseTransform = poseTransforms[transformIndex];
                        poseTransform.localPosition = Vector3.Lerp(
                            poseTransform.localPosition,
                            loopStartPositions[transformIndex],
                            loopBlend);
                        poseTransform.localRotation = Quaternion.Slerp(
                            poseTransform.localRotation,
                            loopStartRotations[transformIndex],
                            loopBlend);
                        poseTransform.localScale = Vector3.Lerp(
                            poseTransform.localScale,
                            loopStartScales[transformIndex],
                            loopBlend);
                    }
                }
            }

            for (int rendererIndex = 0; rendererIndex < infos.Count; rendererIndex++)
            {
                RendererBakeInfo info = infos[rendererIndex];
                info.renderer.BakeMesh(info.sampledMesh, true);
                Vector3[] vertices = info.sampledMesh.vertices;
                Vector3[] normals = info.sampledMesh.normals;
                int expectedCount = info.renderer.sharedMesh.vertexCount;

                if (vertices.Length != expectedCount || normals.Length != expectedCount)
                {
                    throw new InvalidOperationException(
                        $"{info.renderer.name} trả về vertex/normal count không hợp lệ tại frame {frame}.");
                }

                Matrix4x4 rendererToRoot =
                    instance.transform.worldToLocalMatrix * info.renderer.transform.localToWorldMatrix;
                Matrix4x4 normalToRoot = rendererToRoot.inverse.transpose;
                int rowStart = frame * vertexCount + info.vertexOffset;

                for (int vertex = 0; vertex < vertices.Length; vertex++)
                {
                    Vector3 position = rendererToRoot.MultiplyPoint3x4(vertices[vertex]);
                    Vector3 normal = normalToRoot.MultiplyVector(normals[vertex]).normalized;
                    positionPixels[rowStart + vertex] = new Color(position.x, position.y, position.z, 1f);
                    normalPixels[rowStart + vertex] = new Color(normal.x, normal.y, normal.z, 1f);
                    boundsMin = Vector3.Min(boundsMin, position);
                    boundsMax = Vector3.Max(boundsMax, position);

                    if (frame == 0)
                    {
                        firstFrameVertices[info.vertexOffset + vertex] = position;
                        firstFrameNormals[info.vertexOffset + vertex] = normal;
                    }
                }
            }
        }
    }

    private static Texture2D CreateOrUpdateTexture(
        string textureName,
        int width,
        int height,
        Color[] pixels,
        string assetPath)
    {
        Texture2D created = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true)
        {
            name = textureName,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };
        created.SetPixels(pixels);
        created.Apply(false, false);

        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
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

    private static Mesh CreateOrUpdateCombinedMesh(
        string safeName,
        List<RendererBakeInfo> infos,
        Vector3[] vertices,
        Vector3[] normals,
        Vector3 boundsMin,
        Vector3 boundsMax,
        string assetPath)
    {
        Mesh created = new Mesh
        {
            name = safeName + "_VAT",
            indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
        };
        created.vertices = vertices;
        created.normals = normals;

        Vector2[] sourceUV = new Vector2[vertices.Length];
        List<int[]> subMeshTriangles = new List<int[]>();
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
        created.uv = sourceUV;

        List<Vector2> vatUV = new List<Vector2>(vertices.Length);
        for (int vertex = 0; vertex < vertices.Length; vertex++)
        {
            vatUV.Add(new Vector2((vertex + 0.5f) / vertices.Length, 0f));
        }
        created.SetUVs(1, vatUV);

        created.subMeshCount = subMeshTriangles.Count;
        for (int subMesh = 0; subMesh < subMeshTriangles.Count; subMesh++)
        {
            created.SetTriangles(subMeshTriangles[subMesh], subMesh, false);
        }

        Bounds animationBounds = new Bounds();
        animationBounds.SetMinMax(boundsMin, boundsMax);
        animationBounds.Expand(0.1f);
        created.bounds = animationBounds;

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
        Texture2D positionTexture,
        Texture2D normalTexture,
        int frameCount,
        Material[] materialOverrides,
        float materialBrightness,
        float duration)
    {
        List<Material> output = new List<Material>();
        Dictionary<int, Material> materialCache = new Dictionary<int, Material>();
        int uniqueMaterialIndex = 0;

        for (int infoIndex = 0; infoIndex < infos.Count; infoIndex++)
        {
            Mesh mesh = infos[infoIndex].renderer.sharedMesh;
            Material[] sourceMaterials = infos[infoIndex].renderer.sharedMaterials;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int materialSlot = output.Count;
                Material source = materialOverrides != null &&
                    materialSlot < materialOverrides.Length &&
                    materialOverrides[materialSlot] != null
                        ? materialOverrides[materialSlot]
                        : sourceMaterials.Length == 0
                            ? null
                            : sourceMaterials[Mathf.Min(subMesh, sourceMaterials.Length - 1)];
                int cacheKey = source != null ? source.GetInstanceID() : 0;
                if (!materialCache.TryGetValue(cacheKey, out Material vatMaterial))
                {
                    string materialPath =
                        outputFolder + "/" + safeName + "_Material_" + uniqueMaterialIndex + ".mat";
                    vatMaterial = CreateOrUpdateMaterial(
                        materialPath,
                        safeName + "_VAT_" + uniqueMaterialIndex,
                        shader,
                        source,
                        positionTexture,
                        normalTexture,
                        frameCount,
                        materialBrightness,
                        duration);
                    materialCache.Add(cacheKey, vatMaterial);
                    uniqueMaterialIndex++;
                }

                output.Add(vatMaterial);
            }
        }

        return output.ToArray();
    }

private static Material CreateOrUpdateMaterial(
        string assetPath,
        string materialName,
        Shader shader,
        Material source,
        Texture2D positionTexture,
        Texture2D normalTexture,
        int frameCount,
        float materialBrightness,
        float duration)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            material = new Material(shader) { name = materialName };
            AssetDatabase.CreateAsset(material, assetPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetTexture("_BaseMap", Texture2D.whiteTexture);
        material.SetColor("_BaseColor", new Color(materialBrightness, materialBrightness, materialBrightness, 1f));
        material.SetTexture("_MetallicGlossMap", Texture2D.whiteTexture);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.5f);

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
                Color sourceColor = source.GetColor("_BaseColor");
                sourceColor.r *= materialBrightness;
                sourceColor.g *= materialBrightness;
                sourceColor.b *= materialBrightness;
                material.SetColor("_BaseColor", sourceColor);
            }

            if (source.HasProperty("_MetallicGlossMap"))
            {
                Texture metallicMap = source.GetTexture("_MetallicGlossMap");
                material.SetTexture(
                    "_MetallicGlossMap",
                    metallicMap != null ? metallicMap : Texture2D.whiteTexture);
            }

            if (source.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", source.GetFloat("_Metallic"));
            }

            if (source.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));
            }
        }

        material.SetColor("_OutlineColor", new Color(0.025f, 0.02f, 0.015f, 1f));
        material.SetFloat("_OutlineWidth", 1.5f);
        material.SetTexture("_VATPositionTex", positionTexture);
        material.SetTexture("_VATNormalTex", normalTexture);
        material.SetFloat("_VATFrameCount", frameCount);
        material.SetFloat("_VATDuration", duration);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

private static GameObject CreateOrUpdateVATPrefab(
        string prefabPath,
        string safeName,
        GameObject sourceModel,
        AnimationClip animationClip,
        Material[] nearMaterials,
        Mesh mesh,
        Material[] materials,
        Material[] farMaterials,
        float nearToVatHeight,
        float vatToFarHeight,
        float farCullHeight)
    {
        GameObject temporaryRoot = new GameObject(safeName + "_HybridLOD");
        try
        {
            GameObject nearVisual = (GameObject)PrefabUtility.InstantiatePrefab(sourceModel);
            if (nearVisual == null)
            {
                nearVisual = UnityEngine.Object.Instantiate(sourceModel);
            }
            nearVisual.name = "LOD0_Near_Skinned";
            nearVisual.transform.SetParent(temporaryRoot.transform, false);
            nearVisual.transform.localPosition = Vector3.zero;
            nearVisual.transform.localRotation = Quaternion.identity;
            nearVisual.transform.localScale = Vector3.one;
            ApplyMaterialOverrides(nearVisual, nearMaterials);

            SkinnedMeshRenderer[] nearSkinnedRenderers =
                nearVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < nearSkinnedRenderers.Length; i++)
            {
                nearSkinnedRenderers[i].updateWhenOffscreen = false;
            }

            EnemyHybridLODAnimator nearAnimator = nearVisual.AddComponent<EnemyHybridLODAnimator>();
            nearAnimator.Configure(animationClip);

            GameObject midVisual = new GameObject("LOD1_Mid_VAT_Unlit");
            midVisual.transform.SetParent(temporaryRoot.transform, false);
            MeshFilter midMeshFilter = midVisual.AddComponent<MeshFilter>();
            midMeshFilter.sharedMesh = mesh;
            MeshRenderer midRenderer = midVisual.AddComponent<MeshRenderer>();
            midRenderer.sharedMaterials = materials;
            midRenderer.shadowCastingMode = ShadowCastingMode.Off;
            midRenderer.receiveShadows = false;
            EnemyVATAnimator midAnimator = midVisual.AddComponent<EnemyVATAnimator>();
            midAnimator.Configure(false, 0f, 1f);

            GameObject farVisual = new GameObject("LOD2_Far_VAT_Unlit");
            farVisual.transform.SetParent(temporaryRoot.transform, false);
            MeshFilter farMeshFilter = farVisual.AddComponent<MeshFilter>();
            farMeshFilter.sharedMesh = mesh;
            MeshRenderer farRenderer = farVisual.AddComponent<MeshRenderer>();
            farRenderer.sharedMaterials = farMaterials;
            farRenderer.shadowCastingMode = ShadowCastingMode.Off;
            farRenderer.receiveShadows = false;
            EnemyVATAnimator farAnimator = farVisual.AddComponent<EnemyVATAnimator>();
            farAnimator.Configure(false, 0f, 1f);

            Renderer[] nearRenderers = nearVisual.GetComponentsInChildren<Renderer>(true);
            float lod0Height = Mathf.Clamp(nearToVatHeight, 0.02f, 0.95f);
            float lod1Height = Mathf.Clamp(vatToFarHeight, 0.01f, lod0Height - 0.01f);
            float lod2Height = Mathf.Clamp(farCullHeight, 0.001f, lod1Height - 0.005f);

            LODGroup lodGroup = temporaryRoot.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.None;
            lodGroup.animateCrossFading = false;
            lodGroup.SetLODs(new[]
            {
                new LOD(lod0Height, nearRenderers),
                new LOD(lod1Height, new Renderer[] { midRenderer }),
                new LOD(lod2Height, new Renderer[] { farRenderer })
            });
            lodGroup.RecalculateBounds();

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
        GameObject vatPrefab)
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

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(vatPrefab);
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


private static void ApplyMaterialOverrides(GameObject nearVisual, Material[] materialOverrides)
    {
        if (materialOverrides == null || materialOverrides.Length == 0)
        {
            return;
        }

        int slot = 0;
        SkinnedMeshRenderer[] renderers = nearVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Material[] assigned = renderers[rendererIndex].sharedMaterials;
            for (int materialIndex = 0; materialIndex < assigned.Length; materialIndex++)
            {
                if (slot < materialOverrides.Length && materialOverrides[slot] != null)
                {
                    assigned[materialIndex] = materialOverrides[slot];
                }
                slot++;
            }
            renderers[rendererIndex].sharedMaterials = assigned;
        }
    }


private static Material[] CreateOrUpdateNearMaterials(
        string safeName,
        string outputFolder,
        Shader nearShader,
        Material[] sourceMaterials)
    {
        Material[] nearMaterials = new Material[sourceMaterials.Length];
        for (int i = 0; i < sourceMaterials.Length; i++)
        {
            Material source = sourceMaterials[i];
            string assetPath = outputFolder + "/" + safeName + "_NearMaterial_" + i + ".mat";
            Material nearMaterial = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (nearMaterial == null)
            {
                nearMaterial = new Material(nearShader) { name = safeName + "_Near_" + i };
                AssetDatabase.CreateAsset(nearMaterial, assetPath);
            }
            else
            {
                nearMaterial.shader = nearShader;
            }

            nearMaterial.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
            nearMaterial.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
            nearMaterial.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
            nearMaterial.SetColor("_BaseColor", source.GetColor("_BaseColor"));
            nearMaterial.SetTexture("_MetallicGlossMap", source.GetTexture("_MetallicGlossMap"));
            nearMaterial.SetFloat("_Metallic", source.GetFloat("_Metallic"));
            nearMaterial.SetFloat("_Smoothness", source.GetFloat("_Smoothness"));
            nearMaterial.enableInstancing = true;
            EditorUtility.SetDirty(nearMaterial);
            nearMaterials[i] = nearMaterial;
        }

        return nearMaterials;
    }
}
