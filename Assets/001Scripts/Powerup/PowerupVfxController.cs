using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PowerupVfxController : MonoBehaviour
{
    private sealed class ActiveEffect
    {
        public PowerupType type;
        public GameObject root;
        public ParticleSystem particles;
        public LineRenderer ring;
        public float radius;
        public float spinSpeed;
    }

    private readonly Dictionary<PowerupType, ActiveEffect> activeEffects =
        new Dictionary<PowerupType, ActiveEffect>();

    private Transform effectRoot;
    private GameObject stopwatchOverlayObject;
    private static Material sharedParticleMaterial;

    private void Awake()
    {
        EnsureEffectRoot();
    }

    private void OnDisable()
    {
        ClearEffects();
    }

    private void Update()
    {
        foreach (KeyValuePair<PowerupType, ActiveEffect> pair in activeEffects)
        {
            ActiveEffect effect = pair.Value;
            if (effect == null || effect.root == null)
                continue;

            if (effect.spinSpeed != 0f)
            {
                effect.root.transform.Rotate(
                    Vector3.up,
                    effect.spinSpeed * Time.deltaTime,
                    Space.Self);
            }

            if (effect.ring != null)
                UpdateRing(effect.ring, effect.radius + Mathf.Sin(Time.time * 4f) * 0.04f);
        }
    }

    public void RefreshActivePowerups(IReadOnlyList<PowerupRuntimeState> states)
    {
        EnsureEffectRoot();

        HashSet<PowerupType> requiredTypes = new HashSet<PowerupType>();
        if (states != null)
        {
            for (int i = 0; i < states.Count; i++)
            {
                PowerupRuntimeState state = states[i];
                if (state == null || state.data == null)
                    continue;

                requiredTypes.Add(state.data.powerupType);
                if (!activeEffects.ContainsKey(state.data.powerupType))
                    activeEffects.Add(state.data.powerupType, CreateActiveEffect(state.data));
            }
        }

        SetStopwatchOverlay(requiredTypes.Contains(PowerupType.Stopwatch));

        List<PowerupType> typesToRemove = new List<PowerupType>();
        foreach (KeyValuePair<PowerupType, ActiveEffect> pair in activeEffects)
        {
            if (!requiredTypes.Contains(pair.Key))
                typesToRemove.Add(pair.Key);
        }

        for (int i = 0; i < typesToRemove.Count; i++)
            RemoveEffect(typesToRemove[i]);
    }

    public void ClearEffects()
    {
        List<PowerupType> types = new List<PowerupType>(activeEffects.Keys);
        for (int i = 0; i < types.Count; i++)
            RemoveEffect(types[i]);

        SetStopwatchOverlay(false);
    }

    public static void AttachPickupAura(Transform parent, Color color)
    {
        if (parent == null)
            return;

        Transform aura = parent.Find("PowerupPickupAura");
        if (aura != null)
            Object.Destroy(aura.gameObject);

        GameObject auraObject = new GameObject("PowerupPickupAura");
        auraObject.transform.SetParent(parent, false);
        ParticleSystem particles = CreateParticles(
            auraObject.transform,
            color,
            16f,
            0.45f,
            0.08f,
            0.8f,
            0.5f,
            true);

        particles.transform.localPosition = Vector3.up * 0.15f;
    }

    public static void PlayPickupBurst(Vector3 position, Color color)
    {
        GameObject burstObject = new GameObject("PowerupPickupBurst");
        burstObject.transform.position = position;

        ParticleSystem particles = CreateParticles(
            burstObject.transform,
            color,
            38f,
            1.8f,
            0.12f,
            0.55f,
            0.65f,
            false);

        particles.transform.localPosition = Vector3.up * 0.35f;
        Object.Destroy(burstObject, 1.5f);
    }

    private ActiveEffect CreateActiveEffect(PowerupData data)
    {
        Color color = data != null ? data.tint : Color.white;
        PowerupType type = data != null ? data.powerupType : PowerupType.SpeedUp;

        float emission = 18f;
        float speed = 0.7f;
        float size = 0.08f;
        float lifetime = 0.8f;
        float radius = 0.65f;
        float spin = 120f;
        bool createRing = false;
        bool createSpeedChevrons = false;

        switch (type)
        {
            case PowerupType.SpeedUp:
                emission = 24f;
                speed = 1.2f;
                size = 0.06f;
                lifetime = 0.55f;
                radius = 0.5f;
                spin = 0f;
                createSpeedChevrons = true;
                break;

            case PowerupType.Rage:
                emission = 30f;
                speed = 1.45f;
                size = 0.085f;
                lifetime = 0.5f;
                radius = 0.46f;
                spin = -240f;
                createRing = true;
                break;

            case PowerupType.Shield:
                emission = 18f;
                speed = 0.55f;
                size = 0.1f;
                lifetime = 0.95f;
                radius = 0.82f;
                spin = 80f;
                createRing = true;
                break;

            case PowerupType.Heal:
                emission = 16f;
                speed = 0.9f;
                size = 0.08f;
                lifetime = 0.85f;
                radius = 0.5f;
                spin = 145f;
                break;

            case PowerupType.Stopwatch:
                emission = 20f;
                speed = 0.65f;
                size = 0.07f;
                lifetime = 0.9f;
                radius = 0.9f;
                spin = -170f;
                createRing = true;
                break;
        }

        if (type == PowerupType.Rage)
            color.a = 0.32f;

        GameObject effectObject = new GameObject("PowerupVFX_" + type);
        effectObject.transform.SetParent(effectRoot, false);
        effectObject.transform.localPosition = type == PowerupType.SpeedUp
            ? Vector3.up * 0.05f
            : type == PowerupType.Rage
                ? Vector3.up * 0.35f
                : type == PowerupType.Shield
                    ? Vector3.zero
                    : Vector3.up * 0.75f;

        ParticleSystem particles = CreateParticles(
            effectObject.transform,
            color,
            emission,
            speed,
            size,
            lifetime,
            radius,
            true);

        if (createSpeedChevrons)
            CreateSpeedChevrons(effectObject.transform, color);

        LineRenderer ring = null;
        if (createRing)
        {
            GameObject ringObject = new GameObject("EnergyRing");
            ringObject.transform.SetParent(effectObject.transform, false);
            ringObject.transform.localPosition = type == PowerupType.Rage
                ? Vector3.down * 0.1f
                : Vector3.down * 0.55f;
            ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 40;
            ring.widthMultiplier = type == PowerupType.Shield
                ? 0.055f
                : type == PowerupType.Rage
                    ? 0.025f
                    : 0.035f;
            Color ringColor = color;
            if (type == PowerupType.Rage)
                ringColor.a = 0.28f;
            ring.startColor = ringColor;
            ring.endColor = ringColor;
            ring.material = GetParticleMaterial();
            UpdateRing(ring, radius);
        }

        return new ActiveEffect
        {
            type = type,
            root = effectObject,
            particles = particles,
            ring = ring,
            radius = radius,
            spinSpeed = spin
        };
    }

    private static void CreateSpeedChevrons(Transform parent, Color color)
    {
        Color chevronColor = color;
        chevronColor.a = 0.9f;
        CreateSpeedChevron(parent, chevronColor, -0.35f);
        CreateSpeedChevron(parent, chevronColor, -0.7f);
    }

    private static void CreateSpeedChevron(
        Transform parent,
        Color color,
        float forwardOffset)
    {
        GameObject chevronObject = new GameObject();
        chevronObject.transform.SetParent(parent, false);
        chevronObject.transform.localPosition =
            Vector3.down * 0.2f + Vector3.forward * forwardOffset;

        LineRenderer chevron = chevronObject.AddComponent<LineRenderer>();
        chevron.useWorldSpace = false;
        chevron.positionCount = 3;
        chevron.widthMultiplier = 0.065f;
        chevron.numCapVertices = 2;
        chevron.startColor = color;
        chevron.endColor = color;
        chevron.material = GetParticleMaterial();
        chevron.SetPosition(0, new Vector3(-0.18f, 0.02f, -0.12f));
        chevron.SetPosition(1, new Vector3(0f, 0.02f, 0.14f));
        chevron.SetPosition(2, new Vector3(0.18f, 0.02f, -0.12f));
    }

    private void SetStopwatchOverlay(bool active)
    {
        if (active && stopwatchOverlayObject == null)
            stopwatchOverlayObject = CreateStopwatchOverlay();

        if (stopwatchOverlayObject != null)
            stopwatchOverlayObject.SetActive(active);
    }

    private GameObject CreateStopwatchOverlay()
    {
        GameObject overlayObject = new GameObject();
        RectTransform rect = overlayObject.AddComponent<RectTransform>();
        overlayObject.transform.SetParent(transform, false);

        Canvas canvas = overlayObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;

        Image image = overlayObject.AddComponent<Image>();
        image.color = new Color(0.015f, 0.035f, 0.075f, 0.16f);
        image.raycastTarget = false;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        overlayObject.SetActive(false);
        return overlayObject;
    }

    private void RemoveEffect(PowerupType type)
    {
        ActiveEffect effect;
        if (!activeEffects.TryGetValue(type, out effect))
            return;

        activeEffects.Remove(type);
        if (effect != null && effect.root != null)
            Destroy(effect.root);
    }

    private void EnsureEffectRoot()
    {
        if (effectRoot != null)
            return;

        GameObject rootObject = new GameObject("ActivePowerupVFX");
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = Vector3.zero;
        effectRoot = rootObject.transform;
    }

    private static ParticleSystem CreateParticles(
        Transform parent,
        Color color,
        float emissionRate,
        float startSpeed,
        float startSize,
        float lifetime,
        float shapeRadius,
        bool loop)
    {
        GameObject particleObject = new GameObject("Particles");
        particleObject.transform.SetParent(parent, false);
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = loop;
        main.playOnAwake = false;
        main.duration = loop ? 1f : Mathf.Max(0.2f, lifetime);
        main.startLifetime = lifetime;
        main.startSpeed = startSpeed;
        main.startSize = startSize;
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = 0f;
        main.maxParticles = Mathf.Max(32, Mathf.RoundToInt(emissionRate * 2f));

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = shapeRadius;
        shape.arc = 360f;

        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetParticleMaterial();
        renderer.sortingOrder = 20;

        particles.Play();
        return particles;
    }

    private static void UpdateRing(LineRenderer ring, float radius)
    {
        if (ring == null)
            return;

        const int pointCount = 40;
        for (int i = 0; i < pointCount; i++)
        {
            float angle = i / (float)pointCount * Mathf.PI * 2f;
            ring.SetPosition(
                i,
                new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    private static Material GetParticleMaterial()
    {
        if (sharedParticleMaterial != null)
            return sharedParticleMaterial;

        Shader shader = Shader.Find("Custom/Gigachad/Golden Sand Particle");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            sharedParticleMaterial = new Material(shader)
            {
                name = "PowerupRuntimeParticleMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        return sharedParticleMaterial;
    }
}
