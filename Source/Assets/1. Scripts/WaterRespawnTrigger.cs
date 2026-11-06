using UnityEngine;

public class WaterRespawnTrigger : MonoBehaviour
{
    [Header("플레이어 설정")]
    [SerializeField] private string playerTag = "Player";

    [Header("폴백 리스폰 위치 (PlayerRespawnController 없을 때 사용)")]
    [SerializeField] private Transform fallbackRespawnPoint;

    [Header("디버그")]
    [SerializeField] private bool enableDebugLog = false;

    // 쿨다운으로 OnTriggerStay 중복 호출 방지
    private float respawnCooldown = 0f;
    private const float RESPAWN_COOLDOWN = 1.5f;

    private void Update()
    {
        if (respawnCooldown > 0f)
            respawnCooldown -= Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other) => TryRespawn(other);

    // Enter가 누락된 경우 대비 (물 안에서 계속 아래로 내려갈 때 포착)
    private void OnTriggerStay(Collider other) => TryRespawn(other);

    private void TryRespawn(Collider other)
    {
        if (respawnCooldown > 0f)
            return;

        if (!other.CompareTag(playerTag))
            return;

        // PlayerRespawnController를 통한 정식 리스폰
        PlayerRespawnController controller = other.GetComponent<PlayerRespawnController>();
        if (controller == null)
            controller = other.GetComponentInParent<PlayerRespawnController>();

        if (controller != null)
        {
            if (enableDebugLog)
                Debug.Log("[WaterRespawnTrigger] PlayerRespawnController로 리스폰합니다.", this);

            respawnCooldown = RESPAWN_COOLDOWN;
            controller.리스폰실행();
            return;
        }

        // PlayerRespawnController 없을 때 fallback: 직접 위치 복원
        if (fallbackRespawnPoint != null)
        {
            if (enableDebugLog)
                Debug.Log("[WaterRespawnTrigger] fallback 위치로 리스폰합니다.", this);

            respawnCooldown = RESPAWN_COOLDOWN;

            Transform root = other.transform.root;
            Rigidbody rb = root.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = fallbackRespawnPoint.position;
            }
            else
            {
                root.position = fallbackRespawnPoint.position;
            }

            Physics.SyncTransforms();
            return;
        }

        Debug.LogWarning("[WaterRespawnTrigger] PlayerRespawnController도 없고 fallbackRespawnPoint도 없습니다. Inspector에서 설정하거나 플레이어에 PlayerRespawnController를 추가해주세요.", this);
    }
}
