using UnityEngine;
using Unity.XR.CoreUtils;

[RequireComponent(typeof(Camera))]
public class MinimapCameraFollow : MonoBehaviour
{
    [SerializeField] private float height = 28f;
    [SerializeField] private float followSmoothness = 8f;

    private XROrigin xrOrigin;

    private void LateUpdate()
    {
        if (xrOrigin == null)
        {
            xrOrigin = FindFirstObjectByType<XROrigin>();
            if (xrOrigin == null)
            {
                return;
            }
        }

        Transform target = xrOrigin.Camera != null ? xrOrigin.Camera.transform : xrOrigin.transform;
        Vector3 desiredPosition = new Vector3(target.position.x, target.position.y + height, target.position.z);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * followSmoothness);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        height = Mathf.Max(5f, height);
        followSmoothness = Mathf.Max(0.1f, followSmoothness);
    }
#endif
}
