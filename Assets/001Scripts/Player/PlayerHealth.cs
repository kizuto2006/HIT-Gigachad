using UnityEngine;
using System.Collections;
using System;

public class PlayerHealth : MonoBehaviour
{
    [Header("── Stats Reference ──")]
    public PlayerBaseStats stats;

    [Header("── Hit Flash ──")]
    public float hitFlashDuration = 0.1f;
    public Color hitFlashColor = Color.red;

    // Runtime values
    [HideInInspector] public float currentHp;
    public event Action Died;

    private Renderer[] cachedRenderers;
    private MaterialPropertyBlock[] originalPropertyBlocks;
    private MaterialPropertyBlock[] flashPropertyBlocks;
    private Coroutine flashCoroutine;
    private bool isDead;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    void Start()
    {
        currentHp = stats != null ? stats.FinalHp : 100f;
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        originalPropertyBlocks = new MaterialPropertyBlock[cachedRenderers.Length];
        flashPropertyBlocks = new MaterialPropertyBlock[cachedRenderers.Length];

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            originalPropertyBlocks[i] = new MaterialPropertyBlock();
            flashPropertyBlocks[i] = new MaterialPropertyBlock();
            cachedRenderers[i].GetPropertyBlock(originalPropertyBlocks[i]);
        }
    }

    /// <summary>
    /// Nhận damage trực tiếp vào HP
    /// </summary>
    public void TakeDamage(float raw)
    {
        if (isDead || raw <= 0f) return;

        if (stats != null)
            raw *= 1f - stats.FinalArmorReduction;

        if (raw > 0f)
        {
            currentHp -= raw;
            TriggerHitFlash();
        }

        if (currentHp <= 0f)
        {
            currentHp = 0f;
            Die();
            return;
        }
    }

    private void TriggerHitFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
        flashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        if (cachedRenderers == null) yield break;

        // Apply flash
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer targetRenderer = cachedRenderers[i];
            if (targetRenderer == null) continue;

            targetRenderer.GetPropertyBlock(flashPropertyBlocks[i]);

            flashPropertyBlocks[i].SetTexture(BaseMapId, Texture2D.whiteTexture);
            flashPropertyBlocks[i].SetTexture(MainTexId, Texture2D.whiteTexture);
            flashPropertyBlocks[i].SetColor(BaseColorId, hitFlashColor);
            flashPropertyBlocks[i].SetColor(ColorId, hitFlashColor);
            
            targetRenderer.SetPropertyBlock(flashPropertyBlocks[i]);
        }

        yield return new WaitForSeconds(hitFlashDuration);

        // Restore
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].SetPropertyBlock(originalPropertyBlocks[i]);
            }
        }

        flashCoroutine = null;
    }

    private void OnDisable()
    {
        if (cachedRenderers == null) return;
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].SetPropertyBlock(originalPropertyBlocks[i]);
            }
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        Debug.Log("Player died");
        Died?.Invoke();
    }
}
