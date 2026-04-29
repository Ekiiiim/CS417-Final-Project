using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public class XRMoveSpeedModifier : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 2.5f;
    [SerializeField] private float sprintSpeed = 4.5f;

    private ContinuousMoveProvider[] moveProviders;
    private XRPlayerEnergy energySystem;

    private void Awake()
    {
        moveProviders = GetComponentsInChildren<ContinuousMoveProvider>(true);
        energySystem = GetComponent<XRPlayerEnergy>();
        ApplySpeed(walkSpeed);
    }

    private void Update()
    {
        bool sprintRequested = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        bool isSprinting = sprintRequested && (energySystem == null || energySystem.CanSprint);
        ApplySpeed(isSprinting ? sprintSpeed : walkSpeed);
    }

    private void ApplySpeed(float targetSpeed)
    {
        if (moveProviders == null)
        {
            return;
        }

        foreach (ContinuousMoveProvider moveProvider in moveProviders)
        {
            if (moveProvider == null)
            {
                continue;
            }

            if (!Mathf.Approximately(moveProvider.moveSpeed, targetSpeed))
            {
                moveProvider.moveSpeed = targetSpeed;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0.1f, walkSpeed);
        sprintSpeed = Mathf.Max(walkSpeed, sprintSpeed);
    }
#endif
}
