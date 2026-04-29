using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

[RequireComponent(typeof(CharacterController))]
public class XREditorDesktopLocomotion : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 2.5f;
    [SerializeField] private float sprintSpeed = 4.5f;
    [SerializeField] private float lookSensitivity = 0.12f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.1f;

    private CharacterController characterController;
    private XROrigin xrOrigin;
    private XRPlayerEnergy energySystem;
    private Transform cameraTransform;
    private Quaternion baseCameraLocalRotation;
    private float verticalVelocity;
    private float pitch;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        xrOrigin = GetComponent<XROrigin>();
        energySystem = GetComponent<XRPlayerEnergy>();

        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            cameraTransform = xrOrigin.Camera.transform;
            baseCameraLocalRotation = cameraTransform.localRotation;
        }
    }

    private void Update()
    {
        if (!ShouldUseDesktopFallback())
        {
            UnlockCursor();
            return;
        }

        HandleCursorToggle();
        HandleLook();
        HandleMovement();
    }

    private bool ShouldUseDesktopFallback()
    {
        return Keyboard.current != null && Mouse.current != null && !XRSettings.isDeviceActive;
    }

    private void HandleCursorToggle()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }
        else if (Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            LockCursor();
        }
    }

    private void HandleLook()
    {
        if (cameraTransform == null || Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * lookSensitivity;
        pitch = Mathf.Clamp(pitch - mouseDelta.y, -80f, 80f);

        transform.Rotate(Vector3.up * mouseDelta.x);
        cameraTransform.localRotation = baseCameraLocalRotation * Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        Vector2 moveInput = ReadMoveInput();

        Vector3 forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
        Vector3 right = cameraTransform != null ? cameraTransform.right : transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (right * moveInput.x) + (forward * moveInput.y);
        bool canSprint = energySystem == null || energySystem.CanSprint;
        float currentSpeed = (IsSprinting() && canSprint) ? sprintSpeed : walkSpeed;

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (characterController.isGrounded && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = moveDirection * currentSpeed;
        velocity.y = verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed)
        {
            input.y += 1f;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            input.y -= 1f;
        }

        if (Keyboard.current.aKey.isPressed)
        {
            input.x -= 1f;
        }

        if (Keyboard.current.dKey.isPressed)
        {
            input.x += 1f;
        }

        return input.normalized;
    }

    private bool IsSprinting()
    {
        return Keyboard.current.leftShiftKey.isPressed;
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
