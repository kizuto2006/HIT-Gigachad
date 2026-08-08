using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Controls the presentation and input of the player death screen.
/// The visual hierarchy remains editable in DeadCanvas.prefab while the
/// lifecycle and timing stay independent from GameManager.
/// </summary>
[DisallowMultipleComponent]
public sealed class DeadUIController : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0f)] private float showDelaySeconds = 2f;
    [SerializeField, Min(0.05f)] private float fadeInDuration = 0.4f;
    [SerializeField, Range(0.8f, 1f)] private float contentStartScale = 0.94f;

    private CanvasGroup canvasGroup;
    private GameObject introRoot;
    private GameObject summaryRoot;
    private Button introConfirmButton;
    private Button summaryConfirmButton;
    private RectTransform introContent;
    private RectTransform summaryContent;
    private UnityAction returnToStartAction;
    private Coroutine showRoutine;
    private bool isShowing;
    private bool inputReady;

    public bool IsShowing => isShowing;
    public float ShowDelaySeconds => showDelaySeconds;

    private void Awake()
    {
        ResolveReferences();
        PrepareHiddenState();
    }

    private void OnDisable()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }
    }

    public void Configure(UnityAction onReturnToStart)
    {
        returnToStartAction = onReturnToStart;
        ResolveReferences();
        WireButtons();
    }

    public void Show()
    {
        ResolveReferences();
        WireButtons();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
        }

        showRoutine = StartCoroutine(ShowRoutine());
    }

    public void HideImmediate()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        isShowing = false;
        inputReady = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (introRoot != null)
        {
            introRoot.SetActive(true);
        }

        if (summaryRoot != null)
        {
            summaryRoot.SetActive(false);
        }

        ResetContentScale(introContent);
        ResetContentScale(summaryContent);
    }

    private IEnumerator ShowRoutine()
    {
        isShowing = true;
        inputReady = false;

        if (introRoot != null)
        {
            introRoot.SetActive(true);
        }

        if (summaryRoot != null)
        {
            summaryRoot.SetActive(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        SetContentScale(introContent, contentStartScale);

        if (showDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(showDelaySeconds);
        }

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / fadeInDuration);
            float eased = EaseOutCubic(progress);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = eased;
            }

            SetContentScale(introContent, Mathf.LerpUnclamped(contentStartScale, 1f, eased));
            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        ResetContentScale(introContent);
        inputReady = true;
        showRoutine = null;
    }

    private void ShowSummary()
    {
        if (!inputReady || summaryRoot == null)
        {
            ReturnToStart();
            return;
        }

        if (introRoot != null)
        {
            introRoot.SetActive(false);
        }

        summaryRoot.SetActive(true);
        SetContentScale(summaryContent, contentStartScale);
        StartCoroutine(AnimateContentIn(summaryContent));
    }

    private IEnumerator AnimateContentIn(RectTransform content)
    {
        if (content == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / fadeInDuration);
            content.localScale = Vector3.one * Mathf.LerpUnclamped(
                contentStartScale,
                1f,
                EaseOutCubic(progress));
            yield return null;
        }

        ResetContentScale(content);
    }

    private void ReturnToStart()
    {
        if (!inputReady)
        {
            return;
        }

        inputReady = false;
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        returnToStartAction?.Invoke();
    }

    private void ResolveReferences()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (introRoot == null)
        {
            introRoot = FindChild("Intro");
        }

        if (summaryRoot == null)
        {
            summaryRoot = FindChild("Overoll");
            if (summaryRoot == null)
            {
                summaryRoot = FindChild("Summary");
            }
        }

        if (introConfirmButton == null && introRoot != null)
        {
            introConfirmButton = introRoot.GetComponentInChildren<Button>(true);
        }

        if (summaryConfirmButton == null && summaryRoot != null)
        {
            summaryConfirmButton = summaryRoot.GetComponentInChildren<Button>(true);
        }

        if (introContent == null)
        {
            introContent = FindAnimatedContent(introRoot);
        }

        if (summaryContent == null)
        {
            summaryContent = FindAnimatedContent(summaryRoot);
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 1000);
        }
    }

    private void WireButtons()
    {
        if (introConfirmButton != null)
        {
            introConfirmButton.onClick.RemoveListener(ShowSummary);
            introConfirmButton.onClick.RemoveListener(ReturnToStart);
            if (summaryRoot != null)
            {
                introConfirmButton.onClick.AddListener(ShowSummary);
            }
            else
            {
                introConfirmButton.onClick.AddListener(ReturnToStart);
            }
        }

        if (summaryConfirmButton != null)
        {
            summaryConfirmButton.onClick.RemoveListener(ShowSummary);
            summaryConfirmButton.onClick.RemoveListener(ReturnToStart);
            summaryConfirmButton.onClick.AddListener(ReturnToStart);
        }
    }

    private void PrepareHiddenState()
    {
        isShowing = false;
        inputReady = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (introRoot != null)
        {
            introRoot.SetActive(true);
        }

        if (summaryRoot != null)
        {
            summaryRoot.SetActive(false);
        }

        ResetContentScale(introContent);
        ResetContentScale(summaryContent);
    }

    private GameObject FindChild(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.gameObject : null;
    }

    private static RectTransform FindAnimatedContent(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        Transform table = root.transform.Find("Table");
        if (table is RectTransform tableRect)
        {
            return tableRect;
        }

        return root.transform as RectTransform;
    }

    private static void SetContentScale(RectTransform content, float scale)
    {
        if (content != null)
        {
            content.localScale = Vector3.one * scale;
        }
    }

    private static void ResetContentScale(RectTransform content)
    {
        SetContentScale(content, 1f);
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }
}
