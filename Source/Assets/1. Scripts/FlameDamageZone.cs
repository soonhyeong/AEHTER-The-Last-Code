using UnityEngine;

public class FlameDamageZone : MonoBehaviour
{
    [Header("데미지 설정")]
    public float damagePerSecond = 10f;

    private void OnTriggerStay(Collider other)
    {
        // 플레이어만 맞게 하고 싶으면 태그 체크
        if (!other.CompareTag("Player"))
            return;

        // PlayerStatus 스크립트 찾기
        PlayerStatus status = other.GetComponent<PlayerStatus>();
        if (status == null)
        {
            status = other.GetComponentInParent<PlayerStatus>();
        }

        if (status != null)
        {
            status.TakeDamage(damagePerSecond * Time.deltaTime);
        }
    }
}