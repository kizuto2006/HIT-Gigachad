using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One warning circle and its golden-sand eruption. Damage is sampled once at
/// the exact transition from telegraph to explosion.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoneGolemSandBurstZone : MonoBehaviour
{
    private static readonly Collider[] HitBuffer = new Collider[16];

    private readonly List<Material> runtimeMaterials = new List<Material>(4);
    private float radius;
    private float telegraphDuration;
    private float damage;
    private float knockbackForce;
    private Transform source;
    private GameObject warningVisual;
    private Material warningFillMaterial;

    public void Initialize(
        float attackRadius,
        float warningDuration,
        float attackDamage,
        float attackKnockback,
        Transform attackSource)
    {
        radius = Mathf.Max(0.1f, attackRadius);
        telegraphDuration = Mathf.Max(0.1f, warningDuration);
        damage = Mathf.Max(0f, attackDamage);
        knockbackForce = Mathf.Max(0f, attackKnockback);
        source = attackSource;

        CreateWarningVisual();
        StartCoroutine(RunZone());
    }

    private IEnumerator RunZone()
    {
        float elapsed = 0f;
        while (elapsed < telegraphDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / telegraphDuration);
            float pulse = 1f + Mathf.Sin(progress * Mathf.PI * 8f) * 0.035f;
            float closingScale = Mathf.Lerp(1.08f, 1f, progress);
            warningVisual.transform.localScale = Vector3.one * pulse * closingScale;

            if (warningFillMaterial != null)
            {
                Color color = warningFillMaterial.color;
                color.a = Mathf.Lerp(0.18f, 0.42f, progress);
                warningFillMaterial.color = color;
            }

            yield return null;
        }

        warningVisual.SetActive(false);
        ApplyDamage();
        PlayGoldenSandExplosion();
        yield return new WaitForSeconds(1.8f);
        Destroy(gameObject);
    }

    private void CreateWarningVisual()
    {
        warningVisual = new GameObject("Red Danger Telegraph");
        warningVisual.transform.SetParent(transform, false);

        MeshFilter filter = warningVisual.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateDiscMesh(64);

        MeshRenderer meshRenderer = warningVisual.AddComponent<MeshRenderer>();
        warningFillMaterial = CreateTransparentMaterial(new Color(0.95f, 0.035f, 0.02f, 0.2f));
        meshRenderer.sharedMaterial = warningFillMaterial;
        meshRenderer.sortingOrder = 20;

        GameObject borderObject = new GameObject("Danger Border");
        borderObject.transform.SetParent(warningVisual.transform, false);
        LineRenderer border = borderObject.AddComponent<LineRenderer>();
        border.useWorldSpace = false;
        border.loop = true;
        border.positionCount = 64;
        border.widthMultiplier = 0.11f;
        border.numCornerVertices = 2;
        border.numCapVertices = 2;
        border.sharedMaterial = CreateTransparentMaterial(new Color(1f, 0.08f, 0.015f, 0.9f));
        border.sortingOrder = 21;

        for (int i = 0; i < border.positionCount; i++)
        {
            float angle = i / (float)border.positionCount * Mathf.PI * 2f;
            border.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.018f, Mathf.Sin(angle) * radius));
        }
    }

    private void ApplyDamage()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            radius,
            HitBuffer,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        PlayerHealth damagedPlayer = null;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = HitBuffer[i];
            if (hit == null)
            {
                continue;
            }

            PlayerHealth playerHealth = hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null || playerHealth == damagedPlayer)
            {
                continue;
            }

            damagedPlayer = playerHealth;
            playerHealth.TakeDamage(damage);

            PlayerSimpleMovement movement = playerHealth.GetComponent<PlayerSimpleMovement>();
            if (movement != null)
            {
                Vector3 knockbackDirection = playerHealth.transform.position - transform.position;
                knockbackDirection.y = 0f;
                if (knockbackDirection.sqrMagnitude < 0.001f && source != null)
                {
                    knockbackDirection = playerHealth.transform.position - source.position;
                    knockbackDirection.y = 0f;
                }

                movement.ApplyKnockback(knockbackDirection.normalized * knockbackForce);
            }

            break;
        }
    }

    private void PlayGoldenSandExplosion()
    {
        CreateSandColumn();
        CreateSandShockwave();
        CreateDustCloud();
        CreateExplosionLight();
    }

    private void CreateSandColumn()
    {
        ParticleSystem particles = CreateParticleSystem("Golden Sand Column");
        particles.transform.localPosition = Vector3.up * 0.05f;
        particles.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.7f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 1.05f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5.5f, 10f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.34f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.78f, 0.18f, 1f),
            new Color(0.65f, 0.34f, 0.055f, 0.95f));
        main.gravityModifier = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)110) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 34f;
        shape.radius = radius * 0.42f;
        shape.radiusThickness = 1f;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.7f;
        noise.frequency = 0.65f;

        particles.Play();
    }

    private void CreateSandShockwave()
    {
        ParticleSystem particles = CreateParticleSystem("Golden Sand Shockwave");
        particles.transform.localPosition = Vector3.up * 0.08f;
        particles.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.45f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 4.5f, radius * 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.84f, 0.28f, 0.95f),
            new Color(0.82f, 0.48f, 0.09f, 0.8f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)72) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.2f;
        shape.radiusThickness = 1f;

        ParticleSystem.LimitVelocityOverLifetimeModule limitVelocity = particles.limitVelocityOverLifetime;
        limitVelocity.enabled = true;
        limitVelocity.dampen = 0.2f;

        particles.Play();
    }

    private void CreateDustCloud()
    {
        ParticleSystem particles = CreateParticleSystem("Warm Sand Dust");
        particles.transform.localPosition = Vector3.up * 0.2f;

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.8f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.95f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.68f, 0.14f, 0.48f),
            new Color(0.58f, 0.30f, 0.06f, 0.18f));
        main.gravityModifier = -0.04f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)30) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = radius * 0.7f;
        shape.radiusThickness = 0.6f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.75f, 0.2f), 0f),
                new GradientColorKey(new Color(0.52f, 0.27f, 0.06f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.55f, 0.12f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = fadeGradient;

        particles.Play();
    }

    private void CreateExplosionLight()
    {
        GameObject lightObject = new GameObject("Sand Flash");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = Vector3.up * 0.5f;
        Light flash = lightObject.AddComponent<Light>();
        flash.type = LightType.Point;
        flash.color = new Color(1f, 0.63f, 0.12f);
        flash.range = radius * 3.5f;
        flash.intensity = 4.5f;
        StartCoroutine(FadeLight(flash, 0.28f));
    }

    private static IEnumerator FadeLight(Light lightSource, float duration)
    {
        float startIntensity = lightSource.intensity;
        float elapsed = 0f;
        while (lightSource != null && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            lightSource.intensity = Mathf.Lerp(startIntensity, 0f, elapsed / duration);
            yield return null;
        }

        if (lightSource != null)
        {
            Destroy(lightSource.gameObject);
        }
    }

    private ParticleSystem CreateParticleSystem(string objectName)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.transform.SetParent(transform, false);
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        // AddComponent starts the default system immediately because its object
        // is active. Clear that auto-play state before callers configure duration.
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.sharedMaterial = CreateParticleMaterial();
        return particles;
    }

    private Material CreateParticleMaterial()
    {
        Shader shader = Resources.Load<Shader>("Shaders/GoldenSandParticle");
        if (shader == null)
        {
            shader = Shader.Find("Custom/Gigachad/Golden Sand Particle");
        }
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        Material material = new Material(shader)
        {
            name = "Runtime Golden Sand Particle Material"
        };

        material.SetFloat("_Softness", 0.28f);

        runtimeMaterials.Add(material);
        return material;
    }

    private Material CreateTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        Material material = new Material(shader)
        {
            color = color,
            renderQueue = 3000
        };
        runtimeMaterials.Add(material);
        return material;
    }

    private Mesh CreateDiscMesh(int segments)
    {
        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];
        vertices[0] = Vector3.up * 0.01f;

        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0.01f, Mathf.Sin(angle) * radius);

            int triangle = i * 3;
            triangles[triangle] = 0;
            triangles[triangle + 1] = (i + 1) % segments + 1;
            triangles[triangle + 2] = i + 1;
        }

        Mesh mesh = new Mesh { name = "Runtime Sand Burst Telegraph" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private void OnDestroy()
    {
        if (warningVisual != null)
        {
            MeshFilter filter = warningVisual.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Destroy(filter.sharedMesh);
            }
        }

        for (int i = 0; i < runtimeMaterials.Count; i++)
        {
            if (runtimeMaterials[i] != null)
            {
                Destroy(runtimeMaterials[i]);
            }
        }
    }
}
