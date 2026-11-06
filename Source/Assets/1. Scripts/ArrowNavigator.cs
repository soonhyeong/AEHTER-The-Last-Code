using UnityEngine;
using TMPro;

public class ArrowNavigator : MonoBehaviour
{
    [Header("기본 참조")]
    public Transform player;
    public Transform target;
    public RectTransform arrowImage;

    [Header("방향 기준")]
    public bool useCameraForward = true;
    public Transform cameraTransform;

    [Header("UI 텍스트")]
    public TMP_Text floorGuideText;
    public TMP_Text distanceText;

    [Header("도착 판정 거리")]
    public float arriveDistance = 2f;

    private FloorDetector floorDetector;
    private NavigationTarget navigationTarget;

    void Start()
    {
        if (player != null)
            floorDetector = player.GetComponent<FloorDetector>();

        if (target != null)
            navigationTarget = target.GetComponent<NavigationTarget>();
    }

    void Update()
    {
        if (player == null || target == null || arrowImage == null)
            return;

        if (floorDetector == null)
            floorDetector = player.GetComponent<FloorDetector>();

        if (navigationTarget == null)
            navigationTarget = target.GetComponent<NavigationTarget>();

        UpdateNavigation();
    }

    void UpdateNavigation()
    {
        float distance = Vector3.Distance(player.position, target.position);

        if (distanceText != null)
        {
            distanceText.text = Mathf.RoundToInt(distance) + "m";
        }

        if (distance <= arriveDistance)
        {
            arrowImage.gameObject.SetActive(false);

            if (floorGuideText != null)
                floorGuideText.text = "도착";

            return;
        }

        if (floorDetector == null || navigationTarget == null)
        {
            arrowImage.gameObject.SetActive(true);

            if (floorGuideText != null)
                floorGuideText.text = "목표 지점으로 이동";

            RotateArrow();
            return;
        }

        FloorDetector.FloorType playerFloor = floorDetector.currentFloor;
        FloorDetector.FloorType targetFloor = ConvertTargetFloor(navigationTarget.targetFloor);

        if (playerFloor == targetFloor)
        {
            arrowImage.gameObject.SetActive(true);

            if (floorGuideText != null)
                floorGuideText.text = "목표 지점으로 이동";

            RotateArrow();
        }
        else
        {
            arrowImage.gameObject.SetActive(false);

            if (floorGuideText != null)
            {
                if ((int)targetFloor > (int)playerFloor)
                    floorGuideText.text = "↑ 위층으로 이동";
                else
                    floorGuideText.text = "↓ 아래층으로 이동";
            }
        }
    }

    void RotateArrow()
    {
        Vector3 dirToTarget = target.position - player.position;
        dirToTarget.y = 0f;
        dirToTarget.Normalize();

        Vector3 forwardDir;

        if (useCameraForward && cameraTransform != null)
            forwardDir = cameraTransform.forward;
        else
            forwardDir = player.forward;

        forwardDir.y = 0f;
        forwardDir.Normalize();

        float angle = Vector3.SignedAngle(forwardDir, dirToTarget, Vector3.up);

        arrowImage.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    FloorDetector.FloorType ConvertTargetFloor(NavigationTarget.FloorType navFloor)
    {
        switch (navFloor)
        {
            case NavigationTarget.FloorType.Floor1:
                return FloorDetector.FloorType.Floor1;
            case NavigationTarget.FloorType.Floor2:
                return FloorDetector.FloorType.Floor2;
            case NavigationTarget.FloorType.Floor3:
                return FloorDetector.FloorType.Floor3;
        }

        return FloorDetector.FloorType.Floor1;
    }
}