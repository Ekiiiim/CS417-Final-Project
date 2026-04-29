using UnityEngine;
using Unity.XR.CoreUtils;

[RequireComponent(typeof(CharacterController))]
public class XRPlayerCollisionHandler : MonoBehaviour
{
    [SerializeField] private float fallDistanceBeforeRespawn = 4f;
    [SerializeField] private float respawnHeightOffset = 0.15f;
    [SerializeField] private float pushForce = 1.5f;
    [SerializeField] private bool pushRigidbodies = true;

    private CharacterController characterController;
    private XROrigin xrOrigin;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private float fallThresholdY;
    private bool hasSpawnPose;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        xrOrigin = GetComponent<XROrigin>();
    }

    private void Start()
    {
        CaptureSpawnPose();
    }

    private void LateUpdate()
    {
        if (!hasSpawnPose)
        {
            return;
        }

        float referenceY = xrOrigin != null && xrOrigin.Camera != null
            ? xrOrigin.Camera.transform.position.y
            : transform.position.y;

        if (referenceY < fallThresholdY)
        {
            RespawnToSpawnPose();
        }
    }

    public void CaptureSpawnPose()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        fallThresholdY = spawnPosition.y - Mathf.Max(1f, fallDistanceBeforeRespawn);
        hasSpawnPose = true;
    }

    private void RespawnToSpawnPose()
    {
        bool controllerEnabled = characterController != null && characterController.enabled;
        if (controllerEnabled)
        {
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(
            spawnPosition + Vector3.up * Mathf.Max(0f, respawnHeightOffset),
            spawnRotation);

        if (controllerEnabled)
        {
            characterController.enabled = true;
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!pushRigidbodies)
        {
            return;
        }

        Rigidbody hitBody = hit.rigidbody;
        if (hitBody == null || hitBody.isKinematic)
        {
            return;
        }

        Vector3 pushDirection = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        if (pushDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        hitBody.AddForce(pushDirection.normalized * pushForce, ForceMode.Impulse);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fallDistanceBeforeRespawn = Mathf.Max(1f, fallDistanceBeforeRespawn);
        respawnHeightOffset = Mathf.Max(0f, respawnHeightOffset);
        pushForce = Mathf.Max(0f, pushForce);
    }
#endif
}
