using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Receives mouse/touch clicks and keyboard/controller submit events from any
/// Unity UI Selectable. It is added at runtime so dynamically-created panels
/// receive the same ClickSound without changing every prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class UIInteractionSound : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null &&
            eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        SoundEffectsAudioManager.Instance?.PlayClickSound();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        SoundEffectsAudioManager.Instance?.PlayClickSound();
    }
}
