using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.Events;
public class ButtonScaler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Vector2 startScale = Vector2.one;
    public Vector2 endScale = new Vector2(0.9f, 0.9f);
    [SerializeField] bool useAudio = true;
    [SerializeField] protected UnityEvent eventOnPointDown;
    [SerializeField] protected UnityEvent eventOnPointUp;
    [SerializeField] private AudioSource audioSource;
    /*#if UNITY_EDITOR
        private void OnValidate(){
            startScale = transform.localScale;
            endScale = new Vector2(startScale.x + 0.1f, startScale.y + 0.1f);
        }
    #endif*/
    public void OnPointerDown(PointerEventData eventData)
    {
        transform.DOScale(endScale, 0.1f).SetEase(Ease.Linear).SetUpdate(true).SetId(this);
        if (eventOnPointDown != null)
        {
            eventOnPointDown.Invoke();
        }
        if (useAudio)
        {
            //audioSource.Play();
        }
    }
    public void OnPointerUp(PointerEventData eventData)
    {
        transform.DOScale(startScale, 0.1f).SetEase(Ease.Linear).SetUpdate(true).SetId(this);
        if (eventOnPointUp != null)
        {
            eventOnPointUp.Invoke();
        }
    }
    private void OnDisable()
    {
        this.DOKill();
    }
}
