using UnityEngine;

public class BillboardUI : MonoBehaviour
{
    [SerializeField] private Camera targetCam;

    private void Awake()
    {
        if (targetCam == null) targetCam = Camera.main;
    }

    private void LateUpdate()
    {
        if (targetCam == null) return;
        transform.forward = targetCam.transform.forward;
    }
}