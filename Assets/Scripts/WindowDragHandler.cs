using UnityEngine;
using UnityEngine.EventSystems;

public class WindowDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    //the window we're moving
    [SerializeField] private RectTransform window;

    //object that stores where the mouse grabbed the window
    private Vector2 dragOffset;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if(window == null)
        {
            return;
        }
        
        //get the window's parent
        RectTransform parent = window.parent as RectTransform;

       
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out Vector2 mousePosition))
        {
            //save the offset so the window doesn't jump when dragging
            dragOffset = window.anchoredPosition - mousePosition;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if(window == null)
        {
            return;
        }

        RectTransform parent = window.parent as RectTransform;

        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out Vector2 mousePosition))
        {
            //moves the window to follow the mouse
            window.anchoredPosition = mousePosition + dragOffset;
        }
    }
}
