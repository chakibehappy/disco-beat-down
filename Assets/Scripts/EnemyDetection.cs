using System.Collections.Generic;
using UnityEngine;

public class EnemyDetection : MonoBehaviour
{
    [SerializeField] private EnemyManager enemyManager;
    private MovementInput movementInput;
    private CombatScript combatScript;

    public LayerMask layerMask;

    [SerializeField] Vector3 inputDirection;
    [SerializeField] private EnemyScript currentTarget;

    public GameObject cam;

    private void Start()
    {
        movementInput = GetComponentInParent<MovementInput>();
        combatScript = GetComponentInParent<CombatScript>();
    }

    private void Update()
    {
        var camera = Camera.main;
        var forward = camera.transform.forward;
        var right = camera.transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 joystickDirection = forward * movementInput.moveAxis.y + right * movementInput.moveAxis.x;

        // If we aren't moving the joystick, check directly in front of the player
        if (joystickDirection.magnitude < 0.1f)
        {
            inputDirection = transform.forward;
        }
        else
        {
            inputDirection = joystickDirection.normalized;
        }

        RaycastHit info;

        // CRITICAL FIX: Reset the target every frame so we don't lock onto dead/ghost enemies
        currentTarget = null;

        if (Physics.SphereCast(transform.position, 3f, inputDirection, out info, 10, layerMask))
        {
            EnemyScript enemy = info.collider.transform.GetComponent<EnemyScript>();

            if (enemy != null && enemy.IsAttackable())
            {
                currentTarget = enemy;
            }
        }
    }

    public EnemyScript CurrentTarget()
    {
        return currentTarget;
    }

    public void SetCurrentTarget(EnemyScript target)
    {
        currentTarget = target;
    }

    public float InputMagnitude()
    {
        // Return the actual joystick magnitude 
        return new Vector2(movementInput.moveAxis.x, movementInput.moveAxis.y).magnitude;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawRay(transform.position, inputDirection);
        Gizmos.DrawWireSphere(transform.position, 1);
        if (CurrentTarget() != null)
            Gizmos.DrawSphere(CurrentTarget().transform.position, .5f);
    }
}