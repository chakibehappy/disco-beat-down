using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

public class EnemyScript : MonoBehaviour
{
    private Animator animator;
    private CombatScript playerCombat;
    private EnemyManager enemyManager;
    private EnemyDetection enemyDetection;
    private CharacterController characterController;

    [Header("Stats")]
    public int health = 3;
    private Vector3 moveDirection;
    private float currentMoveSpeed = 0f;

    [Header("AI Intelligence")]
    [Tooltip("1 = Stupid (Stands around), 10 = Tactical genius (Gangs up, fast reactions)")]
    [Range(1, 10)]
    public int intelligenceLevel = 5;

    [Header("Unstable Animations (Use Exact Names)")]
    public string[] idleAnimations = { "Idle1", "CapoeiraIdle" };
    public string[] walkAnimations = { "Walk1", "ZombieWalk" };
    public string[] runAnimations = { "Run1", "NarutoRun" };

    [Header("Unstable Strafing & Retreating")]
    public string[] strafeLeftAnimations = { "StrafeLeft", "DrunkStrafeLeft" };
    public string[] strafeRightAnimations = { "StrafeRight", "DrunkStrafeRight" };
    public string[] runStrafeLeftAnimations = { "RunStrafeLeft", "FastSlideLeft" };
    public string[] runStrafeRightAnimations = { "RunStrafeRight", "FastSlideRight" };
    public string[] walkBackAnimations = { "WalkBack1", "GirlyRetreat", "Moonwalk" };

    [Header("Combat Animations")]
    public string[] attackAnimations = { "Punch1", "BoxingPunch", "SpinKick" };
    public string[] hitAnimations = { "Hit1", "Hit2" };
    public string[] deathAnimations = { "Death1", "Death2" };

    [Header("Disrespect Animations")]
    public string[] tauntAnimations = { "SalsaDance", "MockingLaugh", "ChickenDance" };

    [Header("States")]
    [SerializeField] private bool isPreparingAttack;
    [SerializeField] private bool isMoving;
    [SerializeField] private bool isRetreating;
    [SerializeField] private bool isLockedTarget;
    [SerializeField] private bool isStunned;
    [SerializeField] private bool isWaiting = true;

    private bool wantsToTaunt = false;
    private string currentMoveState = "Idle";

    [Header("Combat Hitboxes")]
    public Collider[] hitColliders;

    [Header("Polish")]
    [SerializeField] private ParticleSystem counterParticle;

    private Coroutine PrepareAttackCoroutine;
    private Coroutine RetreatCoroutine;
    private Coroutine DamageCoroutine;
    private Coroutine MovementCoroutine;
    private Coroutine FinishAttackCoroutine;

    public UnityEvent<EnemyScript> OnDamage;
    public UnityEvent<EnemyScript> OnStopMoving;
    public UnityEvent<EnemyScript> OnRetreat;

    void Start()
    {
        enemyManager = GetComponentInParent<EnemyManager>();
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        playerCombat = FindObjectOfType<CombatScript>();
        enemyDetection = playerCombat.GetComponentInChildren<EnemyDetection>();

        playerCombat.OnHit.AddListener((x) => OnPlayerHit(x));
        playerCombat.OnCounterAttack.AddListener((x) => OnPlayerCounter(x));
        playerCombat.OnTrajectory.AddListener((x) => OnPlayerTrajectory(x));

        ToggleHitColliders(false);
        MovementCoroutine = StartCoroutine(EnemyMovement());
    }

    public void ToggleHitColliders(bool state)
    {
        if (hitColliders == null || hitColliders.Length == 0) return;
        foreach (Collider col in hitColliders)
        {
            if (col != null) col.enabled = state;
        }
    }

    public void PlayRandomAnimation(string[] animationArray)
    {
        if (animationArray == null || animationArray.Length == 0) return;
        int randomIndex = Random.Range(0, animationArray.Length);
        string animName = animationArray[randomIndex];
        animator.CrossFadeInFixedTime(animName, 0.2f);
    }

    private void ChangeMovementState(string newState, string[] animArray)
    {
        if (currentMoveState != newState)
        {
            currentMoveState = newState;
            PlayRandomAnimation(animArray);
        }
    }

    private float DistanceToPlayerFlat()
    {
        Vector3 playerPos = playerCombat.transform.position;
        playerPos.y = transform.position.y;
        return Vector3.Distance(transform.position, playerPos);
    }

    IEnumerator EnemyMovement()
    {
        yield return new WaitUntil(() => isWaiting == true);
        isWaiting = false;

        int actionChance = Random.Range(0, 100);
        int attackProbability = intelligenceLevel * 4;

        if (actionChance < attackProbability)
        {
            SetAttack();
        }
        else if (actionChance < 80)
        {
            int randomDir = Random.Range(0, 2);
            moveDirection = randomDir == 1 ? Vector3.right : Vector3.left;
            isMoving = true;
        }
        else
        {
            StopMoving();
            ChangeMovementState("Idle", idleAnimations); // FIX: Ensures they never stand still in a run pose!
        }

        float waitTime = Mathf.Lerp(3f, 0.3f, intelligenceLevel / 10f);
        yield return new WaitForSeconds(waitTime);

        isWaiting = true;
        MovementCoroutine = StartCoroutine(EnemyMovement());
    }

    void Update()
    {
        if (currentMoveState != "Taunt" && currentMoveState != "Hit" && currentMoveState != "Death")
        {
            Vector3 targetPos = new Vector3(playerCombat.transform.position.x, transform.position.y, playerCombat.transform.position.z);
            Vector3 dirToTarget = (targetPos - transform.position).normalized;

            if (dirToTarget != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(dirToTarget);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }
        }
        MoveEnemy(moveDirection);
    }

    void OnPlayerHit(EnemyScript target)
    {
        if (target == this)
        {
            StopEnemyCoroutines();
            wantsToTaunt = false;

            enemyDetection.SetCurrentTarget(null);
            isLockedTarget = false;
            OnDamage.Invoke(this);

            health--;

            if (health <= 0)
            {
                Death();
                return;
            }

            ChangeMovementState("Hit", hitAnimations);
            transform.DOMove(transform.position - (transform.forward / 2), .3f).SetDelay(.1f);
            StopMoving();

            DamageCoroutine = StartCoroutine(HitCoroutine());
        }
    }

    IEnumerator HitCoroutine()
    {
        isStunned = true;

        // TRUE DYNAMIC LENGTH FIX: Safely reads the currently playing Animator State
        yield return new WaitForSeconds(0.1f);
        AnimatorStateInfo stateInfo = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);

        yield return new WaitForSeconds(Mathf.Max(0.1f, stateInfo.length - 0.1f));

        isStunned = false;
        ChangeMovementState("Idle", idleAnimations); // FIX: Force idle animation after hit!

        isWaiting = true;
        MovementCoroutine = StartCoroutine(EnemyMovement());
    }

    void OnPlayerCounter(EnemyScript target)
    {
        if (target == this) PrepareAttack(false);
    }

    void OnPlayerTrajectory(EnemyScript target)
    {
        if (target == this)
        {
            StopEnemyCoroutines();
            isLockedTarget = true;
            PrepareAttack(false);
            StopMoving();
        }
    }

    void Death()
    {
        StopEnemyCoroutines();
        this.enabled = false;
        characterController.enabled = false;

        ChangeMovementState("Death", deathAnimations);
        enemyManager.SetEnemyAvailiability(this, false);
    }

    public void SetRetreat()
    {
        if (isRetreating || currentMoveState == "Taunt" || currentMoveState == "Hit" || currentMoveState == "Death") return;

        StopEnemyCoroutines();
        RetreatCoroutine = StartCoroutine(PrepRetreat());

        IEnumerator PrepRetreat()
        {
            OnRetreat.Invoke(this);
            isRetreating = true;
            moveDirection = -Vector3.forward;
            isMoving = true;

            float retreatTimer = 0f;

            while (DistanceToPlayerFlat() < 4f && retreatTimer < 2.5f)
            {
                retreatTimer += Time.deltaTime;
                yield return null;
            }

            isRetreating = false;
            StopMoving();

            if (wantsToTaunt)
            {
                wantsToTaunt = false;
                ChangeMovementState("Taunt", tauntAnimations);

                // DYNAMIC DANCE LENGTH
                yield return new WaitForSeconds(0.1f);
                AnimatorStateInfo stateInfo = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                yield return new WaitForSeconds(Mathf.Max(0.1f, stateInfo.length - 0.1f));
            }

            ChangeMovementState("Idle", idleAnimations);
            isWaiting = true;
            MovementCoroutine = StartCoroutine(EnemyMovement());
        }
    }

    public void SetAttack()
    {
        isWaiting = false;
        PrepareAttackCoroutine = StartCoroutine(PrepAttack());

        IEnumerator PrepAttack()
        {
            PrepareAttack(true);
            yield return new WaitForSeconds(.2f);
            moveDirection = Vector3.forward;
            isMoving = true;

            float timer = 0;
            while (isPreparingAttack && timer < 3f)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (isPreparingAttack)
            {
                PrepareAttack(false);
                ChangeMovementState("Idle", idleAnimations); // FIX: Don't get stuck running!
            }
        }
    }

    void PrepareAttack(bool active)
    {
        isPreparingAttack = active;

        if (active) counterParticle.Play();
        else
        {
            StopMoving();
            counterParticle.Clear();
            counterParticle.Stop();
            ToggleHitColliders(false);
        }
    }

    void MoveEnemy(Vector3 direction)
    {
        float targetSpeed = 0f;
        Vector3 finalDirection = transform.forward;

        if (isMoving)
        {
            float currentDistance = DistanceToPlayerFlat();

            if (direction == Vector3.forward)
            {
                if (currentDistance > 6.5f) { targetSpeed = 5f; ChangeMovementState("Run", runAnimations); }
                else if (currentDistance < 5.5f) { targetSpeed = 2f; ChangeMovementState("Walk", walkAnimations); }
                else targetSpeed = (currentMoveState == "Run") ? 5f : 2f;
            }
            else if (direction == -Vector3.forward)
            {
                targetSpeed = 2f;
                ChangeMovementState("WalkBack", walkBackAnimations);
            }
            else if (direction == Vector3.right)
            {
                if (currentDistance > 6.5f) { targetSpeed = 4.5f; ChangeMovementState("RunStrafeRight", runStrafeRightAnimations); }
                else if (currentDistance < 5.5f) { targetSpeed = 2f; ChangeMovementState("StrafeRight", strafeRightAnimations); }
                else targetSpeed = (currentMoveState == "RunStrafeRight") ? 4.5f : 2f;
            }
            else if (direction == Vector3.left)
            {
                if (currentDistance > 6.5f) { targetSpeed = 4.5f; ChangeMovementState("RunStrafeLeft", runStrafeLeftAnimations); }
                else if (currentDistance < 5.5f) { targetSpeed = 2f; ChangeMovementState("StrafeLeft", strafeLeftAnimations); }
                else targetSpeed = (currentMoveState == "RunStrafeLeft") ? 4.5f : 2f;
            }

            Vector3 flatPlayerPos = playerCombat.transform.position;
            flatPlayerPos.y = transform.position.y;
            Vector3 dir = (flatPlayerPos - transform.position).normalized;
            Vector3 pDir = Quaternion.AngleAxis(90, Vector3.up) * dir;

            if (direction == Vector3.forward) finalDirection = dir;
            if (direction == Vector3.right || direction == Vector3.left) finalDirection = (pDir * direction.normalized.x);
            if (direction == -Vector3.forward) finalDirection = -transform.forward;

            Vector3 separation = Vector3.zero;
            Collider[] nearby = Physics.OverlapSphere(transform.position, 2.0f);
            foreach (Collider col in nearby)
            {
                if (col.gameObject != this.gameObject && col.GetComponent<EnemyScript>() != null)
                {
                    Vector3 pushAway = transform.position - col.transform.position;
                    pushAway.y = 0;
                    separation += pushAway.normalized;
                }
            }
            if (separation != Vector3.zero)
            {
                finalDirection = (finalDirection + separation.normalized * 1.5f).normalized;
            }
        }
        else
        {
            // IDLE ENFORCEMENT FIX: Guarantee they bounce and idle when they stop moving!
            targetSpeed = 0f;
            if (currentMoveState != "Attack" && currentMoveState != "Taunt" && currentMoveState != "Hit" && currentMoveState != "Death")
            {
                ChangeMovementState("Idle", idleAnimations);
            }
        }

        currentMoveSpeed = Mathf.Lerp(currentMoveSpeed, targetSpeed, 8f * Time.deltaTime);

        Vector3 movementWithGravity = finalDirection * currentMoveSpeed;
        movementWithGravity.y -= 9.81f;

        if (characterController.enabled)
        {
            characterController.Move(movementWithGravity * Time.deltaTime);
        }

        if (isMoving && isPreparingAttack)
        {
            if (DistanceToPlayerFlat() < 2.2f)
            {
                StopMoving();
                if (!playerCombat.isCountering && !playerCombat.isAttackingEnemy)
                    Attack();
                else
                    PrepareAttack(false);
            }
        }
    }

    private void Attack()
    {
        transform.DOMove(transform.position + (transform.forward / 1), .3f);
        ToggleHitColliders(true);

        ChangeMovementState("Attack", attackAnimations);

        if (FinishAttackCoroutine != null) StopCoroutine(FinishAttackCoroutine);
        FinishAttackCoroutine = StartCoroutine(FinishAttackRoutine());
    }

    private IEnumerator FinishAttackRoutine()
    {
        // TRUE DYNAMIC ATTACK LENGTH
        yield return new WaitForSeconds(0.1f);
        AnimatorStateInfo stateInfo = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);

        yield return new WaitForSeconds(Mathf.Max(0.1f, stateInfo.length - 0.15f));

        ToggleHitColliders(false);
        PrepareAttack(false);

        if (health > 0)
        {
            SetRetreat(); // Autonomous Retreat
        }
        else
        {
            ChangeMovementState("Idle", idleAnimations); // Ensure they don't get stuck!
        }
    }

    public void HitEvent()
    {
        if (!playerCombat.isCountering && !playerCombat.isAttackingEnemy)
        {
            playerCombat.DamageEvent();
            wantsToTaunt = true;
        }
    }

    public void StopMoving()
    {
        isMoving = false;
        moveDirection = Vector3.zero;
    }

    void StopEnemyCoroutines()
    {
        PrepareAttack(false);
        ToggleHitColliders(false);

        isRetreating = false;
        isWaiting = false;

        if (RetreatCoroutine != null) StopCoroutine(RetreatCoroutine);
        if (PrepareAttackCoroutine != null) StopCoroutine(PrepareAttackCoroutine);
        if (DamageCoroutine != null) StopCoroutine(DamageCoroutine);
        if (MovementCoroutine != null) StopCoroutine(MovementCoroutine);
        if (FinishAttackCoroutine != null) StopCoroutine(FinishAttackCoroutine);
    }

    #region Public Booleans
    public bool IsAttackable() { return health > 0; }
    public bool IsPreparingAttack() { return isPreparingAttack; }
    public bool IsRetreating() { return isRetreating; }
    public bool IsLockedTarget() { return isLockedTarget; }
    public bool IsStunned() { return isStunned; }
    #endregion
}