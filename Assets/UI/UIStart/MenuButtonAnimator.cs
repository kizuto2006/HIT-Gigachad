using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button), typeof(Image))]
public class MenuButtonAnimator : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    [Header("Motion")]
    [SerializeField] private float hoverScale = 1.055f;
    [SerializeField] private float hoverLift = 5f;
    [SerializeField] private float pressDepth = 5f;
    [SerializeField] private float animationSpeed = 16f;

    [Header("Colours")]
    [SerializeField] private Color normalColour = new(0.31f, 0.31f, 0.31f, 1f);
    [SerializeField] private Color hoverColour = new(0.39f, 0.39f, 0.39f, 1f);
    [SerializeField] private Color pressedColour = new(0.23f, 0.23f, 0.23f, 1f);

    private RectTransform rectTransform;
    private Image background;
    private Vector2 restingPosition;
    private Vector2 targetPosition;
    private Vector3 targetScale;
    private Color targetColour;
    private bool isHovered;
    private bool isLayoutControlled;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        background = GetComponent<Image>();
        // Character cards already have their own art direction. Derive animation
        // colours from that artwork instead of replacing it with menu-button grey.
        if (gameObject.name.StartsWith("Character"))
        {
            normalColour = background.color;
            hoverColour = Color.Lerp(normalColour, Color.white, 0.12f);
            pressedColour = Color.Lerp(normalColour, Color.black, 0.16f);
        }

        isLayoutControlled = transform.parent != null &&
            transform.parent.GetComponent<LayoutGroup>() != null;
        restingPosition = rectTransform.anchoredPosition;
        targetPosition = restingPosition;
        targetScale = Vector3.one;
        targetColour = normalColour;

        GetComponent<Button>().transition = Selectable.Transition.None;
        background.color = normalColour;
    }

    private void OnEnable()
    {
        if (rectTransform == null)
            return;

        restingPosition = rectTransform.anchoredPosition;
        SetNormalState(true);
    }

    public void RefreshRestingPosition()
    {
        if(rectTransform == null)
            rectTransform = (RectTransform)transform;

        if(background == null)
            background = GetComponent<Image>();

        restingPosition = rectTransform.anchoredPosition;
        targetPosition = restingPosition;
        targetScale = Vector3.one;
        targetColour = normalColour;
    }

    private void Update()
    {
        float t = 1f - Mathf.Exp(-animationSpeed * Time.unscaledDeltaTime);
        if (!isLayoutControlled)
            rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, targetPosition, t);

        rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, t);
        background.color = Color.Lerp(background.color, targetColour, t);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        SetHoverState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        SetNormalState(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetPosition = restingPosition + Vector2.down * pressDepth;
        targetScale = Vector3.one * 0.985f;
        targetColour = pressedColour;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isHovered)
            SetHoverState();
        else
            SetNormalState(false);
    }

    public void OnSelect(BaseEventData eventData)
    {
        isHovered = true;
        SetHoverState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isHovered = false;
        SetNormalState(false);
    }

    private void SetHoverState()
    {
        targetPosition = restingPosition + Vector2.up * hoverLift;
        targetScale = Vector3.one * hoverScale;
        targetColour = hoverColour;
    }

    private void SetNormalState(bool immediate)
    {
        targetPosition = restingPosition;
        targetScale = Vector3.one;
        targetColour = normalColour;

        if (!immediate)
            return;

        if (!isLayoutControlled)
            rectTransform.anchoredPosition = targetPosition;

        rectTransform.localScale = targetScale;
        background.color = targetColour;
    }
}
