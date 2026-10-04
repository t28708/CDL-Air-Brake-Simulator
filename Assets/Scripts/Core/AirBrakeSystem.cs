using System;
using UnityEngine;

namespace CDL.AirBrakeSimulator
{
    /// <summary>
    /// Core Simulation & Physics Engine for the Class A CDL Air Brake System.
    /// Simulates dual air circuits (Primary & Secondary), engine compressor dynamics,
    /// push-pull protection valves, service brake pedal fanning/holding, and FMCSA safety thresholds.
    /// </summary>
    public class AirBrakeSystem : MonoBehaviour
    {
        [Header("Pressure Gauges (0 - 150 PSI)")]
        [Tooltip("Primary reservoir pressure (services rear drive axle brakes).")]
        [Range(0f, 150f)]
        [SerializeField] private float primaryPSI = 125f;

        [Tooltip("Secondary reservoir pressure (services front steer axle & trailer supply).")]
        [Range(0f, 150f)]
        [SerializeField] private float secondaryPSI = 100f;

        [Header("Starting Pressure Randomization (New Game)")]
        [Tooltip("Minimum starting PSI when a new game begins.")]
        [SerializeField] private float newGameMinPSI = 90f;
        [Tooltip("Maximum starting PSI when a new game begins.")]
        [SerializeField] private float newGameMaxPSI = 110f;

        [Header("Engine & Compressor")]
        [SerializeField] private IgnitionState ignitionState = IgnitionState.OFF;
        [Tooltip("Governor Cut-Out Pressure (FMCSA standard: 120 - 140 PSI).")]
        [Range(120f, 140f)]
        [SerializeField] private float governorCutOutPSI = 126f;

        [Tooltip("Governor Cut-In Pressure (typically ~20-25 PSI below Cut-Out).")]
        [Range(85f, 110f)]
        [SerializeField] private float governorCutInPSI = 102f;

        [Tooltip("Normal idle compressor build-up rate (PSI per second).")]
        [SerializeField] private float idleCompressorRate = 3.5f;

        [Tooltip("Revved / fast idle compressor build-up rate (PSI per second).")]
        [SerializeField] private float fastIdleCompressorRate = 8.5f;

        [Tooltip("Is engine currently running at fast idle / revved (1200-1500 RPM).")]
        [SerializeField] private bool isEngineRevved = false;

        [Header("Push-Pull Valves")]
        [Tooltip("Yellow Diamond Knob (Tractor Parking / Spring Brake). True = Pushed IN (Released), False = Popped OUT (Applied).")]
        [SerializeField] private bool yellowBrakePushedIn = false;

        [Tooltip("Red Octagon Knob (Trailer Air Supply). True = Pushed IN (Supplied), False = Popped OUT (Applied).")]
        [SerializeField] private bool redTrailerPushedIn = false;

        [Tooltip("Red Trailer Air Supply Pop-Out Pressure (FMCSA: 20 - 45 PSI, trailer pops first).")]
        [Range(35f, 45f)]
        [SerializeField] private float redPopOutThreshold = 42f;

        [Tooltip("Yellow Tractor Parking Knob Pop-Out Pressure (FMCSA: 20 - 45 PSI, tractor pops second).")]
        [Range(20f, 32f)]
        [SerializeField] private float yellowPopOutThreshold = 26f;

        [Header("Service Brake Pedal")]
        [SerializeField] private bool isPedalHeld = false;
        [Tooltip("Minimum pressure drop per fanning pump (PSI).")]
        [SerializeField] private float minFanDrop = 8f;
        [Tooltip("Maximum pressure drop per fanning pump (PSI).")]
        [SerializeField] private float maxFanDrop = 12f;

        [Header("Applied Leakage Dynamics")]
        [Tooltip("Standard legal leakage rate under applied pedal (PSI per minute, normal truck is ~1.5 - 2.0 PSI/min).")]
        [SerializeField] private float normalAppliedLeakRatePerMin = 1.8f;

        [Tooltip("Simulate an illegal air leak exceeding 4 PSI/min for student training / fault testing.")]
        [SerializeField] private bool faultInjectExcessiveLeak = false;
        [SerializeField] private float faultLeakRatePerMin = 6.5f;

        [Header("Low Air Warning")]
        [Tooltip("Threshold at or below which warning buzzer and visual alarm activate (FMCSA: at or above 60 PSI).")]
        [Range(55f, 75f)]
        [SerializeField] private float lowAirWarningThreshold = 60f;

        // Internal State Tracking
        private bool isCompressorLoaded = false;
        private bool isLowAirWarningActive = false;
        private float pedalHoldTimer = 0f;
        private bool initialChamberDropApplied = false;

        // Events for UI, Audio, and DMV Test Manager
        public event Action<float, float> OnPressureChanged;
        public event Action<IgnitionState> OnIgnitionChanged;
        public event Action<ValveType, bool> OnValveStateChanged;
        public event Action<ValveType, float> OnEmergencyValvePopOut;
        public event Action<bool> OnPedalStateChanged;
        public event Action<float> OnPedalFanned;
        public event Action<float> OnGovernorCutOut;
        public event Action OnGovernorCutIn;
        public event Action<bool> OnLowAirWarningChanged;

        #region Public Properties

        public float PrimaryPSI => primaryPSI;
        public float SecondaryPSI => secondaryPSI;
        public float AveragePSI => (primaryPSI + secondaryPSI) * 0.5f;
        public IgnitionState CurrentIgnition => ignitionState;
        public bool IsYellowBrakePushedIn => yellowBrakePushedIn;
        public bool IsRedTrailerPushedIn => redTrailerPushedIn;
        public bool IsPedalHeld => isPedalHeld;
        public float PedalHoldDuration => pedalHoldTimer;
        public bool IsLowAirWarningActive => isLowAirWarningActive;
        public bool IsCompressorLoaded => isCompressorLoaded;
        public bool IsEngineRevved => isEngineRevved;
        public float GovernorCutOutPSI => governorCutOutPSI;
        public float LowAirWarningThreshold => lowAirWarningThreshold;
        public bool FaultInjectExcessiveLeak
        {
            get => faultInjectExcessiveLeak;
            set => faultInjectExcessiveLeak = value;
        }

        #endregion

        private void Awake()
        {
            ResetForNewGame(newGameMinPSI, newGameMaxPSI);
        }

        private void Start()
        {
            // Initial check of compressor load state
            if (primaryPSI < governorCutOutPSI)
            {
                isCompressorLoaded = true;
            }

            CheckLowAirWarning(true);
            NotifyStateChanged();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateCompressorDynamics(dt);
            UpdateAppliedLeakageDynamics(dt);
            UpdateAutomaticValvePopOut();
            CheckLowAirWarning(false);
        }

        #region Physics & Pressure Dynamics

        private void UpdateCompressorDynamics(float dt)
        {
            if (ignitionState != IgnitionState.RUNNING) return;

            // Check if cut-in is triggered
            if (!isCompressorLoaded && primaryPSI <= governorCutInPSI)
            {
                isCompressorLoaded = true;
                OnGovernorCutIn?.Invoke();
            }

            // Charge reservoirs if compressor is loaded
            if (isCompressorLoaded)
            {
                float rate = isEngineRevved ? fastIdleCompressorRate : idleCompressorRate;
                float delta = rate * dt;

                // Slightly individual charging rates for realism
                primaryPSI = Mathf.Min(150f, primaryPSI + delta);
                secondaryPSI = Mathf.Min(150f, secondaryPSI + delta * 0.98f);

                // Check Governor Cut-Out
                if (primaryPSI >= governorCutOutPSI)
                {
                    primaryPSI = governorCutOutPSI;
                    secondaryPSI = Mathf.Min(governorCutOutPSI, secondaryPSI);
                    isCompressorLoaded = false;
                    OnGovernorCutOut?.Invoke(primaryPSI);
                }

                NotifyPressureChanged();
            }
        }

        private void UpdateAppliedLeakageDynamics(float dt)
        {
            if (!isPedalHeld)
            {
                pedalHoldTimer = 0f;
                initialChamberDropApplied = false;
                return;
            }

            pedalHoldTimer += dt;

            // Immediate initial brake chamber filling drop (~4 to 5 PSI) within first 0.8s
            if (!initialChamberDropApplied && pedalHoldTimer > 0.15f)
            {
                float chamberDrop = UnityEngine.Random.Range(4.5f, 5.5f);
                ConsumeAir(chamberDrop, chamberDrop * 0.97f);
                initialChamberDropApplied = true;
            }

            // Continuous applied leakage rate (PSI/minute converted to PSI/sec)
            float leakRatePerMin = faultInjectExcessiveLeak ? faultLeakRatePerMin : normalAppliedLeakRatePerMin;
            float leakRatePerSec = leakRatePerMin / 60f;
            float drop = leakRatePerSec * dt;

            ConsumeAir(drop, drop);
        }

        private void UpdateAutomaticValvePopOut()
        {
            float checkPressure = Mathf.Min(primaryPSI, secondaryPSI);

            // Red Trailer Air Supply pops out first (around 40 - 45 PSI)
            if (redTrailerPushedIn && checkPressure <= redPopOutThreshold)
            {
                redTrailerPushedIn = false;
                OnValveStateChanged?.Invoke(ValveType.TrailerRed, false);
                OnEmergencyValvePopOut?.Invoke(ValveType.TrailerRed, checkPressure);
            }

            // Yellow Tractor Parking knob pops out second (around 20 - 30 PSI)
            if (yellowBrakePushedIn && checkPressure <= yellowPopOutThreshold)
            {
                yellowBrakePushedIn = false;
                OnValveStateChanged?.Invoke(ValveType.TractorYellow, false);
                OnEmergencyValvePopOut?.Invoke(ValveType.TractorYellow, checkPressure);
            }
        }

        private void CheckLowAirWarning(bool forceNotify)
        {
            // FMCSA: Low air warning requires electrical power (Ignition ACC_ON or RUNNING)
            bool isElectricalActive = (ignitionState == IgnitionState.ACC_ON || ignitionState == IgnitionState.RUNNING);
            bool isPressureLow = (primaryPSI <= lowAirWarningThreshold || secondaryPSI <= lowAirWarningThreshold);
            bool shouldBeActive = isElectricalActive && isPressureLow;

            if (shouldBeActive != isLowAirWarningActive || forceNotify)
            {
                isLowAirWarningActive = shouldBeActive;
                OnLowAirWarningChanged?.Invoke(isLowAirWarningActive);
            }
        }

        private void ConsumeAir(float primaryDrop, float secondaryDrop)
        {
            if (primaryDrop <= 0f && secondaryDrop <= 0f) return;

            primaryPSI = Mathf.Max(0f, primaryPSI - primaryDrop);
            secondaryPSI = Mathf.Max(0f, secondaryPSI - secondaryDrop);

            NotifyPressureChanged();
        }

        #endregion

        #region User Controls & Interactions

        /// <summary>
        /// Fan (pump) the foot service brake pedal. Drops pressure by 8 - 12 PSI per pump.
        /// </summary>
        public void FanPedal()
        {
            float dropPrimary = UnityEngine.Random.Range(minFanDrop, maxFanDrop);
            float dropSecondary = UnityEngine.Random.Range(minFanDrop, maxFanDrop);

            ConsumeAir(dropPrimary, dropSecondary);
            OnPedalFanned?.Invoke((dropPrimary + dropSecondary) * 0.5f);
        }

        /// <summary>
        /// Set whether the foot service brake pedal is currently pressed and held down.
        /// </summary>
        public void SetPedalHeld(bool held)
        {
            if (isPedalHeld == held) return;

            isPedalHeld = held;
            OnPedalStateChanged?.Invoke(isPedalHeld);
        }

        /// <summary>
        /// Set vehicle ignition state: OFF, ACC_ON, RUNNING.
        /// </summary>
        public void SetIgnition(IgnitionState state)
        {
            if (ignitionState == state)
            {
                // Even if already in RUNNING, ensure compressor is loaded if below cut-out
                if (ignitionState == IgnitionState.RUNNING && primaryPSI < governorCutOutPSI)
                {
                    isCompressorLoaded = true;
                }
                return;
            }

            ignitionState = state;
            if (ignitionState == IgnitionState.RUNNING)
            {
                // When starting the engine, compressor loads if pressure is below cut-out
                if (primaryPSI < governorCutOutPSI)
                {
                    isCompressorLoaded = true;
                }
            }
            else
            {
                isEngineRevved = false;
            }

            CheckLowAirWarning(true);
            OnIgnitionChanged?.Invoke(ignitionState);
            NotifyPressureChanged();
        }

        /// <summary>
        /// Set engine fast idle / rev state (1200 - 1500 RPM) for compressor build-up test.
        /// </summary>
        public void SetEngineRevved(bool revved)
        {
            if (ignitionState != IgnitionState.RUNNING)
            {
                isEngineRevved = false;
                return;
            }
            isEngineRevved = revved;
        }

        /// <summary>
        /// Toggle or set Yellow Diamond Parking Brake Valve.
        /// Pushed IN = Release Parking Brake. Popped OUT = Apply Parking Brake.
        /// Cannot latch pushed-in if reservoir pressure is below emergency spring threshold (~20 PSI).
        /// </summary>
        public bool SetYellowValve(bool pushedIn)
        {
            if (pushedIn)
            {
                if (Mathf.Min(primaryPSI, secondaryPSI) < 20f)
                {
                    // Mechanical spring immediately pushes it back out
                    yellowBrakePushedIn = false;
                    OnValveStateChanged?.Invoke(ValveType.TractorYellow, false);
                    OnEmergencyValvePopOut?.Invoke(ValveType.TractorYellow, primaryPSI);
                    return false;
                }
                // Air consumption when charging tractor spring chambers
                ConsumeAir(1.5f, 1.5f);
            }

            yellowBrakePushedIn = pushedIn;
            OnValveStateChanged?.Invoke(ValveType.TractorYellow, yellowBrakePushedIn);
            return true;
        }

        public void ToggleYellowValve()
        {
            SetYellowValve(!yellowBrakePushedIn);
        }

        /// <summary>
        /// Toggle or set Red Octagon Trailer Air Supply Valve.
        /// Pushed IN = Supply Trailer & Release Trailer Spring Brakes. Popped OUT = Emergency Lock.
        /// </summary>
        public bool SetRedValve(bool pushedIn)
        {
            if (pushedIn)
            {
                if (Mathf.Min(primaryPSI, secondaryPSI) < 20f)
                {
                    redTrailerPushedIn = false;
                    OnValveStateChanged?.Invoke(ValveType.TrailerRed, false);
                    OnEmergencyValvePopOut?.Invoke(ValveType.TrailerRed, primaryPSI);
                    return false;
                }
                // Air consumption when charging trailer air lines
                ConsumeAir(2.5f, 2.5f);
            }

            redTrailerPushedIn = pushedIn;
            OnValveStateChanged?.Invoke(ValveType.TrailerRed, redTrailerPushedIn);
            return true;
        }

        public void ToggleRedValve()
        {
            SetRedValve(!redTrailerPushedIn);
        }

        /// <summary>
        /// Fast forward simulated time for the 60-second Applied Leakage Test.
        /// </summary>
        public void FastForwardAppliedLeakage(float durationSeconds)
        {
            if (!isPedalHeld) return;

            float leakRatePerMin = faultInjectExcessiveLeak ? faultLeakRatePerMin : normalAppliedLeakRatePerMin;
            float totalDrop = (leakRatePerMin / 60f) * durationSeconds;

            ConsumeAir(totalDrop, totalDrop);
        }

        /// <summary>
        /// Resets the pneumatic system for a new CDL inspection game:
        /// - Randomizes initial circuit pressures between minPSI and maxPSI (default 90 - 110 PSI)
        /// - Sets realistic slight circuit pressure differential
        /// - Pops out parking and supply valves
        /// - Resets compressor loading state so compressor charges when engine starts
        /// </summary>
        public void ResetForNewGame(float minPSI = 90f, float maxPSI = 110f)
        {
            // Randomize spread between 90 - 110 PSI
            float baseP = UnityEngine.Random.Range(minPSI, maxPSI);
            float diff = UnityEngine.Random.Range(-3.0f, 3.0f);

            primaryPSI = Mathf.Clamp(baseP, minPSI, maxPSI);
            secondaryPSI = Mathf.Clamp(baseP + diff, minPSI, maxPSI);

            // Round to 1 decimal place for clean telemetry and crisp needle angles
            primaryPSI = Mathf.Round(primaryPSI * 10f) / 10f;
            secondaryPSI = Mathf.Round(secondaryPSI * 10f) / 10f;

            yellowBrakePushedIn = false;
            redTrailerPushedIn = false;
            isPedalHeld = false;
            isEngineRevved = false;

            // Since starting pressure (90-110 PSI) is below governor cut-out (~126 PSI), compressor will charge when engine runs
            isCompressorLoaded = (primaryPSI < governorCutOutPSI);

            CheckLowAirWarning(true);
            NotifyStateChanged();

            Debug.Log($"<color=#38bdf8><b>[AirBrakeSystem] New Game initialized with randomized circuit pressures: Primary = {primaryPSI:F1} PSI, Secondary = {secondaryPSI:F1} PSI (Spread: {minPSI}-{maxPSI} PSI)</b></color>");
        }

        /// <summary>
        /// Reset air pressure for new game start with random circuit pressures (90-110 PSI).
        /// </summary>
        public void ResetToFullCharge()
        {
            ResetForNewGame(newGameMinPSI, newGameMaxPSI);
        }

        /// <summary>
        /// Direct setter for testing/staging specific pressures.
        /// </summary>
        public void SetPressuresDirectly(float pPSI, float sPSI)
        {
            primaryPSI = Mathf.Clamp(pPSI, 0f, 150f);
            secondaryPSI = Mathf.Clamp(sPSI, 0f, 150f);
            CheckLowAirWarning(true);
            NotifyPressureChanged();
        }

        #endregion

        #region Helpers

        private void NotifyPressureChanged()
        {
            OnPressureChanged?.Invoke(primaryPSI, secondaryPSI);
        }

        private void NotifyStateChanged()
        {
            NotifyPressureChanged();
            OnIgnitionChanged?.Invoke(ignitionState);
            OnValveStateChanged?.Invoke(ValveType.TractorYellow, yellowBrakePushedIn);
            OnValveStateChanged?.Invoke(ValveType.TrailerRed, redTrailerPushedIn);
            OnPedalStateChanged?.Invoke(isPedalHeld);
        }

        #endregion
    }
}
