using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class HabitLogButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private Button button;
    private Vector3 restScale;

    private void Awake()
    {
        button = GetComponent<Button>();
        restScale = transform.localScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !button.IsInteractable())
            return;
        transform.DOKill();
        transform.DOScale(restScale * 0.95f, 0.1f).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    public void OnPointerUp(PointerEventData eventData) => Release();
    public void OnPointerExit(PointerEventData eventData) => Release();

    private void Release()
    {
        transform.DOKill();
        transform.DOScale(restScale, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void OnDisable()
    {
        transform.DOKill();
        transform.localScale = restScale;
    }
}
