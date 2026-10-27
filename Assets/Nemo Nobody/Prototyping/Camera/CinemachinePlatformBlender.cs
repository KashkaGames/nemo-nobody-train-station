using UnityEngine;
#if UNITY_6000_0_OR_NEWER
using Unity.Cinemachine;
using CinemachineCam = Unity.Cinemachine.CinemachineCamera;
#else
using Cinemachine;
using CinemachineCam = Cinemachine.CinemachineVirtualCamera;
#endif

[DefaultExecutionOrder(100)]
public class CinemachinePlatformBlender : MonoBehaviour
{
    [Header("1. Anchors (The 1D Decision Axis)")]
    [Tooltip("Stationary transform on the platform where Father stands.")]
    [SerializeField] private Transform fatherAnchor;

    [Tooltip("Moving transform on the train carriage step where Mother reaches.")]
    [SerializeField] private Transform motherAnchor;

    [Tooltip("Nemo's transform (controlled by NemoController).")]
    [SerializeField] private Transform nemoTransform;

    [Header("2. Source Cinemachine Cameras")]
    [Tooltip("Cinemachine camera configured for Father's perspective (Telephoto).")]
    [SerializeField] private CinemachineCam fatherCamera;

    [Tooltip("Cinemachine camera configured for the child follow dolly (Medium).")]
    [SerializeField] private CinemachineCam dollyCamera;

    [Tooltip("Cinemachine camera parented to train carriage looking back (Wide).")]
    [SerializeField] private CinemachineCam motherCamera;

    [Header("3. Output Live Camera")]
    [Tooltip("The output Cinemachine camera that CinemachineBrain renders (Set Priority to 100).")]
    [SerializeField] private CinemachineCam blendedOutputCamera;

    [Header("4. Blending Settings")]
    [Tooltip("Smooths out sudden input taps. Set to 0 for instantaneous 1:1 tracking.")]
    [SerializeField] private float blendDamping = 10f;

    [Header("Telemetry / Debug (Read Only)")]
    [Range(0f, 1f)]
    [SerializeField] private float progressT = 0f;
    [SerializeField] private float fatherWeight = 1f;
    [SerializeField] private float dollyWeight = 0f;
    [SerializeField] private float motherWeight = 0f;

    [Header("Obstacle Clearance")]
    [SerializeField] private bool preventClipping = true;
    [SerializeField] private float cameraCollisionRadius = 0.25f;
    [SerializeField] private LayerMask collisionLayers = ~0; // Everything

    public float ProgressT => progressT;
    public float FatherWeight => fatherWeight;
    public float DollyWeight => dollyWeight;
    public float MotherWeight => motherWeight;

    private void LateUpdate()
    {
        if (!ValidateSetup()) return;

        CalculateDynamicProgress();
        CalculateWeights();
        ApplyBlendedState();
    }

    private bool ValidateSetup()
    {
        return fatherAnchor != null && motherAnchor != null && nemoTransform != null &&
               fatherCamera != null && dollyCamera != null && motherCamera != null &&
               blendedOutputCamera != null;
    }

    private void CalculateDynamicProgress()
    {
        // Vector along the platform between the two parents
        Vector3 platformAxis = motherAnchor.position - fatherAnchor.position;
        float totalPlatformLength = platformAxis.magnitude;

        if (totalPlatformLength <= 0.001f) return;

        Vector3 platformDir = platformAxis / totalPlatformLength;
        Vector3 nemoOffset = nemoTransform.position - fatherAnchor.position;

        // Project Nemo onto the axis
        float projectedDistance = Vector3.Dot(nemoOffset, platformDir);

        // Normalize: 0.0 (at Father) -> 1.0 (at Mother)
        progressT = Mathf.Clamp01(projectedDistance / totalPlatformLength);
    }

    private void CalculateWeights()
    {
        if (progressT <= 0.5f)
        {
            // Zone 1: Father (1.0 -> 0.0) to Dolly (0.0 -> 1.0)
            float localAlpha = progressT / 0.5f;
            float smooth = Mathf.SmoothStep(0f, 1f, localAlpha);

            fatherWeight = 1f - smooth;
            dollyWeight = smooth;
            motherWeight = 0f;
        }
        else
        {
            // Zone 2: Dolly (1.0 -> 0.0) to Mother (0.0 -> 1.0)
            float localAlpha = (progressT - 0.5f) / 0.5f;
            float smooth = Mathf.SmoothStep(0f, 1f, localAlpha);

            fatherWeight = 0f;
            dollyWeight = 1f - smooth;
            motherWeight = smooth;
        }
    }

    private void ApplyBlendedState()
    {
        Vector3 sourcePosA, sourcePosB;
        Quaternion sourceRotA, sourceRotB;
        LensSettings lensA, lensB;
        float blendAlpha;

        if (progressT <= 0.5f)
        {
            fatherCamera.transform.GetPositionAndRotation(out sourcePosA, out sourceRotA);
            lensA = GetLens(fatherCamera);

            dollyCamera.transform.GetPositionAndRotation(out sourcePosB, out sourceRotB);
            lensB = GetLens(dollyCamera);

            blendAlpha = dollyWeight;
        }
        else
        {
            dollyCamera.transform.GetPositionAndRotation(out sourcePosA, out sourceRotA);
            lensA = GetLens(dollyCamera);

            motherCamera.transform.GetPositionAndRotation(out sourcePosB, out sourceRotB);
            lensB = GetLens(motherCamera);

            blendAlpha = motherWeight;
        }

        Vector3 targetPos = Vector3.Lerp(sourcePosA, sourcePosB, blendAlpha);
        Quaternion targetRot = Quaternion.Slerp(sourceRotA, sourceRotB, blendAlpha);
        LensSettings targetLens = LensSettings.Lerp(lensA, lensB, blendAlpha);

        // Native Physics check: If a wall/train gets between Nemo and the camera, push camera out
        if (preventClipping && nemoTransform != null)
        {
            Vector3 castOrigin = nemoTransform.position + Vector3.up * 1.0f;
            Vector3 toCamera = targetPos - castOrigin;
            float targetDistance = toCamera.magnitude;

            if (Physics.SphereCast(castOrigin, cameraCollisionRadius, toCamera.normalized, out RaycastHit hit, targetDistance, collisionLayers))
            {
                // Place camera just in front of the hit surface
                targetPos = castOrigin + (toCamera.normalized * Mathf.Max(0.5f, hit.distance - cameraCollisionRadius));
            }
        }

        Transform outputTransform = blendedOutputCamera.transform;
        if (blendDamping > 0f)
        {
            float dt = Time.deltaTime * blendDamping;
            outputTransform.SetPositionAndRotation(Vector3.Lerp(outputTransform.position, targetPos, dt), Quaternion.Slerp(outputTransform.rotation, targetRot, dt));
        }
        else
        {
            outputTransform.SetPositionAndRotation(targetPos, targetRot);
        }

        SetLens(blendedOutputCamera, targetLens);
    }
    private LensSettings GetLens(CinemachineCam cam)
    {
#if UNITY_6000_0_OR_NEWER
        return cam.Lens;
#else
        return cam.m_Lens;
#endif
    }

    private void SetLens(CinemachineCam cam, LensSettings lens)
    {
#if UNITY_6000_0_OR_NEWER
        cam.Lens = lens;
#else
        cam.m_Lens = lens;
#endif
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (fatherAnchor == null || motherAnchor == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(fatherAnchor.position, motherAnchor.position);

        if (nemoTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(nemoTransform.position, 0.35f);
        }
    }
#endif
}