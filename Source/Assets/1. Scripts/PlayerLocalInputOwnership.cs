using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Unity.Netcode;

// Input permission only. This does not assign combat authority or change modal/movement flags.
public static class PlayerLocalInputOwnership
{
    public static bool CanConsumeLocalInput(Component anchor)
    {
        return anchor != null && CanConsumeLocalInput(anchor.gameObject);
    }

    public static bool CanConsumeLocalInput(GameObject anchor)
    {
        if (anchor == null) return false;
        Transform player = null;
        NetworkObject outermost = null;
        for (Transform t = anchor.transform; t != null; t = t.parent)
        {
            // Prefer the movement root over a nested gun's NetworkObject/PlayerInput.
            if (t.GetComponent<Move_Hankyul>() != null || t.GetComponent<Move_Yunseo>() != null)
            { player = t; break; }
            var networkObject = t.GetComponent<NetworkObject>();
            if (networkObject != null) outermost = networkObject;
        }
        if (player == null)
        {
            for (Transform t = anchor.transform; t != null; t = t.parent)
                if (t.GetComponent<PlayerInput>() != null) player = t;
        }

        NetworkObject owner = player != null ? player.GetComponentInParent<NetworkObject>() : outermost;
        // Authored scene Players and standalone prefab instances are not remote Players.
        if (owner == null || !owner.IsSpawned) return true;
        NetworkManager manager = owner.NetworkManager;
        // A spawned object without a live manager is not a standalone exception.
        return manager != null && manager.IsListening && owner.IsOwner;
    }

    // Per consumer, not global: callback/Update/FixedUpdate order cannot open another reader's gate.
    public sealed class InputGate
    {
        private bool waitingForRelease;
        private int releaseFrame = -1;

        public bool CanRead(Component anchor)
        {
            if (!CanConsumeLocalInput(anchor))
            {
                waitingForRelease = true;
                releaseFrame = -1;
                return false; // Never inspect devices on a remote Player.
            }
            if (waitingForRelease)
            {
                if (HasHeldControl()) return false;
                waitingForRelease = false;
                releaseFrame = Time.frameCount;
                return false; // Do not reinterpret a release/canceled callback as a new input.
            }
            return releaseFrame != Time.frameCount;
        }

        private static bool HasHeldControl()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.isPressed) return true;
            if (Mouse.current != null && (Mouse.current.leftButton.isPressed
                || Mouse.current.rightButton.isPressed || Mouse.current.middleButton.isPressed)) return true;
            foreach (Gamepad gamepad in Gamepad.all)
                foreach (var control in gamepad.allControls)
                {
                    if (control is ButtonControl button && button.isPressed) return true;
                    // ReadValue already applies the device's deadzone; require its actual neutral value.
                    if (control is StickControl stick && stick.ReadValue().sqrMagnitude > 0f) return true;
                }
            return false;
        }
    }
}
