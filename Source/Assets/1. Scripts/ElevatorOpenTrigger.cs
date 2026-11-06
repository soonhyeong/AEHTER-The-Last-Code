using UnityEngine;

public class ElevatorOpenTrigger : MonoBehaviour
{
    public ElevatorController elevator;

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || elevator == null || !other.CompareTag("Player")) return;

        elevator.OpenDoor();
    }
}
