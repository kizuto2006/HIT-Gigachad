using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UIButtonSfx : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, ISubmitHandler
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (CanPlayFeedback())
        {
            AudioManager.Instance?.PlayButtonHover();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && CanPlayFeedback())
        {
            AudioManager.Instance?.PlayButtonClick();
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (CanPlayFeedback())
        {
            AudioManager.Instance?.PlayButtonClick();
        }
    }

    private bool CanPlayFeedback()
    {
        return button != null && button.IsInteractable();
    }

    public static void AttachToScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            AttachTo(root);
        }
    }

    public static void AttachTo(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button targetButton in buttons)
        {
            if (targetButton.GetComponent<UIButtonSfx>() == null)
            {
                targetButton.gameObject.AddComponent<UIButtonSfx>();
            }
        }
    }
}
