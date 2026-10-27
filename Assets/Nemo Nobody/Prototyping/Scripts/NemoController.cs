using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class NemoController : MonoBehaviour
{
    [Header("Acceleration Tuning")]
    [Tooltip("How quickly MotionSpeed ramps up to 1.0 while holding Space.")]
    [SerializeField] private float accelerationRate;

    [Tooltip("How quickly MotionSpeed drops back to 0.0 when Space is released.")]
    [SerializeField] private float decelerationRate;

    [Header("Platform Track Alignment")]
    [Tooltip("Direction along the platform toward Mother/Train.")]
    [SerializeField] private Vector3 platformForward = Vector3.forward;

    [Tooltip("Projects root motion strictly along platform forward to prevent stock animations from wobbling sideways.")]
    [SerializeField] private bool lockToPlatformForward = true;

    [Header("Animator Parameter")]
    [SerializeField] private string speedFloatName = "MotionSpeed";

    private CharacterController characterController;
    private Animator animator;
    private int speedHash;
    private float currentSpeedNormalized = 0f;
    private float verticalVelocity = 0f;

    public float CurrentSpeedNormalized => currentSpeedNormalized;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        speedHash = Animator.StringToHash(speedFloatName);
        platformForward = platformForward.normalized;
    }

    private void Update()
    {
        HandleInput();
        UpdateAnimatorParameters();
    }

    private void HandleInput()
    {
        bool isPressingRun = Input.GetKey(KeyCode.Space);

        if (isPressingRun)
        {
            currentSpeedNormalized += accelerationRate * Time.deltaTime;
        }
        else
        {
            currentSpeedNormalized -= decelerationRate * Time.deltaTime;
        }

        currentSpeedNormalized = Mathf.Clamp01(currentSpeedNormalized);
    }

    private void UpdateAnimatorParameters()
    {
        animator.SetFloat(speedHash, currentSpeedNormalized);
    }

    private void OnAnimatorMove()
    {
        if (animator == null || characterController == null) return;

        // 1. Get raw root motion translation calculated by the blend tree
        Vector3 deltaPosition = animator.deltaPosition;

        // 2. Lock to platform line
        // Stock/MoCap clips often have slight lateral hip sway that can steer Nemo off the platform.
        // This projects the forward motion purely onto platformForward while maintaining exact stride velocity.
        if (lockToPlatformForward)
        {
            float forwardDisplacement = Vector3.Dot(deltaPosition, platformForward);
            deltaPosition = platformForward * forwardDisplacement;
            transform.rotation = Quaternion.LookRotation(platformForward, Vector3.up);
        }

        // 3. Gravity integration
        if (characterController.isGrounded)
        {
            verticalVelocity = -2f; // Keeps character anchored to the ground mesh
        }
        else
        {
            verticalVelocity += Physics.gravity.y * Time.deltaTime;
        }

        deltaPosition.y = verticalVelocity * Time.deltaTime;

        // 4. Feed displacement through CharacterController for collision checks
        characterController.Move(deltaPosition);
    }
}