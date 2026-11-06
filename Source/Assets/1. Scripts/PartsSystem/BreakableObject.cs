using UnityEngine;

public class BreakableObject : MonoBehaviour, IBreakable
{
    [SerializeField] private float health = 1f;
    [SerializeField] private bool disableInsteadOfDestroy = true;

    public void Break()
    {
        if (disableInsteadOfDestroy)
            gameObject.SetActive(false);
        else
            Destroy(gameObject);
    }

    public void TakeBreakDamage(float force)
    {
        health -= Mathf.Max(0.1f, force);
        if (health <= 0f)
            Break();
    }
}
