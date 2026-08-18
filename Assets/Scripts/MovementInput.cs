using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class MovementInput : MonoBehaviour
{
    private Animator anim;
    private Camera cam;
    private CharacterController controller;

    private Vector3 desiredMoveDirection;
    private Vector3 moveVector;

    public Vector2 moveAxis;
    private float verticalVel;

    [Header("Settings")]
    [SerializeField] float movementSpeed = 5f;
    [SerializeField] float rotationSpeed = 0.1f;
    public float acceleration = 1;

    [Header("Physics Settings")]
    [SerializeField] float gravity = -15f;
    [SerializeField] float jumpHeight = 1.5f;

    [Header("Action Settings")]
    [SerializeField] float rollDuration = 0.5f;
    [SerializeField] float rollSpeedMultiplier = 2f;

    [Header("Booleans")]
    [SerializeField] bool blockRotationPlayer;
    public bool isGrounded;
    public bool isRolling;

    public bool isAttacking;

    void Start()
    {
        anim = this.GetComponent<Animator>();
        cam = Camera.main;
        controller = this.GetComponent<CharacterController>();
    }

    void Update()
    {
        // CRITICAL FIX: If DOTween is gliding us for the Batman Stunt, ignore normal physics!
        if (!controller.enabled) return;

        isGrounded = controller.isGrounded;
        anim.SetBool("IsGrounded", isGrounded);

        ApplyGravity();

        if (!isRolling && !isAttacking)
        {
            InputMagnitude();
        }
        else if (isAttacking)
        {
            desiredMoveDirection = Vector3.zero;
        }

        moveVector = new Vector3(desiredMoveDirection.x, verticalVel, desiredMoveDirection.z);
        controller.Move(moveVector * Time.deltaTime);
    }

    void ApplyGravity()
    {
        if (isGrounded && verticalVel < 0)
        {
            verticalVel = -2f;
        }
        else
        {
            verticalVel += gravity * Time.deltaTime;
        }
    }

    void PlayerMoveAndRotation()
    {
        var forward = cam.transform.forward;
        var right = cam.transform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        desiredMoveDirection = forward * moveAxis.y + right * moveAxis.x;

        if (blockRotationPlayer == false)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desiredMoveDirection), rotationSpeed * acceleration);
            desiredMoveDirection = desiredMoveDirection * (movementSpeed * acceleration);
        }
        else
        {
            desiredMoveDirection = (transform.forward * moveAxis.y + transform.right * moveAxis.x) * (movementSpeed * acceleration);
        }
    }

    public void LookAt(Vector3 pos)
    {
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(pos), rotationSpeed);
    }

    public void RotateToCamera(Transform t)
    {
        var forward = cam.transform.forward;

        desiredMoveDirection = forward;
        Quaternion lookAtRotation = Quaternion.LookRotation(desiredMoveDirection);
        Quaternion lookAtRotationOnly_Y = Quaternion.Euler(transform.rotation.eulerAngles.x, lookAtRotation.eulerAngles.y, transform.rotation.eulerAngles.z);

        t.rotation = Quaternion.Slerp(transform.rotation, lookAtRotationOnly_Y, rotationSpeed);
    }

    void InputMagnitude()
    {
        float inputMagnitude = new Vector2(moveAxis.x, moveAxis.y).sqrMagnitude;

        if (inputMagnitude > 0.1f)
        {
            anim.SetFloat("InputMagnitude", inputMagnitude * acceleration, .1f, Time.deltaTime);
            PlayerMoveAndRotation();
        }
        else
        {
            anim.SetFloat("InputMagnitude", 0f, .1f, Time.deltaTime);
            desiredMoveDirection = Vector3.zero;
        }
    }

    #region Input

    public void OnMove(InputValue value)
    {
        moveAxis = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed && isGrounded && !isRolling && !isAttacking)
        {
            verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            anim.SetTrigger("Jump");
        }
    }

    public void OnRoll(InputValue value)
    {
        if (value.isPressed && isGrounded && !isRolling && !isAttacking && moveAxis.sqrMagnitude > 0.1f)
        {
            StartCoroutine(RollRoutine());
        }
    }

    #endregion

    private IEnumerator RollRoutine()
    {
        isRolling = true;
        anim.SetTrigger("Roll");

        float originalSpeed = movementSpeed;
        movementSpeed *= rollSpeedMultiplier;

        yield return new WaitForSeconds(rollDuration);

        movementSpeed = originalSpeed;
        isRolling = false;
    }

    private void OnDisable()
    {
        anim.SetFloat("InputMagnitude", 0);
    }
}