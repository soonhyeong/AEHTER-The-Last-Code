using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerSetup : NetworkBehaviour
{
    [Header("1��Ī ī�޶�")]
    [SerializeField] private Camera playerCamera;

    [Header("Audio Listener")]
    [SerializeField] private AudioListener audioListener;

    public override void OnNetworkSpawn()
    {
        ApplyPresentationOwnership();
    }

    public override void OnGainedOwnership()
    {
        ApplyPresentationOwnership();
    }

    public override void OnLostOwnership()
    {
        ApplyPresentationOwnership();
    }

    public override void OnNetworkDespawn()
    {
        // Only retire this player's presentation; do not select a scene camera.
        SetPresentationActive(false);
    }

    private void ApplyPresentationOwnership()
    {
        // Authored, unspawned players keep their standalone prefab state.
        if (!IsSpawned)
            return;

        SetPresentationActive(IsOwner);
    }

    private void SetPresentationActive(bool active)
    {
        // Reject scene/other-player references. Never disable the player root
        // or CameraPos hierarchy: input ownership is handled separately.
        if (playerCamera != null && playerCamera.transform.IsChildOf(transform))
        {
            if (active && !playerCamera.gameObject.activeSelf)
                playerCamera.gameObject.SetActive(true);

            playerCamera.enabled = active;
        }

        if (audioListener != null && audioListener.transform.IsChildOf(transform))
        {
            audioListener.enabled = active;
        }
    }
}
