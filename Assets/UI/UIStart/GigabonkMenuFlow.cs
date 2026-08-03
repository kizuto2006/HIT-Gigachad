using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GigabonkMenuFlow : MonoBehaviour
{
    [Header("Camera advance")]
    [SerializeField] private float advanceDistance = 2.1f;
    [SerializeField] private float advanceDuration = 0.85f;

    private Transform menuCamera;
    private GameObject menuMapRoot;
    private GameObject menuCanvas;
    private Vector3 initialCameraPosition;
    private Quaternion initialCameraRotation;
    private Transform menuTitle;
    private Vector3 menuTitleBaseScale;
    private bool isTransitioning;

    private void Awake()
    {
        menuCamera = Camera.main != null ? Camera.main.transform : transform;
        menuMapRoot = GameObject.Find("GigabonkMenuMap");
        menuCanvas = GameObject.Find("CanvasStartUI");

        initialCameraPosition = menuCamera.position;
        initialCameraRotation = menuCamera.rotation;

        SetupMenuBranding();
        CreateSandDustField();
    }
    private void Update()
    {
        if (menuTitle == null)
            return;

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 2.1f) * 0.035f;
        menuTitle.localScale = menuTitleBaseScale * pulse;
    }

    private void SetupMenuBranding()
    {
        GameObject tagline = GameObject.Find("GigabonkTagline");
        if (tagline != null)
            tagline.SetActive(false);

        GameObject title = GameObject.Find("GigabonkLogo");
        if (title == null)
            return;

        menuTitle = title.transform;
        menuTitleBaseScale = menuTitle.localScale;

        Outline outline = title.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(5f, -5f);
            outline.useGraphicAlpha = true;
        }
    }

    private void CreateSandDustField()
    {
        if (menuMapRoot == null || menuCamera == null || menuMapRoot.transform.Find("MenuSandDust") != null)
            return;

        GameObject dustRoot = new GameObject("MenuSandDust");
        dustRoot.transform.SetParent(menuMapRoot.transform, false);

        Material particleMaterial = CreateSandParticleMaterial();
        Vector3 fieldCenter = menuCamera.position + menuCamera.forward * 7f + Vector3.up * 0.25f;

        CreateDustStream(dustRoot.transform, "DustLeftToRight", fieldCenter, 1f, particleMaterial, 17f);
        CreateDustStream(dustRoot.transform, "DustRightToLeft", fieldCenter + Vector3.up * 0.45f, -1f, particleMaterial, 11f);
    }

    private static void CreateDustStream(
        Transform parent,
        string streamName,
        Vector3 position,
        float direction,
        Material particleMaterial,
        float emissionRate)
    {
        GameObject stream = new GameObject(streamName);
        stream.transform.SetParent(parent, false);
        stream.transform.position = position;

        ParticleSystem particles = stream.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.duration = 7f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.72f, 0.49f, 0.22f, 0.22f),
            new Color(1f, 0.82f, 0.48f, 0.52f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 180;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = emissionRate;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(11f, 2.6f, 2.4f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = direction > 0f
            ? new ParticleSystem.MinMaxCurve(0.35f, 0.95f)
            : new ParticleSystem.MinMaxCurve(-0.95f, -0.35f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.04f, 0.14f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = 0.24f;
        noise.strengthY = 0.16f;
        noise.strengthZ = 0.12f;
        noise.frequency = 0.32f;
        noise.scrollSpeed = 0.12f;
        noise.damping = true;

        ParticleSystem.ColorOverLifetimeModule colourOverLifetime = particles.colorOverLifetime;
        colourOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.86f, 0.58f), 0.55f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.72f, 0.2f),
                new GradientAlphaKey(0.55f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            });
        colourOverLifetime.color = fade;

        ParticleSystemRenderer particleRenderer = stream.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.material = particleMaterial;
        particleRenderer.sortingFudge = 1f;

        particles.Play(true);
    }

    private static Material CreateSandParticleMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");
        Material material = new Material(shader)
        {
            name = "Menu Sand Dust (Runtime)",
            hideFlags = HideFlags.DontSave
        };

        const int textureSize = 32;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "Soft Sand Particle (Runtime)",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color[] pixels = new Color[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float normalizedX = (x + 0.5f) / textureSize * 2f - 1f;
                float normalizedY = (y + 0.5f) / textureSize * 2f - 1f;
                float radius = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2f);
                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        material.mainTexture = texture;
        return material;
    }

    public void BeginPlay()
    {
        if (isTransitioning)
            return;

        StartCoroutine(AdvanceCameraAndShowCharacter());
    }

    public void ExitMenu()
    {
        if (menuMapRoot != null)
            menuMapRoot.SetActive(false);

        if (menuCanvas != null)
            menuCanvas.SetActive(false);
    }

    public void ReturnToMainMenu()
    {
        if (isTransitioning)
            return;

        StartCoroutine(ReturnCameraAndShowMenu());
    }

    private IEnumerator AdvanceCameraAndShowCharacter()
    {
        isTransitioning = true;

        if (menuCamera == null)
            menuCamera = transform;

        Vector3 startPosition = menuCamera.position;
        Vector3 targetPosition = startPosition + menuCamera.forward * advanceDistance;
        float elapsed = 0f;

        while (elapsed < advanceDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / advanceDuration);
            float eased = 1f - Mathf.Pow(1f - normalized, 3f);
            menuCamera.position = Vector3.Lerp(startPosition, targetPosition, eased);
            yield return null;
        }

        menuCamera.position = targetPosition;

        if (UIController.Instance != null)
        {
            UIController.Instance.StartUI.SetActiveStartPanel(false);
            UIController.Instance.SelectCharacterUI.SetActiveCharacter(true);
        }

        isTransitioning = false;
    }

    private IEnumerator ReturnCameraAndShowMenu()
    {
        isTransitioning = true;

        if (UIController.Instance != null)
        {
            UIController.Instance.SelectCharacterUI.SetActiveCharacter(false);
            UIController.Instance.SelectMapUI.SetActiveMap(false);
        }

        Vector3 startPosition = menuCamera.position;
        Quaternion startRotation = menuCamera.rotation;
        float elapsed = 0f;

        while (elapsed < advanceDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / advanceDuration);
            float eased = 1f - Mathf.Pow(1f - normalized, 3f);
            menuCamera.position = Vector3.Lerp(startPosition, initialCameraPosition, eased);
            menuCamera.rotation = Quaternion.Slerp(startRotation, initialCameraRotation, eased);
            yield return null;
        }

        menuCamera.position = initialCameraPosition;
        menuCamera.rotation = initialCameraRotation;

        if (UIController.Instance != null)
            UIController.Instance.StartUI.SetActiveStartPanel(true);

        isTransitioning = false;
    }
}
