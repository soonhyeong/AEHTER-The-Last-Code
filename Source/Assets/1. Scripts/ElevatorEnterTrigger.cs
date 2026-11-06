using UnityEngine;

public class ElevatorEnterTrigger : MonoBehaviour
{
    public ElevatorController elevator;

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || elevator == null || !other.CompareTag("Player")) return;

        elevator.CloseDoorAndMove();
    }
}
