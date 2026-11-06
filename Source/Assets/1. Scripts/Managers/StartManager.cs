using UnityEngine;

public class StartManager : MonoBehaviour
{
    public Transform startPoint;

    public Transform player;

    private void Start()
    {
        if (startPoint == null)
        {
            Debug.LogError("startPoint�� �������� �ʾҽ��ϴ�.");
            return;
        }

        if (player == null)
        {
            Debug.LogError("player�� �������� �ʾҽ��ϴ�.");
            return;
        }

        MovePlayerToStartPoint();
    }

    private void MovePlayerToStartPoint()
    {
        player.position = startPoint.position;
        player.rotation = startPoint.rotation;
    }
}



