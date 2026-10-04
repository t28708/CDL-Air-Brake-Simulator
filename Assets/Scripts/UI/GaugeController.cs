using UnityEngine;
using UnityEngine.UI;

namespace CDL.AirBrakeSimulator
{
    /// <summary>
    /// Controls dynamic circular needle rotation and digital telemetry readouts
    /// for Primary and Secondary air brake gauges with physical instrument damping.
    /// Turns OFF / dims needles and digital displays when vehicle ignition is OFF.
    /// </summary>
    public class GaugeController : MonoBehaviour
    {
        [Header("Air System Reference")]
        [SerializeField] private AirBrakeSystem airSystem;

        [Header("Gauge Needles (RectTransforms)")]
        [Tooltip("Transform of the Primary Air Needle (rotates around Z axis).")]
        [SerializeField] private RectTransform primaryNeedle;

        [Tooltip("Transform of the Secondary Air Needle (rotates around Z axis).")]
        [SerializeField] private RectTransform secondaryNeedle;

        [Header("Gauge Images for Backlighting")]
        [SerializeField] private Image imgPrimaryDial;
        [SerializeField] private Image imgSecondaryDial;
        [SerializeField] private Image imgPrimaryNeedle;
        [SerializeField] private Image imgSecondaryNeedle;

        [Header("Rotation Calibration")]
        [Tooltip("Gauge needle angle at 0 PSI (in degrees, typically +135 for 8 o'clock position).")]
        [SerializeField] private float angleAtZeroPSI = 135f;

        [Tooltip("Gauge needle angle at 150 PSI (in degrees, typically -135 for 4 o'clock position).")]
        [SerializeField] private float angleAtMaxPSI = -135f;

        [Tooltip("Maximum gauge scale in PSI (standard commercial truck gauge: 150 PSI).")]
        [SerializeField] private float maxGaugePSI = 150f;

        [Tooltip("Needle response damping speed (higher = faster, lower = smoother mechanical lag).")]
        [Range(2f, 30f)]
        [SerializeField] private float needleDampingSpeed = 12f;

        [Header("Digital Readouts")]
        [SerializeField] private Text primaryPsiText;
        [SerializeField] private Text secondaryPsiText;

        [Header("Color Grading")]
        [SerializeField] private Color safeColor = new Color(0.2f, 0.85f, 0.3f);    // 90 - 125 PSI
        [SerializeField] private Color cautionColor = new Color(0.95f, 0.8f, 0.1f); // 60 - 90 & 125 - 140 PSI
        [SerializeField] private Color warningColor = new Color(0.95f, 0.2f, 0.2f); // < 60 PSI & > 140 PSI
        [SerializeField] private Color unpoweredTextColor = new Color(0.28f, 0.32f, 0.38f, 0.35f);

        // Current smoothed angles
        private float currentPrimaryAngle;
        private float currentSecondaryAngle;
        private float targetPrimaryAngle;
        private float targetSecondaryAngle;

        private void Awake()
        {
            if (airSystem == null)
            {
                airSystem = FindFirstObjectByType<AirBrakeSystem>();
            }

            currentPrimaryAngle = angleAtZeroPSI;
            currentSecondaryAngle = angleAtZeroPSI;
            targetPrimaryAngle = angleAtZeroPSI;
            targetSecondaryAngle = angleAtZeroPSI;
        }

        private void OnEnable()
        {
            if (airSystem != null)
            {
                airSystem.OnPressureChanged += HandlePressureChanged;
                airSystem.OnIgnitionChanged += HandleIgnitionChanged;
                // Snap to current system state immediately
                RefreshInstrumentState();
                currentPrimaryAngle = targetPrimaryAngle;
                currentSecondaryAngle = targetSecondaryAngle;
                ApplyNeedleRotations();
            }
        }

        private void OnDisable()
        {
            if (airSystem != null)
            {
                airSystem.OnPressureChanged -= HandlePressureChanged;
                airSystem.OnIgnitionChanged -= HandleIgnitionChanged;
            }
        }

        private void Update()
        {
            if (airSystem == null)
            {
                airSystem = FindFirstObjectByType<AirBrakeSystem>();
            }

            RefreshInstrumentState();

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Smooth needle movement mimicking mechanical gauge springs
            currentPrimaryAngle = Mathf.LerpAngle(currentPrimaryAngle, targetPrimaryAngle, dt * needleDampingSpeed);
            currentSecondaryAngle = Mathf.LerpAngle(currentSecondaryAngle, targetSecondaryAngle, dt * needleDampingSpeed);

            ApplyNeedleRotations();
        }

        private void HandleIgnitionChanged(IgnitionState state)
        {
            RefreshInstrumentState();
        }

        private void HandlePressureChanged(float pPSI, float sPSI)
        {
            RefreshInstrumentState();
        }

        public void RefreshInstrumentState()
        {
            if (airSystem == null) return;

            bool isElectricalOn = (airSystem.CurrentIgnition != IgnitionState.OFF);
            float pPSI = airSystem.PrimaryPSI;
            float sPSI = airSystem.SecondaryPSI;

            if (isElectricalOn)
            {
                targetPrimaryAngle = CalculateAngleForPSI(pPSI);
                targetSecondaryAngle = CalculateAngleForPSI(sPSI);
            }
            else
            {
                // Unpowered: needles drop to zero position
                targetPrimaryAngle = angleAtZeroPSI;
                targetSecondaryAngle = angleAtZeroPSI;
            }

            UpdateDigitalReadouts(pPSI, sPSI, isElectricalOn);
            UpdateBacklightVisuals(isElectricalOn);
        }

        private float CalculateAngleForPSI(float psi)
        {
            float normalized = Mathf.Clamp01(psi / maxGaugePSI);
            return Mathf.Lerp(angleAtZeroPSI, angleAtMaxPSI, normalized);
        }

        private void ApplyNeedleRotations()
        {
            if (primaryNeedle != null)
            {
                primaryNeedle.localRotation = Quaternion.Euler(0f, 0f, currentPrimaryAngle);
            }

            if (secondaryNeedle != null)
            {
                secondaryNeedle.localRotation = Quaternion.Euler(0f, 0f, currentSecondaryAngle);
            }
        }

        private void UpdateDigitalReadouts(float pPSI, float sPSI, bool isElectricalOn)
        {
            if (primaryPsiText != null)
            {
                if (isElectricalOn)
                {
                    primaryPsiText.text = $"{pPSI:F0} PSI";
                    primaryPsiText.color = GetColorForPSI(pPSI);
                }
                else
                {
                    primaryPsiText.text = "-- PSI";
                    primaryPsiText.color = unpoweredTextColor;
                }
            }

            if (secondaryPsiText != null)
            {
                if (isElectricalOn)
                {
                    secondaryPsiText.text = $"{sPSI:F0} PSI";
                    secondaryPsiText.color = GetColorForPSI(sPSI);
                }
                else
                {
                    secondaryPsiText.text = "-- PSI";
                    secondaryPsiText.color = unpoweredTextColor;
                }
            }
        }

        private void UpdateBacklightVisuals(bool isElectricalOn)
        {
            Color dialCol = isElectricalOn ? Color.white : new Color(0.32f, 0.35f, 0.40f, 1f);
            Color needleCol = isElectricalOn ? Color.white : new Color(0.40f, 0.28f, 0.28f, 0.45f);

            if (imgPrimaryDial != null) imgPrimaryDial.color = dialCol;
            if (imgSecondaryDial != null) imgSecondaryDial.color = dialCol;
            if (imgPrimaryNeedle != null) imgPrimaryNeedle.color = needleCol;
            if (imgSecondaryNeedle != null) imgSecondaryNeedle.color = needleCol;
        }

        private Color GetColorForPSI(float psi)
        {
            if (psi < 60f || psi > 140f) return warningColor;
            if (psi >= 90f && psi <= 128f) return safeColor;
            return cautionColor;
        }

        #region Public Setters & Binding

        public void BindAirSystem(AirBrakeSystem system)
        {
            if (airSystem != null)
            {
                airSystem.OnPressureChanged -= HandlePressureChanged;
                airSystem.OnIgnitionChanged -= HandleIgnitionChanged;
            }
            airSystem = system;
            if (airSystem != null && enabled)
            {
                airSystem.OnPressureChanged += HandlePressureChanged;
                airSystem.OnIgnitionChanged += HandleIgnitionChanged;
                RefreshInstrumentState();
            }
        }

        public void SetNeedleReferences(RectTransform priNeedle, RectTransform secNeedle)
        {
            primaryNeedle = priNeedle;
            secondaryNeedle = secNeedle;
        }

        public void SetTextReferences(Text priText, Text secText)
        {
            primaryPsiText = priText;
            secondaryPsiText = secText;
        }

        public void SetImageReferences(Image priDial, Image secDial, Image priNeedle, Image secNeedle)
        {
            imgPrimaryDial = priDial;
            imgSecondaryDial = secDial;
            imgPrimaryNeedle = priNeedle;
            imgSecondaryNeedle = secNeedle;
            RefreshInstrumentState();
        }

        #endregion
    }
}
