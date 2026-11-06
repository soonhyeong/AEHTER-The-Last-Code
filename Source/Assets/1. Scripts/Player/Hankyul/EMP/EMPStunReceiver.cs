using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EMPStunReceiver : MonoBehaviour, IEMPStunnable
{
    [Header("Targets")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private MonoBehaviour[] behavioursToDisable;
    [SerializeField] private GameObject empVfxObject;

    [Header("Options")]
    [SerializeField] private bool stopNavMeshAgent = true;
    [SerializeField] private bool freezeAnimator = true;
    [SerializeField] private bool resetVelocityOnStun = true;
    [SerializeField] private Rigidbody[] rigidbodiesToZero;

    private Coroutine empRoutine;
    private bool isEMPActive;
    private bool cachedAgentStopped;
    private float cachedAnimatorSpeed = 1f;
    private NavMeshAgent stunnedAgent;
    private Animator stunnedAnimator;
    private MonoBehaviour[] stunnedBehaviours;
    private bool[] cachedBehaviourEnabled;

    public bool IsEMPActive => isEMPActive;

    public void ApplyEMP(float duration)
    {
        if (!gameObject.activeInHierarchy) return;

        if (empRoutine != null)
            StopCoroutine(empRoutine);

        empRoutine = StartCoroutine(EMPProcess(duration));
    }

    private IEnumerator EMPProcess(float duration)
    {
        EnterEMPState();

        yield return new WaitForSeconds(duration);

        ExitEMPState();
        empRoutine = null;
    }

    private void EnterEMPState()
    {
        // Reapplying EMP extends the timer, not the original-state snapshot.
        if (isEMPActive) return;
        isEMPActive = true;

        if (empVfxObject != null)
            empVfxObject.SetActive(true);

        if (stopNavMeshAgent && agent != null && agent.isOnNavMesh)
        {
            stunnedAgent = agent;
            cachedAgentStopped = agent.isStopped;
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (freezeAnimator && animator != null)
        {
            stunnedAnimator = animator;
            cachedAnimatorSpeed = animator.speed;
            animator.speed = 0f;
        }

        if (resetVelocityOnStun && rigidbodiesToZero != null)
        {
            foreach (Rigidbody rb in rigidbodiesToZero)
            {
                if (rb == null) continue;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (behavioursToDisable != null)
        {
            stunnedBehaviours = (MonoBehaviour[])behavioursToDisable.Clone();
            cachedBehaviourEnabled = new bool[stunnedBehaviours.Length];
            // Capture every value before disabling any target (including duplicate entries).
            for (int i = 0; i < stunnedBehaviours.Length; i++)
            {
                MonoBehaviour behaviour = stunnedBehaviours[i];
                if (behaviour != null) cachedBehaviourEnabled[i] = behaviour.enabled;
            }
            foreach (MonoBehaviour behaviour in stunnedBehaviours)
            {
                if (behaviour == null) continue;
                if (behaviour == this) continue;
                behaviour.enabled = false;
            }
        }
    }

    private void ExitEMPState()
    {
        if (!isEMPActive) return;
        // Clear before callbacks triggered by restoring a target's enabled state.
        isEMPActive = false;

        if (stunnedBehaviours != null)
        {
            for (int i = 0; i < stunnedBehaviours.Length; i++)
            {
                MonoBehaviour behaviour = stunnedBehaviours[i];
                if (behaviour == null) continue;
                if (behaviour == this) continue;
                behaviour.enabled = cachedBehaviourEnabled[i];
            }
        }

        if (stunnedAnimator != null)
            stunnedAnimator.speed = cachedAnimatorSpeed;

        if (stunnedAgent != null && stunnedAgent.isOnNavMesh)
            stunnedAgent.isStopped = cachedAgentStopped;

        if (empVfxObject != null)
            empVfxObject.SetActive(false);

        stunnedAgent = null;
        stunnedAnimator = null;
        stunnedBehaviours = null;
        cachedBehaviourEnabled = null;
    }

    private void OnDisable()
    {
        if (empRoutine != null)
        {
            StopCoroutine(empRoutine);
            empRoutine = null;
        }
        ExitEMPState();
    }
}
