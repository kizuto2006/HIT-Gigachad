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
        if (isDead || raw <= 0f)
            return;

        PlayerPowerupController powerups = GetComponent<PlayerPowerupController>();
        if (powerups != null && powerups.IsInvulnerable)
            return;
        if (stats != null &&
            stats.FinalDodgeChance > 0f &&
            UnityEngine.Random.value < stats.FinalDodgeChance)
        {
            return;
        }

        if (stats != null)
            raw *= 1f - stats.FinalArmorReduction;

        if (raw > 0f)
        {
            float damageApplied = Mathf.Min(raw, currentHp);
            currentHp -= raw;
            TriggerHitFlash();
            DamageNumberPopup.ShowHealthChange(-damageApplied, GetHealthNumberPosition());
            if (damageApplied > 0f)
                SoundEffectsAudioManager.Instance?.PlayTakeDamageSound();
        }

        if (currentHp <= 0f)
        {
            currentHp = 0f;
            Die();
            return;
        }
    }

    public void Heal(float amount)
    {
        if (isDead || amount <= 0f)
            return;

        float maxHealth = stats != null ? stats.FinalHp : 100f;
        float previousHp = currentHp;
        currentHp = Mathf.Clamp(currentHp + amount, 0f, maxHealth);

        float healedAmount = currentHp - previousHp;
        if (healedAmount > 0f)
            DamageNumberPopup.ShowHealthChange(healedAmount, GetHealthNumberPosition());
    }

    public void HealToFull()
    {
        if (isDead)
            return;

        float maxHealth = stats != null ? stats.FinalHp : 100f;
        float previousHp = currentHp;
        currentHp = maxHealth;

        float healedAmount = currentHp - previousHp;
        if (healedAmount > 0f)
            DamageNumberPopup.ShowHealthChange(healedAmount, GetHealthNumberPosition());
    }

    private Vector3 GetHealthNumberPosition()
    {
        if (cachedRenderers != null)
        {
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                Renderer targetRenderer = cachedRenderers[i];
                if (targetRenderer == null || !targetRenderer.enabled)
                    continue;

                Bounds bounds = targetRenderer.bounds;
                return bounds.center + Vector3.up * (bounds.extents.y + 0.2f);
            }
        }

        return transform.position + Vector3.up;
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

        PlayerPowerupController powerups = GetComponent<PlayerPowerupController>();
        if (powerups != null)
            powerups.ClearAllPowerups();

        SoundEffectsAudioManager.Instance?.PlayLoseSound();
        Debug.Log("Player died");
        Died?.Invoke();
    }
}
