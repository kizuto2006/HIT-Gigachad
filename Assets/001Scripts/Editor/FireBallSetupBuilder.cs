using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class FireBallSetupBuilder
{
    private const string VfxFolder = "Assets/Prefab/WeaponVFX/FireBall";
    private const string ProjectilePath = VfxFolder + "/FireBall_Projectile.prefab";
    private const string ExplosionPath = VfxFolder + "/FireBall_HitExplosion.prefab";
    private const string WeaponPath = "Assets/Resources/Weapons/FireBall.asset";

    private const string CoreMaterialPath = VfxFolder + "/FireBall_Core.mat";
    private const string RingMaterialPath = VfxFolder + "/FireBall_Ring.mat";
    private const string EmberMaterialPath = VfxFolder + "/FireBall_Ember.mat";
    private const string SparkMaterialPath = VfxFolder + "/FireBall_Spark.mat";

    [MenuItem("Tools/Gigachad/Setup FireBall")]
    public static void Setup()
    {
        EnsureFolder("Assets/Prefab/WeaponVFX");
        EnsureFolder(VfxFolder);

        Shader shader = Shader.Find("Gigachad/FireballBillboard");
        if (shader == null)
        {
            Debug.LogError("[FireBallSetup] Shader Gigachad/FireballBillboard was not found.");
            return;
        }

        Material coreMaterial = CreateMaterial(
            CoreMaterialPath,
            shader,
            new Color(1f, 0.92f, 0.28f, 1f),
            4.2f,
            0f,
            0.09f);
        Material ringMaterial = CreateMaterial(
            RingMaterialPath,
            shader,
            new Color(1f, 0.62f, 0.025f, 1f),
            3.2f,
            1f,
            0.075f);
        Material emberMaterial = CreateMaterial(
            EmberMaterialPath,
            shader,
            new Color(1f, 0.19f, 0.015f, 1f),
            2.25f,
            0f,
            0.12f);
        Material sparkMaterial = CreateMaterial(
            SparkMaterialPath,
            shader,
            new Color(1f, 0.92f, 0.4f, 1f),
            4.8f,
            2f,
            0.05f);

        GameObject explosionPrefab = CreateHitExplosion(
            coreMaterial,
            ringMaterial,
            emberMaterial,
            sparkMaterial);
        GameObject projectilePrefab = CreateProjectile(
            coreMaterial,
            ringMaterial,
            emberMaterial,
            sparkMaterial,
            explosionPrefab);

        WeaponData weapon = CreateWeaponData(projectilePrefab);
        EditorUtility.SetDirty(weapon);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = projectilePrefab;
        EditorGUIUtility.PingObject(projectilePrefab);
        Debug.Log("[FireBallSetup] Rebuilt the stylized fireball projectile and impact VFX.");
    }

    private static Material CreateMaterial(
        string path,
        Shader shader,
        Color tint,
        float intensity,
        float shape,
        float softness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetColor("_Tint", tint);
        material.SetFloat("_Intensity", intensity);
        material.SetFloat("_Shape", shape);
        material.SetFloat("_Softness", softness);
        material.renderQueue = 3050;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateProjectile(
        Material coreMaterial,
        Material ringMaterial,
        Material emberMaterial,
        Material sparkMaterial,
        GameObject hitEffectPrefab)
    {
        GameObject root = new GameObject("FireBall_Projectile");

        SphereCollider collider = root.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.34f;

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        Projectile projectile = root.AddComponent<Projectile>();
        projectile.lifetime = 5f;
        projectile.homingTurnSpeed = 720f;
        projectile.hitEffectPrefab = hitEffectPrefab;
        projectile.explosionRadius = 2.2f;
        projectile.explosionDamageMultiplier = 0.5f;
        projectile.explosionEffectLifetime = 0.8f;

        AddProjectileCore(root, coreMaterial);
        AddProjectileRings(root, ringMaterial);
        AddRoundTrail(root, emberMaterial);
        AddTrailRings(root, ringMaterial);
        AddTinySparks(root, sparkMaterial);

        Light light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.52f, 0.08f);
        light.range = 2.8f;
        light.intensity = 2.1f;
        light.shadows = LightShadows.None;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ProjectilePath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void AddProjectileCore(GameObject root, Material material)
    {
        ParticleSystem particles = CreateParticleSystem(root, "WhiteYellowCore");
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.075f, 0.12f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.48f, 0.66f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 0.75f, 1f),
            new Color(1f, 0.78f, 0.12f, 1f));
        main.maxParticles = 12;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 80f;

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = Curve(0.72f, 1f, 0.72f);

        ConfigureRenderer(particles, material, ParticleSystemRenderMode.Billboard, 3);
    }

    private static void AddProjectileRings(GameObject root, Material material)
    {
        ParticleSystem particles = CreateParticleSystem(root, "OrbitingRings");
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.11f, 0.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.48f, 0.78f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.94f, 0.12f, 1f),
            new Color(1f, 0.33f, 0.015f, 0.9f));
        main.maxParticles = 18;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 52f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.13f;

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = Curve(0.55f, 1.15f, 0.35f);

        ConfigureRenderer(particles, material, ParticleSystemRenderMode.Billboard, 2);
    }

    private static void AddRoundTrail(GameObject root, Material material)
    {
        ParticleSystem particles = CreateParticleSystem(root, "RoundOrangeTrail");
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.13f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.4f, 0.025f, 0.95f),
            new Color(0.95f, 0.045f, 0.008f, 0.78f));
        main.maxParticles = 32;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 45f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.16f;

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = Curve(1f, 0.82f, 0f);

        ConfigureRenderer(particles, material, ParticleSystemRenderMode.Billboard, 0);
    }

    private static void AddTrailRings(GameObject root, Material material)
    {
        ParticleSystem particles = CreateParticleSystem(root, "DetachedYellowRings");
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.93f, 0.12f, 0.95f),
            new Color(1f, 0.42f, 0.02f, 0.82f));
        main.maxParticles = 22;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 28f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = Curve(0.8f, 1.15f, 0f);

        ConfigureRenderer(particles, material, ParticleSystemRenderMode.Billboard, 1);
    }

    private static void AddTinySparks(GameObject root, Material material)
    {
        ParticleSystem particles = CreateParticleSystem(root, "TinyWhiteSparks");
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            Color.white,
            new Color(1f, 0.75f, 0.08f, 0.95f));
        main.maxParticles = 28;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 22f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.24f;

        ConfigureRenderer(particles, material, ParticleSystemRenderMode.Stretch, 4);
    }

    private static GameObject CreateHitExplosion(
        Material coreMaterial,
        Material ringMaterial,
        Material emberMaterial,
        Material sparkMaterial)
    {
        GameObject root = new GameObject("FireBall_HitExplosion");

        ParticleSystem flash = CreateParticleSystem(root, "WhiteFlash");
        ConfigureBurst(
            flash,
            5,
            0.11f,
            0.2f,
            0.1f,
            0.5f,
            0.55f,
            0.95f,
            new Color(1f, 1f, 0.72f, 1f),
            new Color(1f, 0.65f, 0.05f, 1f));
        SetSizeCurve(flash, Curve(0.28f, 1.25f, 0f));
        ConfigureRenderer(flash, coreMaterial, ParticleSystemRenderMode.Billboard, 4);

        ParticleSystem rings = CreateParticleSystem(root, "YellowOrangeRings");
        ConfigureBurst(
            rings,
            9,
            0.16f,
            0.3f,
            0.5f,
            2.7f,
            0.3f,
            0.72f,
            new Color(1f, 0.95f, 0.12f, 1f),
            new Color(1f, 0.2f, 0.01f, 0.9f));
        SetSizeCurve(rings, Curve(0.35f, 1.18f, 0f));
        ConfigureRenderer(rings, ringMaterial, ParticleSystemRenderMode.Billboard, 3);

        ParticleSystem embers = CreateParticleSystem(root, "RoundRedEmbers");
        ConfigureBurst(
            embers,
            13,
            0.18f,
            0.38f,
            1.5f,
            4.2f,
            0.09f,
            0.24f,
            new Color(1f, 0.36f, 0.015f, 1f),
            new Color(0.88f, 0.025f, 0.005f, 0.85f));
        SetSizeCurve(embers, Curve(1f, 0.75f, 0f));
        ConfigureRenderer(embers, emberMaterial, ParticleSystemRenderMode.Billboard, 1);

        ParticleSystem sparks = CreateParticleSystem(root, "RadialWhiteSparks");
        ConfigureBurst(
            sparks,
            26,
            0.12f,
            0.28f,
            3.5f,
            7.5f,
            0.018f,
            0.05f,
            Color.white,
            new Color(1f, 0.72f, 0.1f, 1f));
        SetSizeCurve(sparks, Curve(1f, 0.72f, 0f));
        ParticleSystemRenderer sparkRenderer = ConfigureRenderer(
            sparks,
            sparkMaterial,
            ParticleSystemRenderMode.Stretch,
            5);
        sparkRenderer.velocityScale = 0.15f;
        sparkRenderer.lengthScale = 2.4f;

        Light light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.63f, 0.12f);
        light.range = 3.4f;
        light.intensity = 3.8f;
        light.shadows = LightShadows.None;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ExplosionPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void ConfigureBurst(
        ParticleSystem particles,
        short count,
        float minLifetime,
        float maxLifetime,
        float minSpeed,
        float maxSpeed,
        float minSize,
        float maxSize,
        Color minColor,
        Color maxColor)
    {
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(minColor, maxColor);
        main.maxParticles = count + 4;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(
                0f,
                new ParticleSystem.MinMaxCurve(count))
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.06f;
    }

    private static ParticleSystem CreateParticleSystem(
        GameObject parent,
        string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        ParticleSystem particles = child.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = true;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;
        return particles;
    }

    private static ParticleSystemRenderer ConfigureRenderer(
        ParticleSystem particles,
        Material material,
        ParticleSystemRenderMode renderMode,
        int sortingOrder)
    {
        ParticleSystemRenderer renderer =
            particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = renderMode;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.allowOcclusionWhenDynamic = false;
        return renderer;
    }

    private static void SetSizeCurve(
        ParticleSystem particles,
        ParticleSystem.MinMaxCurve curve)
    {
        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = curve;
    }

    private static ParticleSystem.MinMaxCurve Curve(
        float start,
        float middle,
        float end)
    {
        return new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, start),
                new Keyframe(0.32f, middle),
                new Keyframe(1f, end)));
    }

    private static WeaponData CreateWeaponData(GameObject prefab)
    {
        WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(WeaponPath);
        if (weapon == null)
        {
            weapon = ScriptableObject.CreateInstance<WeaponData>();
            AssetDatabase.CreateAsset(weapon, WeaponPath);
        }

        weapon.id = "fireball";
        weapon.weaponName = "Fire Ball";
        weapon.description =
            "Tự động phóng cầu lửa rực sáng vào kẻ địch gần nhất.";
        weapon.icon = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Icons/Weapons/Firestaff.png");
        weapon.weaponType = WeaponType.Projectile;
        weapon.rarity = WeaponRarity.Common;
        weapon.atk = 18f;
        weapon.crit = 0.12f;
        weapon.projectileSpeed = 14f;
        weapon.additionalProjectileDamageMultiplier = 0.7f;
        weapon.projectileCount = 1;
        weapon.size = 1f;
        weapon.displaySizeAsPercent = true;
        weapon.cooldown = 1.1f;
        weapon.maxAttackSpeedMultiplier = 4f;
        weapon.pierce = 0;
        weapon.knockback = 0.75f;
        weapon.maxLevel = 40;
        weapon.useAutomaticLevelUpgrades = true;
        weapon.automaticUpgradeStats =
            AutomaticWeaponUpgradeStats.Damage |
            AutomaticWeaponUpgradeStats.ProjectileSpeed |
            AutomaticWeaponUpgradeStats.Cooldown;
        weapon.automaticDamageBonus = 3f;
        weapon.automaticSizeBonus = 0f;
        weapon.automaticProjectileSpeedBonus = 0.8f;
        weapon.automaticCooldownReduction = 0.03f;
        weapon.automaticKnockbackBonus = 0f;
        weapon.automaticSecondStatInterval = 4;
        weapon.automaticProjectileCountInterval = 12;
        weapon.automaticMaxProjectileCount = 5;
        weapon.grantSecondProjectileAtLevel2 = true;
        weapon.attackType = WeaponAttackType.Custom;
        weapon.projectilePrefab = prefab;
        weapon.attackEffectPrefab = null;
        return weapon;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        string folderName = path.Substring(separator + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }
}
