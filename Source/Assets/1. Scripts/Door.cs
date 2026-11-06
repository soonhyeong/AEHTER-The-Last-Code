using System.Collections;
using UnityEngine;

public class Door : MonoBehaviour
{
    [Header("문 Transform")]
    public Transform leftDoor;    // 왼쪽 문 오브젝트
    public Transform rightDoor;   // 오른쪽 문 오브젝트

    [Header("회전 설정")]
    public float openDuration = 1f;
    public float leftTargetAngle = -90f;   // 왼쪽 문 바깥으로 열릴 각
    public float rightTargetAngle = 90f;   // 오른쪽 문 바깥으로 열릴 각

    private bool isOpen = false;

    public void OpenDoors()
    {
        if (isOpen) return;
        isOpen = true;

        StartCoroutine(OpenRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        Quaternion leftStartRot = leftDoor.localRotation;
        Quaternion rightStartRot = rightDoor.localRotation;

        Quaternion leftEndRot = Quaternion.Euler(
            leftStartRot.eulerAngles.x,
            leftStartRot.eulerAngles.y + leftTargetAngle,
            leftStartRot.eulerAngles.z);

        Quaternion rightEndRot = Quaternion.Euler(
            rightStartRot.eulerAngles.x,
            rightStartRot.eulerAngles.y + rightTargetAngle,
            rightStartRot.eulerAngles.z);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / openDuration;

            leftDoor.localRotation = Quaternion.Slerp(leftStartRot, leftEndRot, t);
            rightDoor.localRotation = Quaternion.Slerp(rightStartRot, rightEndRot, t);

            yield return null;
        }

        leftDoor.localRotation = leftEndRot;
        rightDoor.localRotation = rightEndRot;
    }
}
