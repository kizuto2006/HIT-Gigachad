using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class EnemyVATBaker
{
    private const string SourceFbxPath = "Assets/Model/Enemy/Mummy/EnemyAnimation/Walking.fbx";
    private const string EnemyPrefabPath = "Assets/Prefab/Enemy_Mummy.prefab";
    private const string ShaderName = "Gigachad/VAT/Enemy Lit";
    private const string OutputFolder = "Assets/Generated/VAT/MummyWalking";

    private const string MeshPath = OutputFolder + "/MummyWalking_Mesh.asset";
    private const string PositionTexturePath = OutputFolder + "/MummyWalking_Position.asset";
    private const string NormalTexturePath = OutputFolder + "/MummyWalking_Normal.asset";
    private const string MaterialPath = OutputFolder + "/MummyWalking_Material.mat";

    [MenuItem("Tools/Gigachad/VAT/Bake Mummy Walking")]
    public static void BakeMummyWalking()
    {
        GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbxPath);
        if (sourceAsset == null)
        {
            throw new InvalidOperationException($"Không tìm thấy source FBX: {SourceFbxPath}");
        }

        AnimationClip clip = FindFirstRuntimeClip(SourceFbxPath);
        if (clip == null)
        {
            throw new InvalidOperationException($"Không tìm thấy AnimationClip trong {SourceFbxPath}");
        }

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            throw new InvalidOperationException($"Không tìm thấy shader {ShaderName}. Hãy đợi Unity compile shader rồi bake lại.");
        }

        EnsureFolder(OutputFolder);

        GameObject instance = UnityEngine.Object.Instantiate(sourceAsset);
        instance.name = sourceAsset.name + "_VAT_BakeSource";
        instance.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.enabled = false;
            }

            SkinnedMeshRenderer skinnedRenderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null)
            {
                throw new InvalidOperationException("Source FBX không có SkinnedMeshRenderer hợp lệ.");
            }

            if (skinnedRenderer.sharedMesh.subMeshCount != 1)
            {
                throw new InvalidOperationException("VAT baker hiện yêu cầu source mesh có đúng một submesh.");
            }

            int vertexCount = skinnedRenderer.sharedMesh.vertexCount;
            int frameCount = Mathf.Max(2, Mathf.RoundToInt(clip.length * clip.frameRate));
            if (vertexCount > SystemInfo.maxTextureSize || frameCount > SystemInfo.maxTextureSize)
            {
                throw new InvalidOperationException(
                    $"VAT texture {vertexCount}x{frameCount} vượt maxTextureSize {SystemInfo.maxTextureSize}.");
            }

            Color[] positionPixels = new Color[vertexCount * frameCount];
            Color[] normalPixels = new Color[vertexCount * frameCount];
            Vector3[] firstFrameVertices = null;
            Vector3[] firstFrameNormals = null;
            Vector3 boundsMin = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 boundsMax = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            Mesh sampledMesh = new Mesh { name = "VAT_SampledFrame" };
            try
            {
                for (int frame = 0; frame < frameCount; frame++)
                {
                    float sampleTime = clip.length * frame / frameCount;
                    clip.SampleAnimation(instance, sampleTime);
                    skinnedRenderer.BakeMesh(sampledMesh, true);

                    Vector3[] vertices = sampledMesh.vertices;
                    Vector3[] normals = sampledMesh.normals;
                    if (vertices.Length != vertexCount || normals.Length != vertexCount)
                    {
                        throw new InvalidOperationException(
                            $"Frame {frame} có vertex/normal count không khớp source mesh.");
                    }

                    Matrix4x4 rendererToRoot =
                        instance.transform.worldToLocalMatrix * skinnedRenderer.transform.localToWorldMatrix;
                    Matrix4x4 normalToRoot = rendererToRoot.inverse.transpose;
                    int rowStart = frame * vertexCount;

                    for (int vertex = 0; vertex < vertexCount; vertex++)
                    {
                        Vector3 position = rendererToRoot.MultiplyPoint3x4(vertices[vertex]);
                        Vector3 normal = normalToRoot.MultiplyVector(normals[vertex]).normalized;
                        positionPixels[rowStart + vertex] = new Color(position.x, position.y, position.z, 1f);
                        normalPixels[rowStart + vertex] = new Color(normal.x, normal.y, normal.z, 1f);
                        boundsMin = Vector3.Min(boundsMin, position);
                        boundsMax = Vector3.Max(boundsMax, position);

                        if (frame == 0)
                        {
                            firstFrameVertices ??= new Vector3[vertexCount];
                            firstFrameNormals ??= new Vector3[vertexCount];
                            firstFrameVertices[vertex] = position;
                            firstFrameNormals[vertex] = normal;
                        }
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sampledMesh);
            }

            Texture2D positionTexture = CreateTexture(
                "MummyWalking_Position",
                vertexCount,
                frameCount,
                positionPixels,
                PositionTexturePath);
            Texture2D normalTexture = CreateTexture(
                "MummyWalking_Normal",
                vertexCount,
                frameCount,
                normalPixels,
                NormalTexturePath);
            Mesh vatMesh = CreateMesh(
                skinnedRenderer.sharedMesh,
                firstFrameVertices,
                firstFrameNormals,
                boundsMin,
                boundsMax,
                MeshPath);
            Material vatMaterial = CreateMaterial(
                shader,
                skinnedRenderer.sharedMaterial,
                positionTexture,
                normalTexture,
                frameCount,
                clip.length);

            IntegrateIntoEnemyPrefab(vatMesh, vatMaterial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"VAT bake hoàn tất: {vertexCount} vertices, {frameCount} frames, " +
                $"{positionTexture.width}x{positionTexture.height}, prefab={EnemyPrefabPath}");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static AnimationClip FindFirstRuntimeClip(string assetPath)
    {
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

    private static Texture2D CreateTexture(
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

    private static Mesh CreateMesh(
        Mesh source,
        Vector3[] firstFrameVertices,
        Vector3[] firstFrameNormals,
        Vector3 boundsMin,
        Vector3 boundsMax,
        string assetPath)
    {
        Mesh created = UnityEngine.Object.Instantiate(source);
        created.name = "MummyWalking_VAT";
        created.vertices = firstFrameVertices;
        created.normals = firstFrameNormals;

        List<Vector2> vatUV = new List<Vector2>(created.vertexCount);
        for (int vertex = 0; vertex < created.vertexCount; vertex++)
        {
            vatUV.Add(new Vector2((vertex + 0.5f) / created.vertexCount, 0f));
        }
        created.SetUVs(1, vatUV);

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

    private static Material CreateMaterial(
        Shader shader,
        Material source,
        Texture2D positionTexture,
        Texture2D normalTexture,
        int frameCount,
        float duration)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "MummyWalking_VAT" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

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

        material.SetTexture("_VATPositionTex", positionTexture);
        material.SetTexture("_VATNormalTex", normalTexture);
        material.SetFloat("_VATFrameCount", frameCount);
        material.SetFloat("_VATDuration", duration);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void IntegrateIntoEnemyPrefab(Mesh mesh, Material material)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        try
        {
            Transform visual = prefabRoot.transform.Find("Visual");
            if (visual == null)
            {
                throw new InvalidOperationException("Enemy prefab không có child Visual.");
            }

            for (int i = visual.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(visual.GetChild(i).gameObject);
            }

            GameObject vatVisual = new GameObject("Mummy_VAT");
            vatVisual.transform.SetParent(visual, false);

            MeshFilter meshFilter = vatVisual.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = vatVisual.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;

            vatVisual.AddComponent<EnemyVATAnimator>();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, EnemyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
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
