using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

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
        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed ||
               VRControllerInput.IsTriggerPressedOnEitherHand();
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

internal static class VRControllerInput
{
    private const float ButtonAxisThreshold = 0.5f;
    private static readonly List<XRInputDevice> Devices = new();
    private static InputAction sprintAction;

    public static bool IsTriggerPressedOnEitherHand()
    {
        if (ReadSprintAction())
        {
            return true;
        }

        return IsTriggerPressed(XRNode.LeftHand) || IsTriggerPressed(XRNode.RightHand);
    }

    private static bool ReadSprintAction()
    {
        if (sprintAction == null)
        {
            sprintAction = new InputAction("VR Sprint", InputActionType.Button);
            sprintAction.AddBinding("<XRController>{LeftHand}/trigger");
            sprintAction.AddBinding("<XRController>{LeftHand}/triggerPressed");
            sprintAction.AddBinding("<XRController>{RightHand}/trigger");
            sprintAction.AddBinding("<XRController>{RightHand}/triggerPressed");
            sprintAction.AddBinding("<XRController>/trigger");
            sprintAction.AddBinding("<XRController>/triggerPressed");
        }

        if (!sprintAction.enabled)
        {
            sprintAction.Enable();
        }

        return sprintAction.IsPressed();
    }

    private static bool IsTriggerPressed(XRNode hand)
    {
        if (TryGetButton(hand, XRCommonUsages.triggerButton))
        {
            return true;
        }

        return TryGetDevice(hand, out XRInputDevice device) &&
               device.TryGetFeatureValue(XRCommonUsages.trigger, out float triggerValue) &&
               triggerValue >= ButtonAxisThreshold;
    }

    private static bool TryGetButton(XRNode hand, InputFeatureUsage<bool> usage)
    {
        return TryGetDevice(hand, out XRInputDevice device) &&
               device.TryGetFeatureValue(usage, out bool isPressed) &&
               isPressed;
    }

    private static bool TryGetDevice(XRNode hand, out XRInputDevice device)
    {
        device = InputDevices.GetDeviceAtXRNode(hand);
        if (device.isValid)
        {
            return true;
        }

        Devices.Clear();
        InputDeviceCharacteristics handCharacteristic = hand == XRNode.LeftHand
            ? InputDeviceCharacteristics.Left
            : InputDeviceCharacteristics.Right;

        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | handCharacteristic,
            Devices);

        foreach (XRInputDevice candidate in Devices)
        {
            if (candidate.isValid)
            {
                device = candidate;
                return true;
            }
        }

        return false;
    }
}
