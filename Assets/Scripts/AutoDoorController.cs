using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;

public class AutoDoorController : MonoBehaviour
{
    private enum DoorAnimationMode
    {
        SingleSwing = 0,
        DoubleSwing = 1
    }

    [SerializeField] private DoorAnimationMode animationMode = DoorAnimationMode.SingleSwing;
    [SerializeField] private Transform[] doorPanels;
    [SerializeField] private Collider[] blockingColliders;
    [SerializeField] private float proximityOpenDistance = 2.25f;
    [SerializeField] private float openAngle = 95f;
    [SerializeField] private float openSpeed = 3f;
    [SerializeField] private float autoCloseDelay = 1.5f;

    private readonly List<Quaternion> closedRotations = new();
    private readonly List<Quaternion> openRotations = new();
    private readonly List<XROrigin> xrOrigins = new();
    private float openWeight;
    private float closeCountdown;
    private int occupants;

    private void Awake()
    {
        CacheDoorRotations();
        CachePlayerOrigins();
        ApplyDoorPose(0f);
    }

    private void Update()
    {
        bool shouldOpen = occupants > 0 || closeCountdown > 0f || IsPlayerWithinRange();
        float targetWeight = shouldOpen ? 1f : 0f;
        openWeight = Mathf.MoveTowards(openWeight, targetWeight, Time.deltaTime * openSpeed);
        ApplyDoorPose(Mathf.SmoothStep(0f, 1f, openWeight));

        bool enableBlocking = openWeight < 0.98f;
        SetBlockingCollidersEnabled(enableBlocking);

        if (occupants == 0 && closeCountdown > 0f)
        {
            closeCountdown -= Time.deltaTime;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        occupants++;
        closeCountdown = autoCloseDelay;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        occupants = Mathf.Max(0, occupants - 1);
        closeCountdown = autoCloseDelay;
    }

    public void Configure(Transform[] panels, Collider[] collidersToToggle, bool useDoubleDoorAnimation)
    {
        animationMode = useDoubleDoorAnimation ? DoorAnimationMode.DoubleSwing : DoorAnimationMode.SingleSwing;
        doorPanels = panels;
        blockingColliders = collidersToToggle;
        transform.gameObject.isStatic = false;
        if (doorPanels != null)
        {
            foreach (Transform panel in doorPanels)
            {
                if (panel != null)
                {
                    panel.gameObject.isStatic = false;
                }
            }
        }

        CacheDoorRotations();
        CachePlayerOrigins();
        ApplyDoorPose(openWeight);
    }

    private void CachePlayerOrigins()
    {
        xrOrigins.Clear();
        XROrigin[] foundOrigins = FindObjectsByType<XROrigin>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (XROrigin origin in foundOrigins)
        {
            if (origin != null)
            {
                xrOrigins.Add(origin);
            }
        }
    }

    private void CacheDoorRotations()
    {
        closedRotations.Clear();
        openRotations.Clear();

        if (doorPanels == null || doorPanels.Length == 0)
        {
            return;
        }

        for (int index = 0; index < doorPanels.Length; index++)
        {
            Transform panel = doorPanels[index];
            if (panel == null)
            {
                closedRotations.Add(Quaternion.identity);
                openRotations.Add(Quaternion.identity);
                continue;
            }

            Quaternion closedRotation = panel.localRotation;
            float direction = ResolveDoorDirection(panel, index);
            Quaternion openRotation = closedRotation * Quaternion.Euler(0f, openAngle * direction, 0f);

            closedRotations.Add(closedRotation);
            openRotations.Add(openRotation);
        }
    }

    private float ResolveDoorDirection(Transform panel, int index)
    {
        string panelName = panel.name.ToLowerInvariant();
        if (animationMode == DoorAnimationMode.SingleSwing)
        {
            if (panelName.Contains("_l") || panelName.Contains(" left") || panel.localPosition.x < -0.01f)
            {
                return -1f;
            }

            return 1f;
        }

        if (panelName.Contains("_l") || panelName.Contains(" left"))
        {
            return -1f;
        }

        if (panelName.Contains("_r") || panelName.Contains(" right"))
        {
            return 1f;
        }

        if (panel.localPosition.x < -0.01f)
        {
            return -1f;
        }

        if (panel.localPosition.x > 0.01f)
        {
            return 1f;
        }

        return index % 2 == 0 ? 1f : -1f;
    }

    private void ApplyDoorPose(float weight)
    {
        if (doorPanels == null)
        {
            return;
        }

        for (int index = 0; index < doorPanels.Length; index++)
        {
            Transform panel = doorPanels[index];
            if (panel == null || index >= closedRotations.Count || index >= openRotations.Count)
            {
                continue;
            }

            panel.localRotation = Quaternion.Slerp(closedRotations[index], openRotations[index], weight);
        }
    }

    private bool IsPlayerWithinRange()
    {
        if (xrOrigins.Count == 0)
        {
            CachePlayerOrigins();
        }

        Vector3 doorPosition = transform.position;
        foreach (XROrigin xrOrigin in xrOrigins)
        {
            if (xrOrigin == null)
            {
                continue;
            }

            Transform referenceTransform = xrOrigin.Camera != null ? xrOrigin.Camera.transform : xrOrigin.transform;
            Vector3 playerPosition = referenceTransform.position;
            playerPosition.y = doorPosition.y;

            if (Vector3.Distance(playerPosition, doorPosition) <= proximityOpenDistance)
            {
                return true;
            }
        }

        return false;
    }

    private void SetBlockingCollidersEnabled(bool shouldEnable)
    {
        if (blockingColliders == null)
        {
            return;
        }

        foreach (Collider blockingCollider in blockingColliders)
        {
            if (blockingCollider != null && blockingCollider.enabled != shouldEnable)
            {
                blockingCollider.enabled = shouldEnable;
            }
        }
    }

    private static bool IsPlayerCollider(Collider other)
    {
        return other.GetComponentInParent<XROrigin>() != null || other.GetComponentInParent<CharacterController>() != null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        proximityOpenDistance = Mathf.Max(0.5f, proximityOpenDistance);
        openAngle = Mathf.Clamp(openAngle, 15f, 160f);
        openSpeed = Mathf.Max(0.1f, openSpeed);
        autoCloseDelay = Mathf.Max(0f, autoCloseDelay);
    }
#endif
}
