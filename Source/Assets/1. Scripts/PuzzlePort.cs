using UnityEngine;
using UnityEngine.EventSystems;

public class PuzzlePort : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log("?ы듃 ?쒕옒洹??쒖옉");
    }

    public void OnDrag(PointerEventData eventData)
    {
        Debug.Log("Port dragging.");
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log("Port drag ended.");
    }
}
