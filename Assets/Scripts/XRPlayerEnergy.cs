using UnityEngine;
using Unity.XR.CoreUtils;

public class XRPlayerEnergy : MonoBehaviour
{
    [SerializeField] private float maxEnergy = 100f;
    [SerializeField] private float sprintDrainPerSecond = 18f;
    [SerializeField] private float movingRecoveryPerSecond = 5f;
    [SerializeField] private float idleRecoveryPerSecond = 11f;
    [SerializeField] private float movementThreshold = 0.015f;
    [SerializeField] private float sprintStartEnergy = 15f;

    private XROrigin xrOrigin;
    private Vector3 lastSamplePosition;
    private bool hasLastSample;
    private bool sprintLocked;

    public float CurrentEnergy { get; private set; }
    public float NormalizedEnergy => maxEnergy <= 0f ? 0f : CurrentEnergy / maxEnergy;
    public float MaxEnergy => maxEnergy;
    public bool IsMoving { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool HasSprintEnergy => CurrentEnergy > 0.01f;
    public bool CanSprint => !sprintLocked && CurrentEnergy >= sprintStartEnergy;

    private void Awake()
    {
        xrOrigin = GetComponent<XROrigin>();
        CurrentEnergy = maxEnergy;
    }

    private void Update()
    {
        Vector3 samplePosition = GetSamplePosition();
        if (!hasLastSample)
        {
            lastSamplePosition = samplePosition;
            hasLastSample = true;
        }

        Vector3 delta = samplePosition - lastSamplePosition;
        delta.y = 0f;
        IsMoving = delta.magnitude > movementThreshold;
        lastSamplePosition = samplePosition;

        bool sprintRequested = InputSprintRequested();
        if (sprintLocked && CurrentEnergy >= sprintStartEnergy)
        {
            sprintLocked = false;
        }

        IsSprinting = sprintRequested && IsMoving && !sprintLocked && HasSprintEnergy;

        if (IsSprinting)
        {
            CurrentEnergy = Mathf.Max(0f, CurrentEnergy - sprintDrainPerSecond * Time.deltaTime);
            if (CurrentEnergy <= 0.01f)
            {
                CurrentEnergy = 0f;
                sprintLocked = true;
                IsSprinting = false;
            }
        }
        else
        {
            float recoveryRate = IsMoving ? movingRecoveryPerSecond : idleRecoveryPerSecond;
            CurrentEnergy = Mathf.Min(maxEnergy, CurrentEnergy + recoveryRate * Time.deltaTime);
        }
    }

    private Vector3 GetSamplePosition()
    {
        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            return xrOrigin.Camera.transform.position;
        }

        return transform.position;
    }

    private static bool InputSprintRequested()
    {
        return UnityEngine.InputSystem.Keyboard.current != null &&
               UnityEngine.InputSystem.Keyboard.current.leftShiftKey.isPressed;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxEnergy = Mathf.Max(1f, maxEnergy);
        sprintDrainPerSecond = Mathf.Max(0.1f, sprintDrainPerSecond);
        movingRecoveryPerSecond = Mathf.Max(0f, movingRecoveryPerSecond);
        idleRecoveryPerSecond = Mathf.Max(movingRecoveryPerSecond, idleRecoveryPerSecond);
        movementThreshold = Mathf.Max(0.001f, movementThreshold);
        sprintStartEnergy = Mathf.Clamp(sprintStartEnergy, 1f, maxEnergy);
    }
#endif
}
