using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

public class LaboratoryHudController : MonoBehaviour
{
    [SerializeField] private RectTransform playerMarkerRoot;
    [SerializeField] private RectTransform energyFill;
    [SerializeField] private Image energyFillImage;
    [SerializeField] private Text energyLabel;

    private XROrigin xrOrigin;
    private XRPlayerEnergy energySystem;

    private void LateUpdate()
    {
        if (xrOrigin == null)
        {
            xrOrigin = FindFirstObjectByType<XROrigin>();
        }

        if (energySystem == null)
        {
            energySystem = FindFirstObjectByType<XRPlayerEnergy>();
        }

        UpdateMarker();
        UpdateEnergy();
    }

    private void UpdateMarker()
    {
        if (playerMarkerRoot == null || xrOrigin == null)
        {
            return;
        }

        Transform referenceTransform = xrOrigin.Camera != null ? xrOrigin.Camera.transform : xrOrigin.transform;
        playerMarkerRoot.localRotation = Quaternion.Euler(0f, 0f, -referenceTransform.eulerAngles.y);
    }

    private void UpdateEnergy()
    {
        if (energySystem == null)
        {
            return;
        }

        if (energyFill != null)
        {
            float normalizedEnergy = Mathf.Clamp01(energySystem.NormalizedEnergy);
            energyFill.anchorMax = new Vector2(normalizedEnergy, 1f);

            if (energyFillImage != null)
            {
                Color baseColor = Color.Lerp(
                    new Color(0.92f, 0.22f, 0.25f, 1f),
                    new Color(0.18f, 0.88f, 0.95f, 1f),
                    normalizedEnergy);

                if (normalizedEnergy <= 0.25f)
                {
                    float pulse = 0.55f + Mathf.PingPong(Time.time * 2.5f, 0.45f);
                    baseColor = Color.Lerp(baseColor, Color.white, 0.25f * pulse);
                }

                energyFillImage.color = baseColor;
            }
        }

        if (energyLabel != null)
        {
            energyLabel.text = $"ENERGY {Mathf.RoundToInt(energySystem.CurrentEnergy)}/{Mathf.RoundToInt(energySystem.MaxEnergy)}";

            if (energySystem.NormalizedEnergy <= 0.25f)
            {
                float pulse = 0.45f + Mathf.PingPong(Time.time * 2.5f, 0.55f);
                energyLabel.color = Color.Lerp(
                    new Color(1f, 0.72f, 0.72f, 1f),
                    new Color(1f, 0.25f, 0.25f, 1f),
                    pulse);
            }
            else
            {
                energyLabel.color = Color.white;
            }
        }
    }
}
