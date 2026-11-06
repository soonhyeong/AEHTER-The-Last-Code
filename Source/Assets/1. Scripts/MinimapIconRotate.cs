using UnityEngine;

public class MinimapIconRotate : MonoBehaviour
{
    [SerializeField] private Transform player;
    private readonly LocalPlayerUIBinding localBinding = new LocalPlayerUIBinding();

    private void Start()
    {
        BindPlayer(true);
    }

    private void BindPlayer(bool force = false)
    {
        // Prefab marker represents its own Player (including a remote teammate), not camera focus.
        var ownRoot = LocalPlayerBindingResolver.PlayerRoot(this);
        if (ownRoot != null) { player = LocalPlayerBindingResolver.IsLive(ownRoot) ? ownRoot : null; return; }
        localBinding.Poll(this, player, force);
        player = localBinding.Root;
    }

    private void OnDisable() { player = null; localBinding.Clear(); }

    void LateUpdate()
    {
        BindPlayer();
        if (!player) return;

        transform.rotation = Quaternion.Euler(90f, 0f, -player.eulerAngles.y);
    }
}
