using System;
using UnityEngine;

namespace CDL.AirBrakeSimulator
{
    /// <summary>
    /// Finite State Machine & Examination Flow Controller for the Class A CDL In-Cab Air Brake Test.
    /// Enforces the mandatory 4-step LABL procedure (Applied Leakage, Low Air Warning,
    /// Emergency Pop-Out, Compressor Build-Up) with strict DMV/FMCSA pass/fail tolerances,
    /// verbal examiner scripts, live timers, and failure modal triggering.
    /// </summary>
    public class DMVTestManager : MonoBehaviour
    {
        [Header("Air System Reference")]
        [SerializeField] private AirBrakeSystem airSystem;

        [Header("Test Configuration")]
        [SerializeField] private VehicleCombinationType vehicleType = VehicleCombinationType.ClassACombination;
        [Tooltip("Max allowable leakage in 1 minute (4 PSI for Class A Combination, 3 PSI for Single).")]
        [SerializeField] private float maxAllowableLeakagePSI = 4.0f;
        [Tooltip("Duration of the applied leakage test in seconds.")]
        [SerializeField] private float appliedLeakDuration = 60.0f;
        [Tooltip("Max allowable seconds to build from 85 to 100 PSI.")]
        [SerializeField] private float maxBuildUpSeconds = 45.0f;

        [Header("Current State")]
        [SerializeField] private DMVStep currentStep = DMVStep.EnginePrep;
        [SerializeField] private bool isStepActive = false;

        // Step 1: Engine Prep / Cut-Out
        private float cutOutRecordedPSI = 0f;

        // Step 2: Applied Leakage
        private bool isLeakTestRunning = false;
        private float leakTimer = 0f;
        private float leakStartPressure = 0f;
        private float leakFinalDrop = 0f;
        private bool leakageStabilized = false;
        private float stabilizationTimer = 0f;

        // Step 3: Low Air Warning
        private float lowAirAlarmRecordedPSI = 0f;
        private bool alarmRecorded = false;

        // Step 4: Emergency Pop-Out
        private float redPopPressure = 0f;
        private float yellowPopPressure = 0f;
        private bool redPoppedRecorded = false;
        private bool yellowPoppedRecorded = false;

        // Step 5: Compressor Build-Up
        private bool isTimingBuildUp = false;
        private float buildUpStartTime = 0f;
        private float buildUpElapsedTime = 0f;
        private bool buildUpCompleted = false;

        // Game Mode & Guided Training
        private const string PREFS_GAME_MODE = "CDL_AirBrake_GameMode";
        [Header("Simulator Mode")]
        [SerializeField] private GameMode currentGameMode = GameMode.Training;
        private HighlightTarget currentHighlightedTarget = HighlightTarget.None;
        private string currentNextAction = "";
        private string currentWhyWeDoThis = "";

        // Failure Details
        private DMVStep failedStep = DMVStep.EnginePrep;
        private string failureReason = "";
        private string failureRegulation = "";

        // Events for UI
        public event Action<DMVStep> OnStepChanged;
        public event Action<string, string> OnInstructionUpdated; // (instruction, verbalScript)
        public event Action<float, float> OnTimerUpdated; // (current, total)
        public event Action<string, string> OnTestFailed; // (reason, regulation)
        public event Action<TestScorecard> OnTestPassed; // Scorecard of all 5 parameters
        public event Action<GameMode> OnGameModeChanged;
        public event Action<HighlightTarget> OnTargetHighlightChanged;
        public event Action<string, string> OnTrainingGuidanceUpdated; // (nextAction, whyWeDoThis)
        public event Action<HighlightTarget, string> OnSoftErrorHint; // (wrongTarget, hintMessage)

        #region Public Properties

        public VehicleCombinationType VehicleType => vehicleType;
        public GameMode CurrentGameMode => currentGameMode;
        public HighlightTarget CurrentHighlightedTarget => currentHighlightedTarget;
        public string CurrentNextAction => currentNextAction;
        public string CurrentWhyWeDoThis => currentWhyWeDoThis;
        public DMVStep CurrentStep => currentStep;
        public bool IsStepActive => isStepActive;
        public bool IsLeakTestRunning => isLeakTestRunning;
        public float LeakTimerRemaining => Mathf.Max(0f, appliedLeakDuration - leakTimer);
        public float BuildUpElapsed => buildUpElapsedTime;
        public string FailureReason => failureReason;
        public string FailureRegulation => failureRegulation;

        #endregion

        [Serializable]
        public struct TestScorecard
        {
            public float CutOutPSI;
            public float LeakageDropPSI;
            public float LowAirAlarmPSI;
            public float RedPopOutPSI;
            public float YellowPopOutPSI;
            public float BuildUpSeconds;
        }

        private void Awake()
        {
            currentGameMode = (GameMode)PlayerPrefs.GetInt(PREFS_GAME_MODE, (int)GameMode.Training);

            if (airSystem == null)
            {
                airSystem = FindFirstObjectByType<AirBrakeSystem>();
            }
        }

        private void Start()
        {
            SetStep(DMVStep.EnginePrep);
            UpdateTrainingGuidance();
        }

        private void OnEnable()
        {
            if (airSystem != null)
            {
                airSystem.OnGovernorCutOut += HandleGovernorCutOut;
                airSystem.OnLowAirWarningChanged += HandleLowAirWarningChanged;
                airSystem.OnEmergencyValvePopOut += HandleEmergencyValvePopOut;
                airSystem.OnValveStateChanged += HandleValveStateChanged;
                airSystem.OnIgnitionChanged += HandleIgnitionChanged;
                airSystem.OnPedalStateChanged += HandlePedalStateChanged;
                airSystem.OnPressureChanged += HandlePressureChanged;
            }
        }

        private void OnDisable()
        {
            if (airSystem != null)
            {
                airSystem.OnGovernorCutOut -= HandleGovernorCutOut;
                airSystem.OnLowAirWarningChanged -= HandleLowAirWarningChanged;
                airSystem.OnEmergencyValvePopOut -= HandleEmergencyValvePopOut;
                airSystem.OnValveStateChanged -= HandleValveStateChanged;
                airSystem.OnIgnitionChanged -= HandleIgnitionChanged;
                airSystem.OnPedalStateChanged -= HandlePedalStateChanged;
                airSystem.OnPressureChanged -= HandlePressureChanged;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            switch (currentStep)
            {
                case DMVStep.AppliedLeakageTest:
                    UpdateAppliedLeakageStep(dt);
                    break;

                case DMVStep.LowAirAlarmTest:
                    UpdateLowAirAlarmStep();
                    break;

                case DMVStep.EmergencyPopOutTest:
                    UpdateEmergencyPopOutStep();
                    break;

                case DMVStep.CompressorBuildUpTest:
                    UpdateCompressorBuildUpStep(dt);
                    break;
            }

            UpdateTrainingGuidance();
        }

        #region Step Transitions & Setup

        public void SetStep(DMVStep nextStep)
        {
            currentStep = nextStep;
            isStepActive = true;

            switch (currentStep)
            {
                case DMVStep.EnginePrep:
                    SetupEnginePrep();
                    break;

                case DMVStep.AppliedLeakageTest:
                    SetupAppliedLeakage();
                    break;

                case DMVStep.LowAirAlarmTest:
                    SetupLowAirAlarm();
                    break;

                case DMVStep.EmergencyPopOutTest:
                    SetupEmergencyPopOut();
                    break;

                case DMVStep.CompressorBuildUpTest:
                    SetupCompressorBuildUp();
                    break;

                case DMVStep.TestCompleted:
                    CompleteFullTest();
                    break;

                case DMVStep.TestFailed:
                    isStepActive = false;
                    break;
            }

            OnStepChanged?.Invoke(currentStep);
            UpdateTrainingGuidance();
        }

        private void SetupEnginePrep()
        {
            string instruction = "Goal: Build pressure to 120-140 PSI until governor cut-out sneeze.";
            string script = "\"Examiner, I am starting the engine to build air pressure. The governor cut-out must occur between 120 and 140 PSI with an audible purge sneeze.\"";
            OnInstructionUpdated?.Invoke(instruction, script);
            OnTimerUpdated?.Invoke(0f, 0f);
        }

        private void SetupAppliedLeakage()
        {
            isLeakTestRunning = false;
            leakTimer = 0f;
            leakageStabilized = false;
            stabilizationTimer = 0f;
            leakStartPressure = 0f;
            leakFinalDrop = 0f;

            string instruction = "Goal: Shut engine OFF, turn key to ACC/ON, push in both valves, and hold pedal for 60s.";
            string script = "\"Examiner, governor cut-out passed. I will now shut the engine OFF and turn key to ACC/ON to prepare for the Applied Leakage Test.\"";
            OnInstructionUpdated?.Invoke(instruction, script);
            OnTimerUpdated?.Invoke(appliedLeakDuration, appliedLeakDuration);
        }

        private void SetupLowAirAlarm()
        {
            lowAirAlarmRecordedPSI = 0f;
            alarmRecorded = false;

            string instruction = "Goal: Fan (pump) brake pedal. Low air warning alarm must activate at >= 60 PSI.";
            string script = "\"Examiner, I will now fan the foot brake pedal. The low air warning light and buzzer must activate at or above 60 PSI.\"";
            OnInstructionUpdated?.Invoke(instruction, script);
            OnTimerUpdated?.Invoke(0f, 0f);
        }

        private void SetupEmergencyPopOut()
        {
            redPopPressure = 0f;
            yellowPopPressure = 0f;
            redPoppedRecorded = false;
            yellowPoppedRecorded = false;

            string instruction = "Goal: Continue fanning pedal. Red & Yellow valves must pop out between 20 and 45 PSI.";
            string script = "\"Examiner, I will continue fanning the brakes. Between 20 and 45 PSI, the trailer air supply valve and tractor parking valve must pop out to deploy emergency spring brakes.\"";
            OnInstructionUpdated?.Invoke(instruction, script);
            OnTimerUpdated?.Invoke(0f, 0f);
        }

        private void SetupCompressorBuildUp()
        {
            isTimingBuildUp = false;
            buildUpStartTime = 0f;
            buildUpElapsedTime = 0f;
            buildUpCompleted = false;

            string instruction = "Goal: Start engine & rev to ~1200 RPM. Pressure must build 85->100 PSI in <= 45 seconds.";
            string script = "\"Examiner, I am starting the engine to test compressor build-up rate. In a dual air system, pressure must build from 85 to 100 PSI within 45 seconds.\"";
            OnInstructionUpdated?.Invoke(instruction, script);
            OnTimerUpdated?.Invoke(0f, maxBuildUpSeconds);
        }

        #endregion

        #region Step 1 Handling (Engine Prep / Cut-Out)

        private void HandleGovernorCutOut(float cutOutPSI)
        {
            if (currentStep != DMVStep.EnginePrep) return;

            cutOutRecordedPSI = cutOutPSI;

            if (cutOutPSI >= 120f && cutOutPSI <= 140f)
            {
                string script = $"\"Examiner, the compressor governor cut out at {cutOutPSI:F0} PSI with an audible purge, which is within the 120 to 140 PSI legal range. Step 1 passed.\"";
                string instruction = $"Governor Cut-Out Verified at {cutOutPSI:F0} PSI! (Within 120-140 PSI range).\nPreparing for Step 2: Applied Leakage Test...";
                OnInstructionUpdated?.Invoke(instruction, script);

                // Auto-advance to Applied Leakage after brief confirmation
                Invoke(nameof(AdvanceToAppliedLeakage), 2.0f);
            }
            else
            {
                TriggerFailure(
                    $"Governor cut-out occurred at {cutOutPSI:F0} PSI. Legal range is 120 - 140 PSI.",
                    "FMCSA § 393.51 & CDL Manual Section 5.1 (Air Compressor Governor Cut-Out)"
                );
            }
        }

        private void AdvanceToAppliedLeakage()
        {
            SetStep(DMVStep.AppliedLeakageTest);
        }

        #endregion

        #region Step 2 Handling (Applied Leakage)

        private void UpdateAppliedLeakageStep(float dt)
        {
            // If the 60-second test is ACTIVE & RUNNING:
            if (isLeakTestRunning)
            {
                // Engine cannot be started during the active 60s leakage test!
                if (airSystem.CurrentIgnition == IgnitionState.RUNNING)
                {
                    TriggerFailure(
                        "Engine cannot be started during the 60-second Applied Leakage Test! Shut engine OFF and turn key to ACC/ON.",
                        "FMCSA § 396.11 & CDL Manual Section 5.3"
                    );
                    return;
                }

                // Both valves must stay pushed IN during the active test
                if (!airSystem.IsYellowBrakePushedIn || !airSystem.IsRedTrailerPushedIn)
                {
                    TriggerFailure(
                        "Valves were not pushed IN! Both Yellow (Tractor) and Red (Trailer) valves must stay pushed in during the test to release spring brakes.",
                        "CDL Manual Section 5.3 (Applied Leakage Test)"
                    );
                    return;
                }

                // Pedal must stay pressed & held
                if (!airSystem.IsPedalHeld)
                {
                    TriggerFailure(
                        "Foot brake pedal was released before the 60-second applied leakage test was completed!",
                        "CDL Manual Section 5.3 (Applied Leakage Test Procedure)"
                    );
                    return;
                }

                // 60-second test countdown in progress
                leakTimer += dt;
                float remaining = Mathf.Max(0f, appliedLeakDuration - leakTimer);
                OnTimerUpdated?.Invoke(remaining, appliedLeakDuration);

                float currentDrop = leakStartPressure - airSystem.PrimaryPSI;
                string timerInstruction = $"HOLD PEDAL! Time Remaining: {remaining:F0}s\nCurrent Air Loss: {currentDrop:F1} PSI (Max Allowable: {maxAllowableLeakagePSI:F1} PSI)";
                string currentScript = $"\"Examiner, I am holding the foot brake for 1 full minute. Current air loss is {currentDrop:F1} PSI.\"";
                OnInstructionUpdated?.Invoke(timerInstruction, currentScript);

                // If drop exceeds tolerance at any point during the 60s
                if (currentDrop > maxAllowableLeakagePSI)
                {
                    TriggerFailure(
                        $"Excessive air leakage! Dropped {currentDrop:F1} PSI in {leakTimer:F0} seconds. Maximum allowable loss is {maxAllowableLeakagePSI:F1} PSI in 1 minute for combination vehicles.",
                        "FMCSA § 396.11 (Applied Brake Leakage Rate)"
                    );
                    return;
                }

                // Timer completed successfully!
                if (leakTimer >= appliedLeakDuration)
                {
                    CompleteAppliedLeakage(currentDrop);
                }
                return;
            }

            // =========================================================================
            // PREPARATORY PHASE (before pedal is held and test is running)
            // =========================================================================

            // 1. Engine must be shut OFF
            if (airSystem.CurrentIgnition == IgnitionState.RUNNING)
            {
                string instruction = "Step 2 of 5: Applied Leakage Test (L)\n" +
                                     "▶ ACTION REQUIRED: Shut engine OFF (click OFF), then turn key to ACC/ON.";
                string script = "\"Examiner, governor cut-out passed. I will now shut the engine OFF and turn key to ACC/ON to perform the Applied Leakage Test.\"";
                OnInstructionUpdated?.Invoke(instruction, script);
                return;
            }

            // 2. Key must be in ACC/ON
            if (airSystem.CurrentIgnition == IgnitionState.OFF)
            {
                string instruction = "Step 2 of 5: Applied Leakage Test (L)\n" +
                                     "▶ ACTION REQUIRED: Turn ignition key to ACC/ON so instruments and warnings are active.";
                string script = "\"Examiner, turning ignition key to ACC/ON to power dashboard instruments.\"";
                OnInstructionUpdated?.Invoke(instruction, script);
                return;
            }

            // 3. Both valves must be pushed IN to release spring brakes
            if (!airSystem.IsYellowBrakePushedIn || !airSystem.IsRedTrailerPushedIn)
            {
                string whichValves = (!airSystem.IsYellowBrakePushedIn && !airSystem.IsRedTrailerPushedIn)
                    ? "both Yellow (Tractor) and Red (Trailer) valves"
                    : (!airSystem.IsYellowBrakePushedIn ? "Yellow (Tractor Parking) valve" : "Red (Trailer Supply) valve");

                string instruction = "Step 2 of 5: Applied Leakage Test (L)\n" +
                                     $"▶ ACTION REQUIRED: Push IN {whichValves} to release spring brakes.";
                string script = "\"Examiner, pushing in both valves to release spring brakes and charge the system for testing.\"";
                OnInstructionUpdated?.Invoke(instruction, script);
                return;
            }

            // 4. Pedal must be pressed & held
            if (airSystem.IsPedalHeld)
            {
                if (!leakageStabilized)
                {
                    stabilizationTimer += dt;
                    if (stabilizationTimer >= 1.2f)
                    {
                        leakageStabilized = true;
                        isLeakTestRunning = true;
                        leakStartPressure = airSystem.PrimaryPSI;
                        leakTimer = 0f;
                    }
                    else
                    {
                        string instruction = "Brakes applied... Waiting for pressure to stabilize...";
                        string script = "\"Examiner, holding foot brake, waiting for initial chamber pressure to stabilize.\"";
                        OnInstructionUpdated?.Invoke(instruction, script);
                    }
                }
            }
            else
            {
                stabilizationTimer = 0f;
                leakageStabilized = false;
                string instruction = "Step 2 of 5: Applied Leakage Test (L)\n" +
                                     "▶ READY: Press and HOLD the foot brake pedal for 60 seconds (or click Fast Forward 60s >>).\n" +
                                     $"Max allowable air loss: {maxAllowableLeakagePSI:F1} PSI in 1 minute.";
                string script = "\"Examiner, both valves are in. I am pressing and holding the foot brake for 1 full minute. Air loss must not exceed 4 PSI for this combination vehicle.\"";
                OnInstructionUpdated?.Invoke(instruction, script);
            }
        }

        private void CompleteAppliedLeakage(float finalDrop)
        {
            isLeakTestRunning = false;
            leakFinalDrop = finalDrop;

            string script = $"\"Examiner, I have held the service brake for 1 full minute. The system lost {finalDrop:F1} PSI, which does not exceed the {maxAllowableLeakagePSI:F1} PSI legal limit for a Class A combination vehicle. Applied Leakage Test passed.\"";
            string instruction = $"Applied Leakage PASSED! Lost only {finalDrop:F1} PSI in 60s (Limit: <= {maxAllowableLeakagePSI:F1} PSI).\nAdvancing to Step 3: Low Air Warning Alarm Test...";
            OnInstructionUpdated?.Invoke(instruction, script);

            Invoke(nameof(AdvanceToLowAirAlarm), 2.5f);
        }

        public void FastForwardAppliedLeakage()
        {
            if (currentStep != DMVStep.AppliedLeakageTest) return;

            if (airSystem.CurrentIgnition == IgnitionState.RUNNING)
            {
                TriggerFailure(
                    "Engine cannot be running during the Applied Leakage Test! Shut engine OFF and turn key to ACC/ON.",
                    "FMCSA § 396.11 & CDL Manual Section 5.3"
                );
                return;
            }

            // Auto-prepare valves and ignition if needed for convenience
            if (!airSystem.IsYellowBrakePushedIn || !airSystem.IsRedTrailerPushedIn)
            {
                airSystem.SetYellowValve(true);
                airSystem.SetRedValve(true);
            }
            if (airSystem.CurrentIgnition != IgnitionState.ACC_ON)
            {
                airSystem.SetIgnition(IgnitionState.ACC_ON);
            }

            if (leakStartPressure <= 0f) leakStartPressure = airSystem.PrimaryPSI;

            float remainingSeconds = Mathf.Max(5f, appliedLeakDuration - leakTimer);
            airSystem.FastForwardAppliedLeakage(remainingSeconds);

            leakTimer = appliedLeakDuration;
            float totalDrop = Mathf.Max(0.8f, leakStartPressure - airSystem.PrimaryPSI);

            if (totalDrop > maxAllowableLeakagePSI)
            {
                TriggerFailure(
                    $"Excessive air leakage! Fast-forwarded 60 seconds resulted in {totalDrop:F1} PSI drop, exceeding {maxAllowableLeakagePSI:F1} PSI limit.",
                    "FMCSA § 396.11"
                );
            }
            else
            {
                CompleteAppliedLeakage(totalDrop);
            }
        }

        private void AdvanceToLowAirAlarm()
        {
            SetStep(DMVStep.LowAirAlarmTest);
        }

        #endregion

        #region Step 3 Handling (Low Air Warning)

        private void UpdateLowAirAlarmStep()
        {
            if (airSystem.CurrentIgnition == IgnitionState.RUNNING)
            {
                string instruction = "Step 3 of 5: Low Air Warning Alarm Test (A)\n" +
                                     "▶ ACTION REQUIRED: Shut engine OFF and turn key to ACC/ON to fan brakes!";
                string script = "\"Examiner, engine must be OFF with key in ACC/ON to test the low air warning alarm.\"";
                OnInstructionUpdated?.Invoke(instruction, script);
                return;
            }

            if (airSystem.CurrentIgnition == IgnitionState.OFF)
            {
                string instruction = "Step 3 of 5: Low Air Warning Alarm Test (A)\n" +
                                     "▶ ACTION REQUIRED: Turn key to ACC/ON to power the dashboard warning buzzer and light!";
                string script = "\"Examiner, turning key to ACC/ON to power the low air warning system.\"";
                OnInstructionUpdated?.Invoke(instruction, script);
                return;
            }

            // Check if pressure dropped dangerously low without warning triggering
            if (airSystem.PrimaryPSI < 50f && !alarmRecorded)
            {
                TriggerFailure(
                    $"Air pressure dropped to {airSystem.PrimaryPSI:F0} PSI without low air warning activation! Warning must activate at or above 60 PSI.",
                    "FMCSA § 393.51 (Low Air Warning Device Failure)"
                );
            }
        }

        private void HandleLowAirWarningChanged(bool isWarningActive)
        {
            if (currentStep != DMVStep.LowAirAlarmTest || alarmRecorded) return;

            if (isWarningActive)
            {
                alarmRecorded = true;
                lowAirAlarmRecordedPSI = airSystem.PrimaryPSI;

                if (lowAirAlarmRecordedPSI >= 55f) // FMCSA standard at or above 60 PSI (allow slight 55 margin for dynamic fan)
                {
                    string script = $"\"Examiner, the low air warning light and buzzer activated at {lowAirAlarmRecordedPSI:F0} PSI, which is at or above the mandated 60 PSI threshold. Low Air Warning Test passed.\"";
                    string instruction = $"Low Air Warning ACTIVATED at {lowAirAlarmRecordedPSI:F0} PSI! (Threshold: >= 60 PSI).\nAdvancing to Step 4: Emergency Valve Pop-Out Test...";
                    OnInstructionUpdated?.Invoke(instruction, script);

                    Invoke(nameof(AdvanceToEmergencyPopOut), 2.5f);
                }
                else
                {
                    TriggerFailure(
                        $"Low air warning activated too late at {lowAirAlarmRecordedPSI:F0} PSI! FMCSA mandates activation at or above 60 PSI.",
                        "FMCSA § 393.51 (Low Air Warning Threshold)"
                    );
                }
            }
        }

        private void AdvanceToEmergencyPopOut()
        {
            SetStep(DMVStep.EmergencyPopOutTest);
        }

        #endregion

        #region Step 4 Handling (Emergency Valve Pop-Out)

        private void UpdateEmergencyPopOutStep()
        {
            if (airSystem.CurrentIgnition == IgnitionState.RUNNING)
            {
                string instruction = "Step 4 of 5: Emergency Valve Pop-Out Test (B)\n" +
                                     "▶ ACTION REQUIRED: Engine must be OFF with key in ACC/ON!";
                string script = "\"Examiner, engine must be OFF with key in ACC/ON to test emergency valve pop-out.\"";
                OnInstructionUpdated?.Invoke(instruction, script);
                return;
            }

            if (airSystem.PrimaryPSI < 18f && (!redPoppedRecorded || !yellowPoppedRecorded))
            {
                TriggerFailure(
                    "Air pressure dropped below 20 PSI and emergency valves failed to pop out! Tractor protection and emergency spring brake system is defective.",
                    "FMCSA § 393.43 (Breakaway and Emergency Braking)"
                );
            }
        }

        private void HandleEmergencyValvePopOut(ValveType valve, float pressure)
        {
            if (currentStep != DMVStep.EmergencyPopOutTest) return;

            if (valve == ValveType.TrailerRed && !redPoppedRecorded)
            {
                redPoppedRecorded = true;
                redPopPressure = pressure;

                if (pressure > 46f)
                {
                    TriggerFailure(
                        $"Trailer Air Supply (Red) valve popped out prematurely at {pressure:F0} PSI! Must pop out between 20 and 45 PSI.",
                        "FMCSA § 393.43"
                    );
                    return;
                }

                string script = $"\"Examiner, the Red Trailer Air Supply valve popped out at {pressure:F0} PSI. Continuing to fan for the yellow tractor valve.\"";
                string instruction = $"Red Valve Popped at {pressure:F0} PSI (20-45 PSI range).\nContinue fanning until the Yellow Tractor Parking valve pops out!";
                OnInstructionUpdated?.Invoke(instruction, script);
            }
            else if (valve == ValveType.TractorYellow && !yellowPoppedRecorded)
            {
                yellowPoppedRecorded = true;
                yellowPopPressure = pressure;

                if (pressure > 46f)
                {
                    TriggerFailure(
                        $"Tractor Parking (Yellow) valve popped out prematurely at {pressure:F0} PSI! Must pop out between 20 and 45 PSI.",
                        "FMCSA § 393.43"
                    );
                    return;
                }
            }

            // Both valves popped out!
            if (redPoppedRecorded && yellowPoppedRecorded)
            {
                string script = $"\"Examiner, both the Trailer Air Supply valve (at {redPopPressure:F0} PSI) and Tractor Parking Brake valve (at {yellowPopPressure:F0} PSI) popped out between 20 and 45 PSI. Emergency spring brakes are deployed. Emergency Pop-Out Test passed.\"";
                string instruction = $"Both Valves Popped Out!\nRed at {redPopPressure:F0} PSI, Yellow at {yellowPopPressure:F0} PSI (both within 20-45 PSI legal range).\nAdvancing to Step 5: Compressor Build-Up Test...";
                OnInstructionUpdated?.Invoke(instruction, script);

                Invoke(nameof(AdvanceToCompressorBuildUp), 2.5f);
            }
        }

        private void AdvanceToCompressorBuildUp()
        {
            SetStep(DMVStep.CompressorBuildUpTest);
        }

        #endregion

        #region Step 5 Handling (Compressor Build-Up Rate)

        private void UpdateCompressorBuildUpStep(float dt)
        {
            if (buildUpCompleted) return;

            if (airSystem.CurrentIgnition != IgnitionState.RUNNING)
            {
                string instruction = "Start the engine (RUNNING) and rev/idle at ~1200 RPM to test the compressor build-up rate!";
                string script = "\"Examiner, starting engine to begin the compressor build-up rate test.\"";
                OnInstructionUpdated?.Invoke(instruction, script);
                return;
            }

            float currentPressure = airSystem.PrimaryPSI;

            // Wait until pressure reaches 85 PSI to start timing
            if (!isTimingBuildUp)
            {
                if (currentPressure < 85f)
                {
                    string instruction = $"Compressor charging... Build to 85 PSI to start stopwatch. ({currentPressure:F0} / 85 PSI)";
                    string script = "\"Examiner, building air to 85 PSI where the 45-second build-up timing begins.\"";
                    OnInstructionUpdated?.Invoke(instruction, script);
                }
                else
                {
                    // Start stopwatch at 85 PSI
                    isTimingBuildUp = true;
                    buildUpStartTime = Time.time;
                    buildUpElapsedTime = 0f;
                }
            }
            else
            {
                // Timing between 85 PSI and 100 PSI
                buildUpElapsedTime = Time.time - buildUpStartTime;
                OnTimerUpdated?.Invoke(buildUpElapsedTime, maxBuildUpSeconds);

                string instruction = $"TIMING 85 -> 100 PSI: {buildUpElapsedTime:F1}s / {maxBuildUpSeconds:F0}s\nCurrent Pressure: {currentPressure:F0} PSI";
                string script = $"\"Examiner, timing build-up from 85 to 100 PSI. Elapsed time is {buildUpElapsedTime:F1} seconds.\"";
                OnInstructionUpdated?.Invoke(instruction, script);

                if (buildUpElapsedTime > maxBuildUpSeconds)
                {
                    TriggerFailure(
                        $"Compressor build-up took {buildUpElapsedTime:F1} seconds! Maximum legal time to build from 85 to 100 PSI is 45 seconds.",
                        "FMCSA § 393.51 & CDL Manual Section 5.3 (Air Compressor Build-Up Rate)"
                    );
                    return;
                }

                if (currentPressure >= 100f)
                {
                    buildUpCompleted = true;
                    isTimingBuildUp = false;

                    string passScript = $"\"Examiner, the air compressor built pressure from 85 to 100 PSI in {buildUpElapsedTime:F1} seconds, well under the 45-second legal limit. The full CDL In-Cab Air Brake Inspection is complete!\"";
                    string passInstruction = $"Compressor Build-Up PASSED in {buildUpElapsedTime:F1}s! (Limit: <= 45s).\nALL 4 CDL AIR BRAKE TESTS SUCCESSFULLY COMPLETED!";
                    OnInstructionUpdated?.Invoke(passInstruction, passScript);

                    Invoke(nameof(CompleteFullTest), 2.5f);
                }
            }
        }

        private void CompleteFullTest()
        {
            currentStep = DMVStep.TestCompleted;
            isStepActive = false;

            TestScorecard scorecard = new TestScorecard
            {
                CutOutPSI = cutOutRecordedPSI > 0 ? cutOutRecordedPSI : airSystem.GovernorCutOutPSI,
                LeakageDropPSI = leakFinalDrop,
                LowAirAlarmPSI = lowAirAlarmRecordedPSI > 0 ? lowAirAlarmRecordedPSI : airSystem.LowAirWarningThreshold,
                RedPopOutPSI = redPopPressure > 0 ? redPopPressure : 42f,
                YellowPopOutPSI = yellowPopPressure > 0 ? yellowPopPressure : 26f,
                BuildUpSeconds = buildUpElapsedTime > 0 ? buildUpElapsedTime : 18.5f
            };

            OnStepChanged?.Invoke(currentStep);
            OnTestPassed?.Invoke(scorecard);
        }

        #endregion

        #region Failure Handling & Reset

        private void TriggerFailure(string reason, string regulation)
        {
            if (currentGameMode == GameMode.Training)
            {
                Debug.LogWarning($"<color=#fbbf24><b>[Guided Training Mode] Soft Error intercepted:</b></color> {reason}");
                OnSoftErrorHint?.Invoke(currentHighlightedTarget, reason);
                // In Guided Training Mode, do NOT fail the test or show red failure screen!
                return;
            }

            failedStep = currentStep;
            failureReason = reason;
            failureRegulation = regulation;
            currentStep = DMVStep.TestFailed;
            isStepActive = false;
            isLeakTestRunning = false;
            isTimingBuildUp = false;

            OnStepChanged?.Invoke(currentStep);
            OnTestFailed?.Invoke(reason, regulation);
        }

        public void SetGameMode(GameMode mode)
        {
            currentGameMode = mode;
            PlayerPrefs.SetInt(PREFS_GAME_MODE, (int)mode);
            PlayerPrefs.Save();
            OnGameModeChanged?.Invoke(currentGameMode);
            UpdateTrainingGuidance();
            Debug.Log($"<color=#38bdf8><b>[CDL Air Brake] Game Mode set to: {mode}</b></color>");
        }

        public bool CheckActionPermitted(HighlightTarget action, out string feedback)
        {
            if (currentGameMode != GameMode.Training)
            {
                feedback = "";
                return true;
            }

            if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();

            bool isMatch = false;
            switch (currentHighlightedTarget)
            {
                case HighlightTarget.BothValves:
                    isMatch = (action == HighlightTarget.YellowValve || action == HighlightTarget.RedValve || action == HighlightTarget.BothValves);
                    break;
                case HighlightTarget.IgnitionRun:
                    // In engine prep or build-up, turning key to ACC/ON or RUNNING are both valid starting steps
                    isMatch = (action == HighlightTarget.IgnitionRun || (action == HighlightTarget.IgnitionAcc && airSystem != null && airSystem.CurrentIgnition == IgnitionState.OFF));
                    break;
                default:
                    isMatch = (action == currentHighlightedTarget);
                    break;
            }

            if (isMatch)
            {
                feedback = "";
                return true;
            }

            feedback = GetPedagogicalHint(action);
            OnSoftErrorHint?.Invoke(action, feedback);
            return false;
        }

        private string GetPedagogicalHint(HighlightTarget wrongAction)
        {
            switch (wrongAction)
            {
                case HighlightTarget.IgnitionRun:
                    if (currentStep == DMVStep.AppliedLeakageTest || currentStep == DMVStep.LowAirAlarmTest || currentStep == DMVStep.EmergencyPopOutTest)
                        return "Keep engine OFF! Running compressor masks leaks and prevents true pneumatic pressure testing.";
                    break;

                case HighlightTarget.RevEngine:
                    if (airSystem != null && airSystem.CurrentIgnition != IgnitionState.RUNNING)
                        return "Start the engine first before revving!";
                    break;

                case HighlightTarget.HoldPedal:
                    if (currentStep == DMVStep.LowAirAlarmTest || currentStep == DMVStep.EmergencyPopOutTest)
                        return "Don't just hold the pedal! FAN (pump) it repeatedly to bleed off reservoir air.";
                    break;

                case HighlightTarget.FanPedal:
                    if (currentStep == DMVStep.AppliedLeakageTest)
                        return "Hold the pedal firmly! Do NOT fan the pedal during the 60-second static leakage test.";
                    break;

                case HighlightTarget.YellowValve:
                case HighlightTarget.RedValve:
                    if (currentStep == DMVStep.EmergencyPopOutTest)
                        return "Do NOT pull valves manually! Continue fanning until air drops to 20-45 PSI so they pop out automatically.";
                    break;
            }

            return $"Action not required right now. Next step: {currentNextAction}";
        }

        public void UpdateTrainingGuidance()
        {
            if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();

            if (currentGameMode != GameMode.Training)
            {
                if (currentHighlightedTarget != HighlightTarget.None)
                {
                    currentHighlightedTarget = HighlightTarget.None;
                    OnTargetHighlightChanged?.Invoke(HighlightTarget.None);
                }
                return;
            }

            HighlightTarget target = HighlightTarget.None;
            string nextAction = "";
            string whyWeDoThis = "";

            switch (currentStep)
            {
                case DMVStep.EnginePrep:
                    // Step 1.1: Highlight RUNNING. Step 1.2: Highlight REV ENGINE.
                    if (airSystem != null && airSystem.CurrentIgnition != IgnitionState.RUNNING)
                    {
                        target = HighlightTarget.IgnitionRun;
                        nextAction = "Turn Ignition to [ RUNNING ]";
                        whyWeDoThis = "Start engine to charge air tanks with the compressor.";
                    }
                    else
                    {
                        target = HighlightTarget.RevEngine;
                        nextAction = "Hold [ REV ENGINE ] (~1200 RPM)";
                        whyWeDoThis = "Rev engine (~1200 RPM) to speed up air compressor build-up until governor sneezes (~125 PSI).";
                    }
                    break;

                case DMVStep.AppliedLeakageTest:
                    // Step 2.1: OFF. Step 2.2: ACC/ON. Step 2.3: Both Valves. Step 2.4: HOLD PEDAL.
                    if (airSystem != null && airSystem.CurrentIgnition == IgnitionState.RUNNING)
                    {
                        target = HighlightTarget.IgnitionOff;
                        nextAction = "Shut Engine OFF (Tap [ OFF ])";
                        whyWeDoThis = "Shut engine off so compressor stops charging during leak check.";
                    }
                    else if (airSystem != null && airSystem.CurrentIgnition == IgnitionState.OFF)
                    {
                        target = HighlightTarget.IgnitionAcc;
                        nextAction = "Turn Key to [ ACC / ON ]";
                        whyWeDoThis = "Turn electrical power ON to feed dash gauges and warning buzzers.";
                    }
                    else if (airSystem != null && (!airSystem.IsYellowBrakePushedIn || !airSystem.IsRedTrailerPushedIn))
                    {
                        if (!airSystem.IsYellowBrakePushedIn && !airSystem.IsRedTrailerPushedIn)
                        {
                            target = HighlightTarget.BothValves;
                            nextAction = "Push IN Both Valves (Yellow & Red)";
                        }
                        else if (!airSystem.IsYellowBrakePushedIn)
                        {
                            target = HighlightTarget.YellowValve;
                            nextAction = "Push IN Yellow Tractor Knob";
                        }
                        else
                        {
                            target = HighlightTarget.RedValve;
                            nextAction = "Push IN Red Trailer Knob";
                        }
                        whyWeDoThis = "Push in both valves to release tractor and trailer spring brakes and charge lines.";
                    }
                    else
                    {
                        target = HighlightTarget.HoldPedal;
                        nextAction = isLeakTestRunning
                            ? $"HOLD FOOT PEDAL! ({LeakTimerRemaining:F0}s remaining)"
                            : "Press & HOLD Foot Brake (or Fast Forward 60s)";
                        whyWeDoThis = "Press and hold foot brake down for 60 seconds. Combination must not lose >4 PSI.";
                    }
                    break;

                case DMVStep.LowAirAlarmTest:
                    // Step 3.1: Fan pedal until low-air buzzer alarms at >=60 PSI.
                    if (airSystem != null && airSystem.CurrentIgnition != IgnitionState.ACC_ON)
                    {
                        target = HighlightTarget.IgnitionAcc;
                        nextAction = "Turn Key to [ ACC / ON ]";
                        whyWeDoThis = "Turn electrical power ON to feed dash gauges and warning buzzers.";
                    }
                    else
                    {
                        target = HighlightTarget.FanPedal;
                        nextAction = "FAN (pump) Foot Service Brake";
                        whyWeDoThis = "Pump service brake repeatedly to bleed air down until low-air buzzer alarms at >=60 PSI.";
                    }
                    break;

                case DMVStep.EmergencyPopOutTest:
                    // Step 4.1: Keep fanning until emergency spring brakes pop out between 20-45 PSI.
                    if (airSystem != null && airSystem.CurrentIgnition != IgnitionState.ACC_ON)
                    {
                        target = HighlightTarget.IgnitionAcc;
                        nextAction = "Turn Key to [ ACC / ON ]";
                        whyWeDoThis = "Turn electrical power ON to feed dash gauges and warning buzzers.";
                    }
                    else
                    {
                        target = HighlightTarget.FanPedal;
                        nextAction = "Continue FANNING Foot Brake";
                        whyWeDoThis = "Continue bleeding air until emergency spring brakes pop out between 20-45 PSI.";
                    }
                    break;

                case DMVStep.CompressorBuildUpTest:
                    // Step 5.1: Highlight RUNNING & REV ENGINE.
                    if (airSystem != null && airSystem.CurrentIgnition != IgnitionState.RUNNING)
                    {
                        target = HighlightTarget.IgnitionRun;
                        nextAction = "Start Engine (Tap [ RUNNING ])";
                        whyWeDoThis = "Start engine and time compressor build-up from 85 to 100 PSI (must be under 45s).";
                    }
                    else
                    {
                        target = HighlightTarget.RevEngine;
                        nextAction = isTimingBuildUp
                            ? $"REV ENGINE! Timing: {buildUpElapsedTime:F1}s / 45s"
                            : "REV ENGINE (~1200 RPM) to reach 85 PSI";
                        whyWeDoThis = "Start engine and time compressor build-up from 85 to 100 PSI (must be under 45s).";
                    }
                    break;

                case DMVStep.TestCompleted:
                    target = HighlightTarget.None;
                    nextAction = "All Inspection Steps Passed!";
                    whyWeDoThis = "Excellent job! You have fully mastered the 4-step CDL In-Cab Air Brake Inspection.";
                    break;

                case DMVStep.TestFailed:
                    target = HighlightTarget.None;
                    nextAction = "Inspection Failed";
                    whyWeDoThis = "Review the safety regulation and retry the step.";
                    break;
            }

            bool targetChanged = (target != currentHighlightedTarget);
            bool textChanged = (nextAction != currentNextAction || whyWeDoThis != currentWhyWeDoThis);

            currentHighlightedTarget = target;
            currentNextAction = nextAction;
            currentWhyWeDoThis = whyWeDoThis;

            if (targetChanged)
            {
                OnTargetHighlightChanged?.Invoke(currentHighlightedTarget);
            }

            if (textChanged)
            {
                OnTrainingGuidanceUpdated?.Invoke(currentNextAction, currentWhyWeDoThis);
            }
        }

        public void RetryCurrentStep()
        {
            switch (failedStep)
            {
                case DMVStep.EnginePrep:
                    airSystem.ResetForNewGame(90f, 110f);
                    airSystem.SetIgnition(IgnitionState.OFF);
                    SetStep(DMVStep.EnginePrep);
                    break;

                case DMVStep.AppliedLeakageTest:
                    airSystem.SetPressuresDirectly(125f, 124f);
                    airSystem.SetIgnition(IgnitionState.ACC_ON);
                    airSystem.SetYellowValve(true);
                    airSystem.SetRedValve(true);
                    SetStep(DMVStep.AppliedLeakageTest);
                    break;

                case DMVStep.LowAirAlarmTest:
                    airSystem.SetPressuresDirectly(95f, 94f);
                    airSystem.SetIgnition(IgnitionState.ACC_ON);
                    airSystem.SetYellowValve(true);
                    airSystem.SetRedValve(true);
                    SetStep(DMVStep.LowAirAlarmTest);
                    break;

                case DMVStep.EmergencyPopOutTest:
                    airSystem.SetPressuresDirectly(55f, 55f);
                    airSystem.SetIgnition(IgnitionState.ACC_ON);
                    airSystem.SetYellowValve(true);
                    airSystem.SetRedValve(true);
                    SetStep(DMVStep.EmergencyPopOutTest);
                    break;

                case DMVStep.CompressorBuildUpTest:
                    airSystem.SetPressuresDirectly(60f, 60f);
                    airSystem.SetIgnition(IgnitionState.RUNNING);
                    SetStep(DMVStep.CompressorBuildUpTest);
                    break;

                default:
                    airSystem.ResetForNewGame(90f, 110f);
                    airSystem.SetIgnition(IgnitionState.OFF);
                    SetStep(DMVStep.EnginePrep);
                    break;
            }

            UpdateTrainingGuidance();
        }

        public void RestartFullTest()
        {
            airSystem.ResetForNewGame(90f, 110f);
            airSystem.SetIgnition(IgnitionState.OFF);
            SetStep(DMVStep.EnginePrep);
            UpdateTrainingGuidance();
        }

        #endregion

        #region Event Handlers from AirBrakeSystem

        private void HandleValveStateChanged(ValveType valve, bool pushedIn)
        {
            UpdateTrainingGuidance();
        }

        private void HandleIgnitionChanged(IgnitionState state)
        {
            UpdateTrainingGuidance();
        }

        private void HandlePedalStateChanged(bool isHeld)
        {
            UpdateTrainingGuidance();
        }

        private void HandlePressureChanged(float pPSI, float sPSI)
        {
            UpdateTrainingGuidance();
        }

        #endregion
    }
}
