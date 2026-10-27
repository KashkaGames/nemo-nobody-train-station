using UnityEngine;
using UnityEngine.Events;

public class TrainController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Maximum cruising speed in meters per second.")]
    [SerializeField] private float maxSpeed = 8.5f;

    [Tooltip("Time in seconds to accelerate from a dead stop (0 m/s) to max speed.")]
    [SerializeField] private float timeToMaxSpeed = 20.0f;

    [Tooltip("World direction the train travels along the tracks. Defaults to transform.forward.")]
    [SerializeField] private Vector3 trackDirection = Vector3.forward;

    [Tooltip("If true, uses trackDirection in world space. If false, uses local transform.forward.")]
    [SerializeField] private bool useWorldDirection = false;

    [Header("Inertia & Departure Timing")]
    [Tooltip("Delay in seconds before the train starts rolling (useful for audio whistle / door close).")]
    [SerializeField] private float departureDelay = 0f;

    [Tooltip("Automatically begins departure on Start.")]
    [SerializeField] private bool startOnPlay = true;

    [Tooltip("Custom acceleration profile (0,0 to 1,1). Ease-in simulates heavy locomotive mass.")]
    [SerializeField] private AnimationCurve accelerationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Events (Optional Audio/VFX Hooks)")]
    public UnityEvent onDepartureStarted;
    public UnityEvent onMaxSpeedReached;

    // Runtime state
    private bool isMoving = false;
    private float elapsedTime = 0f;
    private float currentSpeed = 0f;
    private float distanceTraveled = 0f;
    private bool hasReachedMaxSpeed = false;

    // Public properties for camera blending, HUD, and audio lerps
    public bool IsMoving => isMoving;
    public float CurrentSpeed => currentSpeed;
    public float NormalizedSpeed => Mathf.Clamp01(currentSpeed / Mathf.Max(0.001f, maxSpeed));
    public float DistanceTraveled => distanceTraveled;
    public float ElapsedAccelerationTime => Mathf.Min(elapsedTime, timeToMaxSpeed);

    private void Start()
    {
        trackDirection = trackDirection.normalized;

        if (startOnPlay)
        {
            if (departureDelay > 0f)
            {
                Invoke(nameof(StartDeparture), departureDelay);
            }
            else
            {
                StartDeparture();
            }
        }
    }

    private void Update()
    {
        if (!isMoving) return;

        elapsedTime += Time.deltaTime;

        // Calculate progress normalized from 0.0 to 1.0 over the 20-second window
        float progress = Mathf.Clamp01(elapsedTime / Mathf.Max(0.001f, timeToMaxSpeed));

        // Evaluate through the acceleration curve for heavy physical inertia
        float speedMultiplier = accelerationCurve.Evaluate(progress);
        currentSpeed = Mathf.Lerp(0f, maxSpeed, speedMultiplier);

        // Check for max speed milestone event
        if (!hasReachedMaxSpeed && progress >= 1.0f)
        {
            hasReachedMaxSpeed = true;
            onMaxSpeedReached?.Invoke();
        }

        // Apply translation along the track
        Vector3 moveDir = useWorldDirection ? trackDirection : transform.forward;
        Vector3 displacement = moveDir * (currentSpeed * Time.deltaTime);

        transform.position += displacement;
        distanceTraveled += displacement.magnitude;
    }

    /// <summary>
    /// Begins the train's departure roll.
    /// </summary>
    public void StartDeparture()
    {
        if (isMoving) return;

        isMoving = true;
        elapsedTime = 0f;
        hasReachedMaxSpeed = false;
        onDepartureStarted?.Invoke();
    }

    /// <summary>
    /// Instantly halts train movement.
    /// </summary>
    public void StopTrain()
    {
        isMoving = false;
        currentSpeed = 0f;
    }

    /// <summary>
    /// Resets the train back to a designated platform origin position.
    /// </summary>
    public void ResetPosition(Vector3 originPosition)
    {
        StopTrain();
        transform.position = originPosition;
        elapsedTime = 0f;
        distanceTraveled = 0f;
        hasReachedMaxSpeed = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Visualize the forward track trajectory in Scene view
        Vector3 dir = useWorldDirection ? trackDirection.normalized : transform.forward;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, dir * 15f);
    }
#endif
}