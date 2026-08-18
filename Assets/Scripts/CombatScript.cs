using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using Cinemachine;

public class CombatScript : MonoBehaviour
{
    private EnemyManager enemyManager;
    private EnemyDetection enemyDetection;
    private MovementInput movementInput;
    private Animator animator;
    private CinemachineImpulseSource impulseSource;

    [Header("Target")]
    private EnemyScript lockedTarget;

    [Header("Combat Settings")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private float comboResetTime = 1.2f;

    [Header("States")]
    public bool isAttackingEnemy = false;
    public bool isCountering = false;

    // Combo Tracking
    private int comboStep = 0;
    private float lastAttackTime;

    [Header("Public References")]
    [SerializeField] private Transform punchPosition;
    [SerializeField] private ParticleSystemScript punchParticle;
    [SerializeField] private GameObject lastHitCamera;
    [SerializeField] private Transform lastHitFocusObject;

    //Coroutines
    private Coroutine counterCoroutine;
    private Coroutine attackCoroutine;
    private Coroutine damageCoroutine;

    [Space]

    //Events
    public UnityEvent<EnemyScript> OnTrajectory;
    public UnityEvent<EnemyScript> OnHit;
    public UnityEvent<EnemyScript> OnCounterAttack;

    int animationCount = 0;
    string[] attacks;

    void Start()
    {
        enemyManager = FindObjectOfType<EnemyManager>();
        animator = GetComponent<Animator>();
        enemyDetection = GetComponentInChildren<EnemyDetection>();
        movementInput = GetComponent<MovementInput>();
        impulseSource = GetComponentInChildren<CinemachineImpulseSource>();
    }

    void AttackCheck()
    {
        if (isAttackingEnemy || movementInput.isRolling)
            return;

        // 1. Try to lock on via the joystick direction
        lockedTarget = enemyDetection.CurrentTarget();

        // 2. AUTO-LOCK FIX: If the joystick missed but we want to attack, snap to the closest enemy!
        if (lockedTarget == null)
        {
            lockedTarget = ClosestEnemyWithinRange(15f);
        }

        float distance = lockedTarget != null ? TargetDistance(lockedTarget) : 0;
        Attack(lockedTarget, distance);
    }

    // Helper function to guarantee the Batman stunt finds a target
    EnemyScript ClosestEnemyWithinRange(float maxRange)
    {
        EnemyScript closest = null;
        float minDistance = maxRange;

        foreach (EnemyStruct eStruct in enemyManager.allEnemies)
        {
            EnemyScript enemy = eStruct.enemyScript;
            if (enemy != null && enemy.IsAttackable() && enemy.isActiveAndEnabled)
            {
                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = enemy;
                }
            }
        }
        return closest;
    }

    public void Attack(EnemyScript target, float distance)
    {
        attacks = new string[] { "AirKick", "AirKick2", "AirPunch", "AirKick3" };

        if (!movementInput.isGrounded)
        {
            AttackType("JumpAttack", attackCooldown, target, 0f);
            return;
        }

        if (target == null || distance < attackRange)
        {
            if (Time.time - lastAttackTime > comboResetTime)
            {
                comboStep = 0;
            }

            comboStep++;
            if (comboStep > 3) comboStep = 1;

            lastAttackTime = Time.time;
            animator.SetInteger("ComboStep", comboStep);

            AttackType("Attack", attackCooldown, target, 0f);
        }
        else if (distance < 15)
        {
            comboStep = 0;
            animationCount = (int)Mathf.Repeat((float)animationCount + 1, (float)attacks.Length);
            string attackString = isLastHit() ? attacks[Random.Range(0, attacks.Length)] : attacks[animationCount];

            AttackType(attackString, attackCooldown, target, .65f);
            impulseSource.m_ImpulseDefinition.m_AmplitudeGain = Mathf.Max(3, 1 * distance);
        }
    }

    void AttackType(string attackTrigger, float cooldown, EnemyScript target, float movementDuration)
    {
        animator.SetTrigger(attackTrigger);

        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);
        attackCoroutine = StartCoroutine(AttackCoroutine(isLastHit() ? 1.5f : cooldown));

        if (isLastHit())
            StartCoroutine(FinalBlowCoroutine());

        if (target != null)
        {
            transform.DOLookAt(target.transform.position, .2f);

            if (movementDuration > 0)
            {
                target.StopMoving();
                MoveTorwardsTarget(target, movementDuration);
            }
        }

        IEnumerator AttackCoroutine(float duration)
        {
            movementInput.acceleration = 0;
            isAttackingEnemy = true;
            movementInput.isAttacking = true;

            yield return new WaitForSeconds(duration);

            isAttackingEnemy = false;
            yield return new WaitForSeconds(.2f);

            movementInput.isAttacking = false;
            LerpCharacterAcceleration();
        }

        IEnumerator FinalBlowCoroutine()
        {
            Time.timeScale = .5f;
            lastHitCamera.SetActive(true);
            lastHitFocusObject.position = lockedTarget.transform.position;
            yield return new WaitForSecondsRealtime(2);
            lastHitCamera.SetActive(false);
            Time.timeScale = 1f;
        }
    }

    void MoveTorwardsTarget(EnemyScript target, float duration)
    {
        OnTrajectory.Invoke(target);
        transform.DOLookAt(target.transform.position, .2f);

        // PHYSICS FIX: Turn off the Character Controller so DOTween can glide the player!
        CharacterController cc = GetComponent<CharacterController>();
        cc.enabled = false;

        transform.DOMove(TargetOffset(target.transform), duration).OnComplete(() =>
        {
            cc.enabled = true; // Turn it back on when the glide finishes!
        });
    }

    void CounterCheck()
    {
        if (isCountering || isAttackingEnemy || !enemyManager.AnEnemyIsPreparingAttack() || movementInput.isRolling)
            return;

        lockedTarget = ClosestCounterEnemy();
        OnCounterAttack.Invoke(lockedTarget);

        if (TargetDistance(lockedTarget) > 2)
        {
            Attack(lockedTarget, TargetDistance(lockedTarget));
            return;
        }

        float duration = .2f;
        animator.SetTrigger("Dodge");
        transform.DOLookAt(lockedTarget.transform.position, .2f);

        // Physics fix applies here too!
        CharacterController cc = GetComponent<CharacterController>();
        cc.enabled = false;
        transform.DOMove(transform.position + lockedTarget.transform.forward, duration).OnComplete(() => { cc.enabled = true; });

        if (counterCoroutine != null)
            StopCoroutine(counterCoroutine);
        counterCoroutine = StartCoroutine(CounterCoroutine(duration));

        IEnumerator CounterCoroutine(float duration)
        {
            isCountering = true;
            movementInput.isAttacking = true;
            yield return new WaitForSeconds(duration);
            Attack(lockedTarget, TargetDistance(lockedTarget));
            isCountering = false;
            movementInput.isAttacking = false;
        }
    }

    float TargetDistance(EnemyScript target)
    {
        return Vector3.Distance(transform.position, target.transform.position);
    }

    public Vector3 TargetOffset(Transform target)
    {
        Vector3 position;
        position = target.position;
        return Vector3.MoveTowards(position, transform.position, .95f);
    }

    public void HitEvent()
    {
        if (lockedTarget != null && lockedTarget.health <= 0)
        {
            lockedTarget = null;
        }

        if (lockedTarget == null)
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 3f);
            foreach (var hitCollider in hitColliders)
            {
                EnemyScript enemy = hitCollider.GetComponentInParent<EnemyScript>();

                if (enemy != null && enemy.health > 0)
                {
                    lockedTarget = enemy;
                    break;
                }
            }
        }

        if (lockedTarget == null || lockedTarget.health <= 0 || enemyManager.AliveEnemyCount() == 0)
            return;

        OnHit.Invoke(lockedTarget);
        punchParticle.PlayParticleAtPosition(punchPosition.position);
    }

    public void DamageEvent()
    {
        GetComponent<CharacterController>().enabled = true;

        animator.SetTrigger("Hit");

        if (damageCoroutine != null)
            StopCoroutine(damageCoroutine);
        damageCoroutine = StartCoroutine(DamageCoroutine());

        IEnumerator DamageCoroutine()
        {
            movementInput.isAttacking = true;
            yield return new WaitForSeconds(.5f);
            movementInput.isAttacking = false;
            LerpCharacterAcceleration();
        }
    }

    EnemyScript ClosestCounterEnemy()
    {
        float minDistance = 100;
        int finalIndex = 0;

        for (int i = 0; i < enemyManager.allEnemies.Length; i++)
        {
            EnemyScript enemy = enemyManager.allEnemies[i].enemyScript;

            if (enemy.IsPreparingAttack())
            {
                if (Vector3.Distance(transform.position, enemy.transform.position) < minDistance)
                {
                    minDistance = Vector3.Distance(transform.position, enemy.transform.position);
                    finalIndex = i;
                }
            }
        }

        return enemyManager.allEnemies[finalIndex].enemyScript;
    }

    void LerpCharacterAcceleration()
    {
        movementInput.acceleration = 0;
        DOVirtual.Float(0, 1, .6f, ((acceleration) => movementInput.acceleration = acceleration));
    }

    bool isLastHit()
    {
        if (lockedTarget == null)
            return false;

        return enemyManager.AliveEnemyCount() == 1 && lockedTarget.health <= 1;
    }

    #region Input

    private void OnCounter()
    {
        CounterCheck();
    }

    private void OnAttack()
    {
        AttackCheck();
    }

    #endregion
}