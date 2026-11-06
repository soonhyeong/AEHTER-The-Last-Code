using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Robots : MonoBehaviour, IObserverAlertReceiver
{
    private enum MoveState { Idle = 0, Walk = 1, WalkToStop = 2, Run = 3 }
    private enum AIState { Patrol, AlertPause, ChaseToRange, Attack, ChaseWide, ObserverInvestigate, Dead }

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform player;
    [SerializeField] private RobotCombat combat;
    [SerializeField] private RobotHealth health;

    [Header("Alert Mark (WorldSpace Canvas or GameObject)")]
    [SerializeField] private GameObject alertMark;
    [SerializeField] private float alertMarkDuration = 1f;

    [Header("Patrol (3s walk -> walkToStop -> 1s idle)")]
    [SerializeField] private float walkSpeed = 1.8f;
    [SerializeField] private float runSpeed = 3.8f;
    [SerializeField] private float walkDuration = 3f;
    [SerializeField] private float walkToStopDuration = 0.8f;
    [SerializeField] private float stopDuration = 1f;

    [Header("Random Patrol Nav")]
    [SerializeField] private int patrolRandomTryCount = 12;
    [SerializeField] private float minPatrolDistance = 2.5f;
    [SerializeField] private float maxPatrolDistance = 7f;
    [Tooltip("NavMesh.SamplePosition search radius")]
    [SerializeField] private float navSampleRadius = 2f;
    [Tooltip("Distance to consider patrol point reached")]
    [SerializeField] private float patrolReachThreshold = 0.8f;
    [Tooltip("Avoid choosing directions too far behind the robot")]
    [SerializeField] private bool avoidBackwardTooMuch = true;
    [SerializeField, Range(-1f, 1f)] private float backwardDotLimit = -0.2f;
    [Tooltip("Fallback straight-line patrol distance")]
    [SerializeField] private float patrolStepDistance = 6f;
    [Tooltip("Layers used as real floor when no NavMesh is available. Enemy and Player layers are intentionally ignored.")]
    [SerializeField] private LayerMask groundMask;

    [Header("Individual Variation")]
    [SerializeField] private bool randomizeIndividualBehavior = true;
    [SerializeField] private Vector2 initialPatrolDelayRange = new Vector2(0f, 2.5f);
    [SerializeField] private Vector2 moveSpeedMultiplierRange = new Vector2(0.85f, 1.15f);
    [SerializeField] private Vector2 behaviorTimeMultiplierRange = new Vector2(0.75f, 1.3f);
    [SerializeField] private Vector2 turnSpeedMultiplierRange = new Vector2(0.8f, 1.2f);
    [SerializeField] private Vector2 animationSpeedRange = new Vector2(0.92f, 1.08f);

    [Header("Detection (Sight)")]
    [SerializeField] private float sightRange = 18f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private float sightCheckInterval = 0.1f;
    [SerializeField] private LayerMask obstacleMask;

    [Header("Combat")]
    [SerializeField] private float fireRange = 12f;
    [SerializeField] private float wideChaseRange = 30f;
    [SerializeField] private float giveUpRange = 35f;
    [SerializeField] private float alertPauseTime = 1f;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireInterval = 0.5f;
    [SerializeField] private int burstShotCount = 3;
    [SerializeField] private float burstCooldown = 2f;
    [SerializeField] private bool startWithBurstCooldown = true;
    [SerializeField] private float bulletSpeed = 18f;
    [SerializeField] private int bulletDamage = 10;
    [SerializeField] private float bulletLifeTime = 4f;
    [SerializeField] private bool fireByAnimationEvent = true;
    [SerializeField] private float fireEventFallbackDelay = 0.25f;
    [SerializeField] private bool useAimingIdleDuringBurstCooldown = true;

    [Header("Observer Alert")]
    [SerializeField] private float observerSearchRadius = 3.5f;
    [SerializeField] private float observerArriveThreshold = 1.1f;
    [SerializeField] private float observerSearchPointInterval = 1.25f;

    [Header("Rotation")]
    [SerializeField] private float rotateSpeed = 540f;

    [Header("HP")]
    [SerializeField] private int maxHP = 100;
    [SerializeField] private float deathDespawnDelay = 2f;

    private bool isDead;
    private bool deathTriggered;

    private AIState aiState = AIState.Patrol;
    private MoveState moveState = MoveState.Idle;

    private float patrolTimer;
    private float stateTimer;
    private float initialPatrolDelay;
    private float initialAnimationPhase;

    private Vector3 patrolDirection;
    private Vector3 currentPatrolTarget;
    private bool hasPatrolTarget;
    private Vector3 attackLockPos;

    private Coroutine alertCo;
    private Coroutine sightCo;
    private Vector3 observerAlertCenter;
    private Vector3 observerInvestigateTarget;
    private float observerSearchEndTime;
    private float observerSearchPointTimer;
    private bool hasObserverInvestigateTarget;

    private static readonly int MoveStateHash = Animator.StringToHash("MoveState");
    private static readonly int DieTriggerHash = Animator.StringToHash("Die");

    private bool HasUsableAgent => agent != null && agent.enabled && agent.isOnNavMesh;
    private float fallbackGroundY;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        combat = GetComponent<RobotCombat>();
        health = GetComponent<RobotHealth>();
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (combat == null) combat = GetComponent<RobotCombat>() ?? gameObject.AddComponent<RobotCombat>();
        if (health == null) health = GetComponent<RobotHealth>() ?? gameObject.AddComponent<RobotHealth>();

        if (player == null)
        {
            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }

        if (sightRange <= fireRange)
            sightRange = fireRange + 3f;

        ApplyIndividualVariation();

        if (agent != null)
        {
            agent.speed = walkSpeed;
            agent.acceleration = 999f;
            agent.angularSpeed = 999f;
            agent.stoppingDistance = 0f;
            agent.updateRotation = false;
        }

        if (alertMark != null)
            alertMark.SetActive(false);

        patrolDirection = transform.forward.sqrMagnitude > 0.001f ? transform.forward.normalized : Vector3.forward;
        patrolStepDistance = Mathf.Max(patrolStepDistance, walkSpeed * walkDuration);
        groundMask = ResolveGroundMask(groundMask);
        fallbackGroundY = transform.position.y;

        combat.Initialize(new RobotCombatConfig
        {
            animator = animator,
            owner = transform,
            target = player,
            bulletPrefab = bulletPrefab,
            firePoint = firePoint,
            fireRange = fireRange,
            fireInterval = fireInterval,
            burstShotCount = burstShotCount,
            burstCooldown = burstCooldown,
            startWithBurstCooldown = startWithBurstCooldown,
            bulletSpeed = bulletSpeed,
            bulletDamage = bulletDamage,
            bulletLifeTime = bulletLifeTime,
            fireByAnimationEvent = fireByAnimationEvent,
            fireEventFallbackDelay = fireEventFallbackDelay,
            useAimingIdleDuringBurstCooldown = useAimingIdleDuringBurstCooldown
        });

        health.Configure(maxHP, deathDespawnDelay);
        health.Died += HandleDeath;

        sightCo = StartCoroutine(SightLoop());
        EnterPatrol();
        patrolTimer = -initialPatrolDelay;

        if (animator != null && randomizeIndividualBehavior)
            animator.Play(0, 0, initialAnimationPhase);
    }

    private void ApplyIndividualVariation()
    {
        if (!randomizeIndividualBehavior)
            return;

        initialPatrolDelay = Random.Range(
            Mathf.Min(initialPatrolDelayRange.x, initialPatrolDelayRange.y),
            Mathf.Max(initialPatrolDelayRange.x, initialPatrolDelayRange.y));
        initialAnimationPhase = Random.value;

        float moveMultiplier = Random.Range(
            Mathf.Min(moveSpeedMultiplierRange.x, moveSpeedMultiplierRange.y),
            Mathf.Max(moveSpeedMultiplierRange.x, moveSpeedMultiplierRange.y));
        float timeMultiplier = Random.Range(
            Mathf.Min(behaviorTimeMultiplierRange.x, behaviorTimeMultiplierRange.y),
            Mathf.Max(behaviorTimeMultiplierRange.x, behaviorTimeMultiplierRange.y));

        walkSpeed *= moveMultiplier;
        runSpeed *= Mathf.Clamp(moveMultiplier + Random.Range(-0.08f, 0.08f), 0.75f, 1.25f);
        walkDuration *= timeMultiplier;
        walkToStopDuration *= Random.Range(0.85f, 1.15f);
        stopDuration *= Random.Range(
            Mathf.Min(behaviorTimeMultiplierRange.x, behaviorTimeMultiplierRange.y),
            Mathf.Max(behaviorTimeMultiplierRange.x, behaviorTimeMultiplierRange.y));
        alertPauseTime *= Random.Range(0.8f, 1.2f);
        fireInterval *= Random.Range(0.85f, 1.15f);
        burstCooldown *= Random.Range(0.8f, 1.25f);
        rotateSpeed *= Random.Range(
            Mathf.Min(turnSpeedMultiplierRange.x, turnSpeedMultiplierRange.y),
            Mathf.Max(turnSpeedMultiplierRange.x, turnSpeedMultiplierRange.y));

        if (animator != null)
        {
            animator.speed = Random.Range(
                Mathf.Min(animationSpeedRange.x, animationSpeedRange.y),
                Mathf.Max(animationSpeedRange.x, animationSpeedRange.y));
        }

        if (agent != null)
            agent.avoidancePriority = Random.Range(30, 71);
    }

    private void OnDisable()
    {
        if (health != null)
            health.Died -= HandleDeath;

        if (sightCo != null) StopCoroutine(sightCo);
        if (alertCo != null) StopCoroutine(alertCo);
    }

    private void Update()
    {
        if (isDead) return;

        combat.SetTarget(player);

        if (player == null)
        {
            UpdatePatrolOnly();
            return;
        }

        switch (aiState)
        {
            case AIState.Patrol:
                UpdatePatrolOnly();
                break;
            case AIState.AlertPause:
                UpdateAlertPause();
                break;
            case AIState.ChaseToRange:
                UpdateChaseToRange();
                break;
            case AIState.Attack:
                UpdateAttack();
                break;
            case AIState.ChaseWide:
                UpdateChaseWide();
                break;
            case AIState.ObserverInvestigate:
                UpdateObserverInvestigate();
                break;
        }
    }

    private IEnumerator SightLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(sightCheckInterval);

        while (true)
        {
            if (!isDead && player != null && aiState == AIState.Patrol)
            {
                if (CanSeePlayerSight(sightRange))
                    EnterAlertPause();
            }

            yield return wait;
        }
    }

    private void EnterPatrol()
    {
        aiState = AIState.Patrol;
        combat.StopCombatAnimation();

        SetAgentStopped(false);
        SetAgentSpeed(walkSpeed);

        patrolTimer = 0f;
        hasPatrolTarget = false;
        SetMoveState(MoveState.Idle);
    }

    private void UpdatePatrolOnly()
    {
        patrolTimer += Time.deltaTime;

        switch (moveState)
        {
            case MoveState.Idle:
                if (patrolTimer >= stopDuration)
                    BeginPatrolWalk();
                break;
            case MoveState.Walk:
                UpdatePatrolWalk();
                if (patrolTimer >= walkDuration)
                    BeginWalkToStop();
                break;
            case MoveState.WalkToStop:
                SetAgentStopped(true);
                if (patrolTimer >= walkToStopDuration)
                    BeginIdle();
                break;
            case MoveState.Run:
                BeginIdle();
                break;
        }
    }

    private void BeginPatrolWalk()
    {
        patrolTimer = 0f;
        SetMoveState(MoveState.Walk);

        SetAgentStopped(false);
        SetAgentSpeed(walkSpeed);

        if (TryGetRandomPatrolPoint(out Vector3 patrolPos))
        {
            currentPatrolTarget = patrolPos;
            hasPatrolTarget = true;

            Vector3 dir = currentPatrolTarget - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
            {
                patrolDirection = dir.normalized;
                RotateTowards(patrolDirection, rotateSpeed);
            }

            SetMoveDestination(currentPatrolTarget, walkSpeed, patrolReachThreshold);
        }
        else
        {
            hasPatrolTarget = false;
            ResetAgentPath();
            SetAgentStopped(true);
            BeginIdle();
        }
    }

    private void UpdatePatrolWalk()
    {
        if (!hasPatrolTarget) return;

        Vector3 toTarget = currentPatrolTarget - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > 0.001f)
            RotateTowards(toTarget.normalized, rotateSpeed);

        MoveWithoutNavMeshIfNeeded(currentPatrolTarget, walkSpeed, patrolReachThreshold);

        if (toTarget.magnitude <= patrolReachThreshold)
        {
            hasPatrolTarget = false;
            BeginWalkToStop();
            return;
        }

        if (HasUsableAgent && !agent.pathPending && (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete))
        {
            hasPatrolTarget = false;
            BeginIdle();
        }
    }

    private void BeginWalkToStop()
    {
        patrolTimer = 0f;
        SetMoveState(MoveState.WalkToStop);

        ResetAgentPath();
        SetAgentStopped(true);
    }

    private void BeginIdle()
    {
        patrolTimer = 0f;
        SetMoveState(MoveState.Idle);

        ResetAgentPath();
        SetAgentStopped(true);
    }

    private bool TryGetRandomPatrolPoint(out Vector3 result)
    {
        Vector3 origin = transform.position;

        for (int i = 0; i < patrolRandomTryCount; i++)
        {
            Vector2 random2D = Random.insideUnitCircle.normalized;
            if (random2D.sqrMagnitude < 0.001f) continue;

            Vector3 dir = new Vector3(random2D.x, 0f, random2D.y);

            if (avoidBackwardTooMuch)
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;

                if (forward.sqrMagnitude > 0.001f)
                {
                    forward.Normalize();
                    if (Vector3.Dot(forward, dir) < backwardDotLimit)
                        continue;
                }
            }

            float distance = Random.Range(minPatrolDistance, maxPatrolDistance);
            Vector3 desired = origin + dir * distance;

            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
                continue;

            Vector3 flat = hit.position - origin;
            flat.y = 0f;
            if (flat.magnitude < minPatrolDistance * 0.7f)
                continue;

            if (HasUsableAgent)
            {
                NavMeshPath path = new NavMeshPath();
                if (!agent.CalculatePath(hit.position, path))
                    continue;

                if (path.status != NavMeshPathStatus.PathComplete)
                    continue;
            }

            result = hit.position;
            return true;
        }

        for (int i = 0; i < 5; i++)
        {
            Vector3 dir = GetNonZeroRandomDirectionXZ();
            Vector3 desired = origin + dir * patrolStepDistance;

            if (TryGetNavPosition(desired, navSampleRadius, out Vector3 navPos))
            {
                if (!HasUsableAgent)
                {
                    result = navPos;
                    return true;
                }

                NavMeshPath path = new NavMeshPath();
                if (agent.CalculatePath(navPos, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    result = navPos;
                    return true;
                }
            }
        }

        result = origin;
        return false;
    }

    private void EnterAlertPause()
    {
        aiState = AIState.AlertPause;
        stateTimer = 0f;

        ResetAgentPath();
        SetAgentStopped(true);

        combat.StopCombatAnimation();
        SetMoveState(MoveState.Idle);
        ShowAlertMarkOnce();
    }

    private void UpdateAlertPause()
    {
        stateTimer += Time.deltaTime;
        FacePlayer(rotateSpeed);

        if (stateTimer >= alertPauseTime)
        {
            if (CanSeePlayerSight(sightRange))
                EnterChaseToRange();
            else
                EnterPatrol();
        }
    }

    private void ShowAlertMarkOnce()
    {
        if (alertMark == null) return;

        if (alertCo != null) StopCoroutine(alertCo);
        alertCo = StartCoroutine(AlertRoutine());
    }

    private IEnumerator AlertRoutine()
    {
        alertMark.SetActive(true);
        yield return new WaitForSeconds(alertMarkDuration);
        alertMark.SetActive(false);
        alertCo = null;
    }

    private void EnterChaseToRange()
    {
        aiState = AIState.ChaseToRange;

        SetAgentStopped(false);
        SetAgentSpeed(runSpeed);

        combat.StopCombatAnimation();
        SetMoveState(MoveState.Run);
    }

    private void UpdateChaseToRange()
    {
        float dist = DistanceToPlayerXZ();

        if (dist <= fireRange)
        {
            EnterAttack();
            return;
        }

        if (!CanSeePlayerSight(sightRange))
        {
            EnterChaseWide();
            return;
        }

        SetMoveDestination(player.position, runSpeed, fireRange);
        FacePlayer(rotateSpeed);
    }

    private void EnterAttack()
    {
        aiState = AIState.Attack;

        ResetAgentPath();
        SetAgentStopped(true);
        if (HasUsableAgent)
            agent.velocity = Vector3.zero;

        attackLockPos = transform.position;
        SetMoveState(MoveState.Idle);
        combat.EnterAttack();
    }

    private void UpdateAttack()
    {
        transform.position = attackLockPos;
        FacePlayer(rotateSpeed);

        if (combat.TickAttack(DistanceToPlayerXZ()))
            EnterChaseWide();
    }

    private void EnterChaseWide()
    {
        aiState = AIState.ChaseWide;

        combat.StopCombatAnimation();
        SetMoveState(MoveState.Run);

        SetAgentStopped(false);
        SetAgentSpeed(runSpeed);
    }

    private void EnterObserverInvestigate(Vector3 alertPosition, float searchDuration)
    {
        if (isDead) return;

        aiState = AIState.ObserverInvestigate;

        combat.StopCombatAnimation();
        SetMoveState(MoveState.Run);

        SetAgentStopped(false);
        SetAgentSpeed(runSpeed);

        observerAlertCenter = alertPosition;
        observerInvestigateTarget = alertPosition;
        observerSearchEndTime = Time.time + Mathf.Max(0.5f, searchDuration);
        observerSearchPointTimer = 0f;
        hasObserverInvestigateTarget = true;

        SetMoveDestination(observerInvestigateTarget, runSpeed, observerArriveThreshold);
        ShowAlertMarkOnce();
    }

    private void UpdateChaseWide()
    {
        float dist = DistanceToPlayerXZ();

        if (dist > giveUpRange)
        {
            EnterPatrol();
            return;
        }

        if (dist <= fireRange)
        {
            EnterAttack();
            return;
        }

        SetMoveDestination(player.position, runSpeed, fireRange);
        FacePlayer(rotateSpeed);
    }

    private void UpdateObserverInvestigate()
    {
        if (CanSeePlayerSight(sightRange))
        {
            EnterAlertPause();
            return;
        }

        if (Time.time >= observerSearchEndTime)
        {
            EnterPatrol();
            return;
        }

        if (hasObserverInvestigateTarget)
        {
            Vector3 toTarget = observerInvestigateTarget - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
                RotateTowards(toTarget.normalized, rotateSpeed);

            if (HasReachedDestination(observerArriveThreshold))
            {
                hasObserverInvestigateTarget = false;
                observerSearchPointTimer = 0f;
                ResetAgentPath();
                SetAgentStopped(true);
                SetMoveState(MoveState.Idle);
            }

            return;
        }

        observerSearchPointTimer += Time.deltaTime;
        if (observerSearchPointTimer < observerSearchPointInterval)
        {
            Vector3 lookCenter = observerAlertCenter - transform.position;
            lookCenter.y = 0f;
            if (lookCenter.sqrMagnitude > 0.001f)
                RotateTowards(lookCenter.normalized, rotateSpeed);

            return;
        }

        observerSearchPointTimer = 0f;

        if (TryGetObserverSearchPoint(out observerInvestigateTarget))
        {
            hasObserverInvestigateTarget = true;
            SetAgentStopped(false);
            SetMoveDestination(observerInvestigateTarget, runSpeed, observerArriveThreshold);
            SetMoveState(MoveState.Run);
        }
    }

    private bool CanSeePlayerSight(float maxDist)
    {
        if (player == null) return false;

        Vector3 origin = transform.position + Vector3.up * 1.5f;
        Vector3 target = player.position + Vector3.up * 1.0f;
        Vector3 toTarget = target - origin;
        float dist = toTarget.magnitude;
        if (dist > maxDist) return false;

        Vector3 dir = toTarget / Mathf.Max(dist, 0.0001f);
        Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
        if (flatDir.sqrMagnitude > 0.0001f)
        {
            float angle = Vector3.Angle(transform.forward, flatDir.normalized);
            if (angle > viewAngle * 0.5f) return false;
        }

        return !Physics.Raycast(origin, dir, dist, obstacleMask);
    }

    private float DistanceToPlayerXZ()
    {
        if (player == null) return float.MaxValue;

        Vector3 a = transform.position;
        Vector3 b = player.position;
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void FacePlayer(float rotSpeed)
    {
        if (player == null) return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return;

        RotateTowards(toPlayer.normalized, rotSpeed);
    }

    private void RotateTowards(Vector3 dir, float rotSpeed)
    {
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotSpeed * Time.deltaTime);
    }

    private Vector3 GetNonZeroRandomDirectionXZ()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 dir = Random.insideUnitSphere;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.2f)
                return dir.normalized;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
    }

    private bool TryGetNavPosition(Vector3 desired, float radius, out Vector3 navPos)
    {
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, radius, NavMesh.AllAreas))
        {
            navPos = hit.position;
            return true;
        }

        navPos = desired;
        return false;
    }

    private void SetAgentSpeed(float speed)
    {
        if (agent != null && agent.enabled)
            agent.speed = speed;
    }

    private void SetAgentStopped(bool stopped)
    {
        if (HasUsableAgent)
            agent.isStopped = stopped;
    }

    private void ResetAgentPath()
    {
        if (HasUsableAgent)
            agent.ResetPath();
    }

    private void SetMoveDestination(Vector3 destination, float speed, float stoppingDistance)
    {
        if (HasUsableAgent)
        {
            agent.isStopped = false;
            agent.speed = speed;
            agent.stoppingDistance = stoppingDistance;
            agent.SetDestination(destination);
            return;
        }

        MoveDirectlyToward(destination, speed, stoppingDistance);
    }

    private void MoveWithoutNavMeshIfNeeded(Vector3 destination, float speed, float stoppingDistance)
    {
        if (HasUsableAgent)
            return;

        MoveDirectlyToward(destination, speed, stoppingDistance);
    }

    private void MoveDirectlyToward(Vector3 destination, float speed, float stoppingDistance)
    {
        Vector3 current = transform.position;
        Vector3 target = destination;
        current.y = 0f;
        target.y = 0f;

        Vector3 toTarget = target - current;
        float distance = toTarget.magnitude;
        if (distance <= stoppingDistance || distance <= 0.001f)
            return;

        Vector3 direction = toTarget / distance;
        RotateTowards(direction, rotateSpeed);

        Vector3 nextPosition = transform.position + direction * (speed * Time.deltaTime);
        transform.position = SnapRuntimePositionToGround(nextPosition);
    }

    private Vector3 SnapRuntimePositionToGround(Vector3 position)
    {
        Vector3 rayStart = position + Vector3.up * 4f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 12f, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (Mathf.Abs(hit.point.y - fallbackGroundY) <= 1.5f)
            {
                position.y = hit.point.y;
                fallbackGroundY = hit.point.y;
                return position;
            }
        }

        position.y = fallbackGroundY;

        return position;
    }

    private static LayerMask ResolveGroundMask(LayerMask configuredMask)
    {
        if (configuredMask.value != 0)
            return configuredMask;

        int mask = LayerMask.GetMask("floor", "Default", "ElevatorInterior");
        if (mask == 0)
            mask = ~LayerMask.GetMask("Player", "Enemy", "UI", "Ignore Raycast", "Water");

        return mask;
    }

    private bool TryGetObserverSearchPoint(out Vector3 result)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset2D = Random.insideUnitCircle * observerSearchRadius;
            Vector3 desired = observerAlertCenter + new Vector3(offset2D.x, 0f, offset2D.y);

            if (!TryGetNavPosition(desired, navSampleRadius, out Vector3 navPos))
                continue;

            if (HasUsableAgent)
            {
                NavMeshPath path = new NavMeshPath();
                if (!agent.CalculatePath(navPos, path) || path.status != NavMeshPathStatus.PathComplete)
                    continue;
            }

            result = navPos;
            return true;
        }

        result = observerAlertCenter;
        return false;
    }

    private bool HasReachedDestination(float threshold)
    {
        if (!HasUsableAgent)
            return Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(observerInvestigateTarget.x, 0f, observerInvestigateTarget.z)) <= threshold;

        if (agent.pathPending) return false;
        if (!agent.hasPath) return true;
        if (agent.remainingDistance > threshold) return false;

        return agent.velocity.sqrMagnitude <= 0.01f;
    }

    public void OnObserverAlert(Vector3 alertPosition, float searchDuration)
    {
        if (!isActiveAndEnabled || isDead) return;
        if (player != null && CanSeePlayerSight(sightRange)) return;

        if (!TryGetNavPosition(alertPosition, navSampleRadius + observerSearchRadius, out Vector3 navPos))
            return;

        EnterObserverInvestigate(navPos, searchDuration);
    }

    public void OnFireAnimationEvent()
    {
        if (!isDead)
            combat.OnFireAnimationEvent(DistanceToPlayerXZ());
    }

    private void HandleDeath(RobotHealth robotHealth)
    {
        if (deathTriggered) return;

        deathTriggered = true;
        isDead = true;
        aiState = AIState.Dead;

        combat.StopCombatAnimation();
        SetMoveState(MoveState.Idle);

        if (agent != null)
        {
            ResetAgentPath();
            SetAgentStopped(true);
            agent.enabled = false;
        }

        if (alertMark != null)
            alertMark.SetActive(false);

        if (alertCo != null) StopCoroutine(alertCo);
        if (sightCo != null) StopCoroutine(sightCo);

        if (animator != null)
        {
            animator.ResetTrigger(DieTriggerHash);
            animator.SetTrigger(DieTriggerHash);
        }

        StartCoroutine(DespawnAfter(robotHealth.DeathDespawnDelay));
    }

    private IEnumerator DespawnAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    private void SetMoveState(MoveState state)
    {
        moveState = state;
        if (animator != null)
            animator.SetInteger(MoveStateHash, (int)state);
    }
}
