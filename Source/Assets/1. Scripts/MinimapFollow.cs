using UnityEngine;
using UnityEngine.SceneManagement;

public class MinimapFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;   // 따라갈 대상 (Player)

    [Header("Camera Height")]
    [SerializeField] private float height = 50f; // 미니맵 카메라 높이

    [Header("Rotation")]
    [SerializeField] private bool rotateWithTarget = false; // true면 플레이어 방향에 맞춰 회전
    private readonly LocalPlayerUIBinding localBinding = new LocalPlayerUIBinding();
    private bool preserveScenePose;
    private float sceneAltitude;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetHooks() { SceneManager.sceneLoaded -= EnsureOperatingFollow; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= EnsureOperatingFollow;
        SceneManager.sceneLoaded += EnsureOperatingFollow;
        for (int i = 0; i < SceneManager.sceneCount; i++) EnsureOperatingFollow(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    private static void EnsureOperatingFollow(Scene scene, LoadSceneMode mode)
    {
        if (!scene.isLoaded || scene.name != "05.폐허된 도시") return;
        Camera candidate = null;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var camera in root.GetComponentsInChildren<Camera>(true))
        {
            if (camera.gameObject.name != "MinimapCamera" || !camera.orthographic || camera.targetTexture == null) continue;
            if (candidate != null) { Debug.LogWarning("[MinimapFollow] Ambiguous operating minimap camera; binding deferred."); return; }
            candidate = camera;
        }
        if (candidate == null || candidate.GetComponent<MinimapFollow>() != null) return;
        var follow = candidate.gameObject.AddComponent<MinimapFollow>();
        follow.preserveScenePose = true;
        follow.sceneAltitude = candidate.transform.position.y;
    }

    private void Start()
    {
        localBinding.Poll(this, target, true);
        target = localBinding.Root;
    }

    private void OnDisable() { target = null; localBinding.Clear(); }

    private void LateUpdate()
    {
        localBinding.Poll(this, target);
        target = localBinding.Root;
        if (target == null) return;

        // 위치 따라가기
        Vector3 targetPos = target.position;
        transform.position = new Vector3(
            targetPos.x,
            preserveScenePose ? sceneAltitude : targetPos.y + height,
            targetPos.z
        );

        // 회전 처리
        if (preserveScenePose) return;
        if (rotateWithTarget)
        {
            transform.rotation = Quaternion.Euler(90f, target.eulerAngles.y, 0f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 북쪽 고정
        }
    }
}
