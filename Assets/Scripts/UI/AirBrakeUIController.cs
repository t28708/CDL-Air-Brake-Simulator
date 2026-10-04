using System;
using UnityEngine;
using UnityEngine.UI;

namespace CDL.AirBrakeSimulator
{
    /// <summary>
    /// Master UI Controller for the CDL Air Brake Simulator.
    /// Manages the in-cab dashboard layout, interactive valve knobs, ignition switches,
    /// warning annunciators, examiner dialogue cards, and DMV pass/fail modals.
    /// </summary>
    public class AirBrakeUIController : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private AirBrakeSystem airSystem;
        [SerializeField] private DMVTestManager dmvManager;
        [SerializeField] private InteractivePedal brakePedal;
        [SerializeField] private CabinThemeManager themeManager;
        [SerializeField] private TutorialGuideController tutorialGuide;

        [Header("Ignition Controls")]
        [SerializeField] private Button btnStartNewGame;
        [SerializeField] private Button btnIgnitionOff;
        [SerializeField] private Button btnIgnitionAcc;
        [SerializeField] private Button btnIgnitionRun;
        [SerializeField] private Image imgIgnitionOff;
        [SerializeField] private Image imgIgnitionAcc;
        [SerializeField] private Image imgIgnitionRun;
        [SerializeField] private Button btnRevEngine;
        [SerializeField] private Text txtRevEngine;
        [SerializeField] private Text txtRpmCounter;

        [Header("Push-Pull Valve Knobs")]
        [SerializeField] private Button btnYellowKnob;
        [SerializeField] private RectTransform yellowKnobTransform;
        [SerializeField] private Text txtYellowStatus;

        [SerializeField] private Button btnRedKnob;
        [SerializeField] private RectTransform redKnobTransform;
        [SerializeField] private Text txtRedStatus;

        [Header("Brake Pedal Controls")]
        [SerializeField] private Button btnFanPedal;

        [Header("Low Air Warning Annunciator")]
        [SerializeField] private GameObject warningPanel;
        [SerializeField] private Image warningIcon;
        [SerializeField] private Text warningText;

        [Header("Examiner Dialogue & Step Panel")]
        [SerializeField] private Text txtStepHeader;
        [SerializeField] private Text txtInstructions;
        [SerializeField] private Text txtVerbalScript;
        [SerializeField] private Text txtTimer;
        [SerializeField] private Button btnFastForward;
        [SerializeField] private Toggle toggleFaultLeak;

        [Header("Examiner Card Collapsible Controls")]
        [SerializeField] private GameObject examinerCardSection;
        [SerializeField] private GameObject examinerMinimizedBar;
        [SerializeField] private Button btnToggleExaminerCard;
        [SerializeField] private Text txtToggleExaminerBtn;
        [SerializeField] private Button btnHideExaminerCard;
        [SerializeField] private Button btnShowExaminerCard;
        [SerializeField] private Button btnHeaderDismiss;
        private bool isExaminerCardVisible = true;

        [Header("DMV Failure Modal")]
        [SerializeField] private GameObject failureModal;
        [SerializeField] private Text txtFailureReason;
        [SerializeField] private Text txtFailureRegulation;
        [SerializeField] private Button btnRetryStep;
        [SerializeField] private Button btnRestartFullTest;

        [Header("DMV Success Modal")]
        [SerializeField] private GameObject successModal;
        [SerializeField] private Text txtScorecard;
        [SerializeField] private Button btnInspectAgain;

        [Header("Simulator Game Mode Switcher")]
        [SerializeField] private Button btnModeTraining;
        [SerializeField] private Button btnModeExam;
        [SerializeField] private Image imgModeTrainingBg;
        [SerializeField] private Image imgModeExamBg;
        [SerializeField] private Text txtModeTraining;
        [SerializeField] private Text txtModeExam;

        [Header("Guided Training UI")]
        [SerializeField] private GameObject nextActionBoxGO;
        [SerializeField] private Text txtNextAction;
        [SerializeField] private GameObject whyWeDoThisBoxGO;
        [SerializeField] private Text txtWhyWeDoThis;

        [Header("Element Highlighters")]
        [SerializeField] private UIElementHighlighter hlIgnitionOff;
        [SerializeField] private UIElementHighlighter hlIgnitionAcc;
        [SerializeField] private UIElementHighlighter hlIgnitionRun;
        [SerializeField] private UIElementHighlighter hlRevEngine;
        [SerializeField] private UIElementHighlighter hlYellowKnob;
        [SerializeField] private UIElementHighlighter hlRedKnob;
        [SerializeField] private UIElementHighlighter hlHoldPedal;
        [SerializeField] private UIElementHighlighter hlFanPedal;
        [SerializeField] private UIElementHighlighter hlFastForward;

        // Visual Colors
        private readonly Color activeControlColor = new Color(0.18f, 0.75f, 0.35f, 1f);
        private readonly Color inactiveControlColor = new Color(0.25f, 0.28f, 0.32f, 1f);
        private readonly Color valvePushedColor = new Color(0.2f, 0.8f, 0.2f);
        private readonly Color valvePoppedColor = new Color(0.9f, 0.2f, 0.2f);

        private float warningFlashTimer = 0f;

        private void Awake()
        {
            HideAllModalsInScene();

            if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
            if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
            if (brakePedal == null) brakePedal = FindFirstObjectByType<InteractivePedal>();
            if (themeManager == null) themeManager = FindFirstObjectByType<CabinThemeManager>();

            WireButtonListeners();
        }

        public void SetThemeManager(CabinThemeManager manager)
        {
            themeManager = manager;
        }

        private void Start()
        {
            HideAllModalsInScene();

            Transform bg = transform.Find("Background");
            if (bg != null && bg.Find("Layout_V8_CleanCockpitErgonomics") == null)
            {
                Debug.Log("<color=#38bdf8><b>[CDL Air Brake] Upgrading canvas to Layout_V8_CleanCockpitErgonomics...</b></color>");
                GameObject builderGO = new GameObject("RuntimeSceneAutoBuilder");
                RuntimeSceneBuilder builder = builderGO.AddComponent<RuntimeSceneBuilder>();
                builder.BuildRuntimeUI();
                return;
            }

            HideAllModalsInScene();

            UpdateIgnitionVisuals(airSystem != null ? airSystem.CurrentIgnition : IgnitionState.OFF);
            UpdateValveVisual(ValveType.TractorYellow, airSystem != null && airSystem.IsYellowBrakePushedIn);
            UpdateValveVisual(ValveType.TrailerRed, airSystem != null && airSystem.IsRedTrailerPushedIn);
            UpdateGameModeVisuals(dmvManager != null ? dmvManager.CurrentGameMode : GameMode.Training);
        }

        private void OnEnable()
        {
            HideAllModalsInScene();
            SubscribeEvents();
            WireButtonListeners();
        }

        private void HideAllModalsInScene()
        {
            if (failureModal != null) failureModal.SetActive(false);
            if (successModal != null) successModal.SetActive(false);

            Transform cT = transform;
            Transform failT = cT.Find("FailureModal");
            if (failT != null) failT.gameObject.SetActive(false);
            Transform passT = cT.Find("SuccessModal");
            if (passT != null) passT.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        public void SubscribeEvents()
        {
            UnsubscribeEvents();
            if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
            if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();

            if (airSystem != null)
            {
                airSystem.OnIgnitionChanged += HandleIgnitionChanged;
                airSystem.OnValveStateChanged += HandleValveStateChanged;
                airSystem.OnLowAirWarningChanged += HandleLowAirWarningChanged;
            }

            if (dmvManager != null)
            {
                dmvManager.OnStepChanged += HandleStepChanged;
                dmvManager.OnInstructionUpdated += HandleInstructionUpdated;
                dmvManager.OnTimerUpdated += HandleTimerUpdated;
                dmvManager.OnTestFailed += HandleTestFailed;
                dmvManager.OnTestPassed += HandleTestPassed;
                dmvManager.OnGameModeChanged += HandleGameModeChanged;
                dmvManager.OnTargetHighlightChanged += HandleTargetHighlightChanged;
                dmvManager.OnTrainingGuidanceUpdated += HandleTrainingGuidanceUpdated;
                dmvManager.OnSoftErrorHint += HandleSoftErrorHint;
            }
        }

        public void UnsubscribeEvents()
        {
            if (airSystem != null)
            {
                airSystem.OnIgnitionChanged -= HandleIgnitionChanged;
                airSystem.OnValveStateChanged -= HandleValveStateChanged;
                airSystem.OnLowAirWarningChanged -= HandleLowAirWarningChanged;
            }

            if (dmvManager != null)
            {
                dmvManager.OnStepChanged -= HandleStepChanged;
                dmvManager.OnInstructionUpdated -= HandleInstructionUpdated;
                dmvManager.OnTimerUpdated -= HandleTimerUpdated;
                dmvManager.OnTestFailed -= HandleTestFailed;
                dmvManager.OnTestPassed -= HandleTestPassed;
                dmvManager.OnGameModeChanged -= HandleGameModeChanged;
                dmvManager.OnTargetHighlightChanged -= HandleTargetHighlightChanged;
                dmvManager.OnTrainingGuidanceUpdated -= HandleTrainingGuidanceUpdated;
                dmvManager.OnSoftErrorHint -= HandleSoftErrorHint;
            }
        }

        private void Update()
        {
            if (transform != null && transform.Find("Background/Layout_V8_CleanCockpitErgonomics") == null)
            {
                Debug.Log("<color=#38bdf8><b>[CDL Air Brake] Upgrading canvas to Layout_V8_CleanCockpitErgonomics in Update()...</b></color>");
                GameObject builderGO = new GameObject("RuntimeSceneAutoBuilder");
                RuntimeSceneBuilder builder = builderGO.AddComponent<RuntimeSceneBuilder>();
                builder.BuildRuntimeUI();
                return;
            }

            HandleKeyboardHotkeys();
            UpdateWarningAnnunciatorFlash();
            UpdateRpmDisplay();
        }

        private void UpdateRpmDisplay()
        {
            if (txtRpmCounter != null && airSystem != null)
            {
                if (airSystem.CurrentIgnition == IgnitionState.RUNNING)
                {
                    txtRpmCounter.text = airSystem.IsEngineRevved ? "RPM: 1500 (REV)" : "RPM: 650 (IDLE)";
                    txtRpmCounter.color = airSystem.IsEngineRevved ? new Color(0.2f, 0.95f, 0.45f) : new Color(0.7f, 0.9f, 1f);
                }
                else if (airSystem.CurrentIgnition == IgnitionState.ACC_ON)
                {
                    txtRpmCounter.text = "RPM: 0 (ACC / ON)";
                    txtRpmCounter.color = new Color(0.95f, 0.85f, 0.2f);
                }
                else
                {
                    txtRpmCounter.text = "POWER: OFF";
                    txtRpmCounter.color = new Color(0.35f, 0.38f, 0.44f, 0.5f);
                }
            }
        }

        private void WireButtonListeners()
        {
            if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
            if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
            if (brakePedal == null) brakePedal = FindFirstObjectByType<InteractivePedal>();

            if (btnModeTraining != null)
            {
                btnModeTraining.onClick.RemoveAllListeners();
                btnModeTraining.onClick.AddListener(() =>
                {
                    if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
                    dmvManager?.SetGameMode(GameMode.Training);
                    UpdateGameModeVisuals(GameMode.Training);
                });
            }

            if (btnModeExam != null)
            {
                btnModeExam.onClick.RemoveAllListeners();
                btnModeExam.onClick.AddListener(() =>
                {
                    if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
                    dmvManager?.SetGameMode(GameMode.Exam);
                    UpdateGameModeVisuals(GameMode.Exam);
                });
            }

            if (btnIgnitionOff != null)
            {
                btnIgnitionOff.onClick.RemoveAllListeners();
                btnIgnitionOff.onClick.AddListener(() =>
                {
                    if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
                    if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
                    Debug.Log($"<color=#10b981>[CDL UI] BtnIgnitionOff clicked! Mode: {(dmvManager != null ? dmvManager.CurrentGameMode.ToString() : "null")}</color>");

                    if (dmvManager != null && dmvManager.CurrentGameMode == GameMode.Training && !dmvManager.CheckActionPermitted(HighlightTarget.IgnitionOff, out _))
                    {
                        hlIgnitionOff?.TriggerWrongActionShake();
                        PulseCurrentHighlight();
                        return;
                    }
                    airSystem?.SetIgnition(IgnitionState.OFF);
                });
            }
            if (btnIgnitionAcc != null)
            {
                btnIgnitionAcc.onClick.RemoveAllListeners();
                btnIgnitionAcc.onClick.AddListener(() =>
                {
                    if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
                    if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
                    Debug.Log($"<color=#10b981>[CDL UI] BtnIgnitionAcc clicked! Mode: {(dmvManager != null ? dmvManager.CurrentGameMode.ToString() : "null")}</color>");

                    if (dmvManager != null && dmvManager.CurrentGameMode == GameMode.Training && !dmvManager.CheckActionPermitted(HighlightTarget.IgnitionAcc, out _))
                    {
                        hlIgnitionAcc?.TriggerWrongActionShake();
                        PulseCurrentHighlight();
                        return;
                    }
                    airSystem?.SetIgnition(IgnitionState.ACC_ON);
                });
            }
            if (btnIgnitionRun != null)
            {
                btnIgnitionRun.onClick.RemoveAllListeners();
                btnIgnitionRun.onClick.AddListener(() =>
                {
                    if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
                    if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
                    Debug.Log($"<color=#10b981>[CDL UI] BtnIgnitionRun clicked! Mode: {(dmvManager != null ? dmvManager.CurrentGameMode.ToString() : "null")}, CurrentIgnition: {(airSystem != null ? airSystem.CurrentIgnition.ToString() : "null")}</color>");

                    if (dmvManager != null && dmvManager.CurrentGameMode == GameMode.Training && !dmvManager.CheckActionPermitted(HighlightTarget.IgnitionRun, out _))
                    {
                        Debug.LogWarning("<color=#f59e0b>[CDL UI] BtnIgnitionRun action disallowed by training guidance!</color>");
                        hlIgnitionRun?.TriggerWrongActionShake();
                        PulseCurrentHighlight();
                        return;
                    }
                    airSystem?.SetIgnition(IgnitionState.RUNNING);
                });
            }

            if (btnRevEngine != null)
            {
                btnRevEngine.onClick.RemoveAllListeners();
                btnRevEngine.onClick.AddListener(() =>
                {
                    if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
                    if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
                    Debug.Log($"<color=#10b981>[CDL UI] BtnRevEngine clicked!</color>");

                    if (dmvManager != null && dmvManager.CurrentGameMode == GameMode.Training && !dmvManager.CheckActionPermitted(HighlightTarget.RevEngine, out _))
                    {
                        hlRevEngine?.TriggerWrongActionShake();
                        PulseCurrentHighlight();
                        return;
                    }
                    if (airSystem != null)
                    {
                        bool nextState = !airSystem.IsEngineRevved;
                        airSystem.SetEngineRevved(nextState);
                    }
                });
            }

            if (btnYellowKnob != null)
            {
                btnYellowKnob.onClick.RemoveAllListeners();
                btnYellowKnob.onClick.AddListener(() =>
                {
                    if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
                    if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
                    Debug.Log($"<color=#10b981>[CDL UI] BtnYellowKnob clicked!</color>");

                    if (dmvManager != null && dmvManager.CurrentGameMode == GameMode.Training && !dmvManager.CheckActionPermitted(HighlightTarget.YellowValve, out _))
                    {
                        hlYellowKnob?.TriggerWrongActionShake();
                        PulseCurrentHighlight();
                        return;
                    }
                    airSystem?.ToggleYellowValve();
                });
            }
            if (btnRedKnob != null)
            {
                btnRedKnob.onClick.RemoveAllListeners();
                btnRedKnob.onClick.AddListener(() =>
                {
                    if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
                    if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
                    Debug.Log($"<color=#10b981>[CDL UI] BtnRedKnob clicked!</color>");

                    if (dmvManager != null && dmvManager.CurrentGameMode == GameMode.Training && !dmvManager.CheckActionPermitted(HighlightTarget.RedValve, out _))
                    {
                        hlRedKnob?.TriggerWrongActionShake();
                        PulseCurrentHighlight();
                        return;
                    }
                    airSystem?.ToggleRedValve();
                });
            }

            if (btnFanPedal != null)
            {
                btnFanPedal.onClick.RemoveAllListeners();
                btnFanPedal.onClick.AddListener(() =>
                {
                    if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
                    if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
                    Debug.Log($"<color=#10b981>[CDL UI] BtnFanPedal clicked!</color>");

                    if (dmvManager != null && dmvManager.CurrentGameMode == GameMode.Training && !dmvManager.CheckActionPermitted(HighlightTarget.FanPedal, out _))
                    {
                        hlFanPedal?.TriggerWrongActionShake();
                        PulseCurrentHighlight();
                        return;
                    }
                    if (brakePedal != null) brakePedal.FanOnce();
                    else if (airSystem != null) airSystem.FanPedal();
                });
            }

            if (btnFastForward != null)
            {
                btnFastForward.onClick.RemoveAllListeners();
                btnFastForward.onClick.AddListener(() =>
                {
                    if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
                    dmvManager?.FastForwardAppliedLeakage();
                });
            }

            if (toggleFaultLeak != null)
            {
                toggleFaultLeak.onValueChanged.RemoveAllListeners();
                toggleFaultLeak.onValueChanged.AddListener((isOn) =>
                {
                    if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
                    if (airSystem != null) airSystem.FaultInjectExcessiveLeak = isOn;
                });
            }

            if (btnRetryStep != null)
            {
                btnRetryStep.onClick.RemoveAllListeners();
                btnRetryStep.onClick.AddListener(() =>
                {
                    if (failureModal != null) failureModal.SetActive(false);
                    if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
                    dmvManager?.RetryCurrentStep();
                });
            }

            if (btnRestartFullTest != null)
            {
                btnRestartFullTest.onClick.RemoveAllListeners();
                btnRestartFullTest.onClick.AddListener(() =>
                {
                    if (failureModal != null) failureModal.SetActive(false);
                    if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
                    dmvManager?.RestartFullTest();
                });
            }

            if (btnInspectAgain != null)
            {
                btnInspectAgain.onClick.RemoveAllListeners();
                btnInspectAgain.onClick.AddListener(() =>
                {
                    if (successModal != null) successModal.SetActive(false);
                    if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
                    dmvManager?.RestartFullTest();
                });
            }

            if (btnStartNewGame != null)
            {
                btnStartNewGame.onClick.RemoveAllListeners();
                btnStartNewGame.onClick.AddListener(StartNewGame);
            }

            ResolveExaminerCardReferences();

            if (btnHideExaminerCard == null)
            {
                Transform bg = transform.Find("Background");
                if (bg != null)
                {
                    Transform t = bg.Find("5_ExaminerCardSection/BtnHideExaminerCard");
                    if (t != null) btnHideExaminerCard = t.GetComponent<Button>();
                }
                if (btnHideExaminerCard == null)
                {
                    GameObject go = GameObject.Find("BtnHideExaminerCard");
                    if (go != null) btnHideExaminerCard = go.GetComponent<Button>();
                }
            }
            if (btnToggleExaminerCard == null)
            {
                GameObject go = GameObject.Find("BtnToggleDMV");
                if (go != null) btnToggleExaminerCard = go.GetComponent<Button>();
            }
            if (btnShowExaminerCard == null)
            {
                Transform bg = transform.Find("Background");
                if (bg != null)
                {
                    Transform t = bg.Find("ExaminerMinimizedBar/BtnShowExaminerCard");
                    if (t != null) btnShowExaminerCard = t.GetComponent<Button>();
                }
                if (btnShowExaminerCard == null)
                {
                    GameObject go = GameObject.Find("BtnShowExaminerCard");
                    if (go != null) btnShowExaminerCard = go.GetComponent<Button>();
                }
            }
            if (btnHeaderDismiss == null)
            {
                Transform bg = transform.Find("Background");
                if (bg != null)
                {
                    Transform t = bg.Find("5_ExaminerCardSection/TaskTitle");
                    if (t != null) btnHeaderDismiss = t.GetComponent<Button>();
                }
                if (btnHeaderDismiss == null)
                {
                    GameObject go = GameObject.Find("TaskTitle");
                    if (go != null) btnHeaderDismiss = go.GetComponent<Button>();
                }
            }

            if (btnToggleExaminerCard != null)
            {
                btnToggleExaminerCard.onClick.RemoveAllListeners();
                btnToggleExaminerCard.onClick.AddListener(ToggleExaminerCard);
                var toggle = btnToggleExaminerCard.GetComponent<ExaminerCardToggle>();
                if (toggle == null) toggle = btnToggleExaminerCard.gameObject.AddComponent<ExaminerCardToggle>();
                toggle.Setup(ExaminerCardToggle.ToggleAction.ToggleCard, examinerCardSection, examinerMinimizedBar, txtToggleExaminerBtn);
            }

            if (btnHideExaminerCard != null)
            {
                btnHideExaminerCard.onClick.RemoveAllListeners();
                btnHideExaminerCard.onClick.AddListener(() => SetExaminerCardVisible(false));
                var toggle = btnHideExaminerCard.GetComponent<ExaminerCardToggle>();
                if (toggle == null) toggle = btnHideExaminerCard.gameObject.AddComponent<ExaminerCardToggle>();
                toggle.Setup(ExaminerCardToggle.ToggleAction.HideCard, examinerCardSection, examinerMinimizedBar);
            }

            if (btnHeaderDismiss != null)
            {
                btnHeaderDismiss.onClick.RemoveAllListeners();
                btnHeaderDismiss.onClick.AddListener(() => SetExaminerCardVisible(false));
                var toggle = btnHeaderDismiss.GetComponent<ExaminerCardToggle>();
                if (toggle == null) toggle = btnHeaderDismiss.gameObject.AddComponent<ExaminerCardToggle>();
                toggle.Setup(ExaminerCardToggle.ToggleAction.HideCard, examinerCardSection, examinerMinimizedBar);
            }

            if (btnShowExaminerCard != null)
            {
                btnShowExaminerCard.onClick.RemoveAllListeners();
                btnShowExaminerCard.onClick.AddListener(() => SetExaminerCardVisible(true));
                var toggle = btnShowExaminerCard.GetComponent<ExaminerCardToggle>();
                if (toggle == null) toggle = btnShowExaminerCard.gameObject.AddComponent<ExaminerCardToggle>();
                toggle.Setup(ExaminerCardToggle.ToggleAction.ShowCard, examinerCardSection, examinerMinimizedBar);
            }
        }

        public bool IsExaminerCardVisible => isExaminerCardVisible;
        private float lastCardToggleTime = -1f;

        public void ToggleExaminerCard()
        {
            if (Time.unscaledTime - lastCardToggleTime < 0.25f) return;
            SetExaminerCardVisible(!isExaminerCardVisible);
        }

        public void SetExaminerCardVisible(bool visible)
        {
            lastCardToggleTime = Time.unscaledTime;
            ResolveExaminerCardReferences();
            isExaminerCardVisible = visible;
            if (examinerCardSection != null) examinerCardSection.SetActive(visible);
            if (examinerMinimizedBar != null) examinerMinimizedBar.SetActive(!visible);
            if (txtToggleExaminerBtn != null)
            {
                txtToggleExaminerBtn.text = visible ? "📋 HIDE DMV" : "📋 SHOW DMV";
            }
            if (btnToggleExaminerCard != null)
            {
                Image img = btnToggleExaminerCard.GetComponent<Image>();
                if (img != null)
                {
                    img.color = visible ? new Color(0.18f, 0.45f, 0.78f, 1f) : new Color(0.85f, 0.55f, 0.10f, 1f);
                }
            }
            Debug.Log($"<color=#38bdf8><b>[CDL Air Brake] UIController SetExaminerCardVisible: {visible} (cardSection: {(examinerCardSection != null ? "ok" : "null")}, minBar: {(examinerMinimizedBar != null ? "ok" : "null")})</b></color>");
        }

        public void SetExaminerCardVisibleDirect(bool visible)
        {
            SetExaminerCardVisible(visible);
        }

        private void ResolveExaminerCardReferences()
        {
            if (examinerCardSection != null && examinerMinimizedBar != null) return;

            Transform bg = transform.Find("Background");
            if (bg == null && transform.parent != null) bg = transform.parent.Find("Background");
            if (bg == null)
            {
                var canvas = GameObject.Find("CDL_AirBrake_Canvas");
                if (canvas != null) bg = canvas.transform.Find("Background");
            }

            if (bg != null)
            {
                if (examinerCardSection == null)
                {
                    Transform t = bg.Find("5_ExaminerCardSection");
                    if (t != null) examinerCardSection = t.gameObject;
                }
                if (examinerMinimizedBar == null)
                {
                    Transform t = bg.Find("ExaminerMinimizedBar");
                    if (t != null) examinerMinimizedBar = t.gameObject;
                }
            }
        }

        public void BindExaminerCollapse(
            GameObject cardSection,
            GameObject minBar,
            Button toggleBtn,
            Text toggleBtnTxt,
            Button hideBtn,
            Button showBtn,
            Button headerDismissBtn)
        {
            examinerCardSection = cardSection;
            examinerMinimizedBar = minBar;
            btnToggleExaminerCard = toggleBtn;
            txtToggleExaminerBtn = toggleBtnTxt;
            btnHideExaminerCard = hideBtn;
            btnShowExaminerCard = showBtn;
            btnHeaderDismiss = headerDismissBtn;

            if (btnToggleExaminerCard != null)
            {
                var toggle = btnToggleExaminerCard.GetComponent<ExaminerCardToggle>();
                if (toggle == null) toggle = btnToggleExaminerCard.gameObject.AddComponent<ExaminerCardToggle>();
                toggle.Setup(ExaminerCardToggle.ToggleAction.ToggleCard, examinerCardSection, examinerMinimizedBar, txtToggleExaminerBtn);
            }
            if (btnHideExaminerCard != null)
            {
                var toggle = btnHideExaminerCard.GetComponent<ExaminerCardToggle>();
                if (toggle == null) toggle = btnHideExaminerCard.gameObject.AddComponent<ExaminerCardToggle>();
                toggle.Setup(ExaminerCardToggle.ToggleAction.HideCard, examinerCardSection, examinerMinimizedBar);
            }
            if (btnShowExaminerCard != null)
            {
                var toggle = btnShowExaminerCard.GetComponent<ExaminerCardToggle>();
                if (toggle == null) toggle = btnShowExaminerCard.gameObject.AddComponent<ExaminerCardToggle>();
                toggle.Setup(ExaminerCardToggle.ToggleAction.ShowCard, examinerCardSection, examinerMinimizedBar);
            }
            if (btnHeaderDismiss != null)
            {
                var toggle = btnHeaderDismiss.GetComponent<ExaminerCardToggle>();
                if (toggle == null) toggle = btnHeaderDismiss.gameObject.AddComponent<ExaminerCardToggle>();
                toggle.Setup(ExaminerCardToggle.ToggleAction.HideCard, examinerCardSection, examinerMinimizedBar);
            }

            WireButtonListeners();
            SetExaminerCardVisible(true);
        }

        /// <summary>
        /// Starts a fresh CDL examination test from Step 1, resetting all pressures, valves, and modals.
        /// </summary>
        public void StartNewGame()
        {
            if (failureModal != null) failureModal.SetActive(false);
            if (successModal != null) successModal.SetActive(false);
            if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
            if (dmvManager != null) dmvManager.RestartFullTest();
        }

        private void HandleKeyboardHotkeys()
        {
            if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
            if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
            if (brakePedal == null) brakePedal = FindFirstObjectByType<InteractivePedal>();
            if (themeManager == null) themeManager = FindFirstObjectByType<CabinThemeManager>();

            bool keyI = false;
            bool keyY = false;
            bool keyR = false;
            bool keyF = false;
            bool keyT = false;
            bool keyN = false;
            bool keyB = false;
            bool keyD = false;
            bool keyH = false;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                keyI = kb.iKey.wasPressedThisFrame;
                keyY = kb.yKey.wasPressedThisFrame;
                keyR = kb.rKey.wasPressedThisFrame;
                keyF = kb.fKey.wasPressedThisFrame;
                keyT = kb.tKey.wasPressedThisFrame;
                keyN = kb.nKey.wasPressedThisFrame;
                keyB = kb.bKey.wasPressedThisFrame;
                keyD = kb.dKey.wasPressedThisFrame;
                keyH = kb.hKey.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!keyI) { try { keyI = Input.GetKeyDown(KeyCode.I); } catch {} }
            if (!keyY) { try { keyY = Input.GetKeyDown(KeyCode.Y); } catch {} }
            if (!keyR) { try { keyR = Input.GetKeyDown(KeyCode.R); } catch {} }
            if (!keyF) { try { keyF = Input.GetKeyDown(KeyCode.F); } catch {} }
            if (!keyT) { try { keyT = Input.GetKeyDown(KeyCode.T); } catch {} }
            if (!keyN) { try { keyN = Input.GetKeyDown(KeyCode.N); } catch {} }
            if (!keyB) { try { keyB = Input.GetKeyDown(KeyCode.B); } catch {} }
            if (!keyD) { try { keyD = Input.GetKeyDown(KeyCode.D); } catch {} }
            if (!keyH) { try { keyH = Input.GetKeyDown(KeyCode.H); } catch {} }
#endif

            if (keyD || keyH)
            {
                ToggleExaminerCard();
            }

            if (keyT)
            {
                if (tutorialGuide == null) tutorialGuide = FindFirstObjectByType<TutorialGuideController>();
                if (tutorialGuide != null) tutorialGuide.ToggleTrainingMode();
            }

            if (keyI && airSystem != null)
            {
                IgnitionState next = (IgnitionState)(((int)airSystem.CurrentIgnition + 1) % 3);
                airSystem.SetIgnition(next);
                Debug.Log($"<color=#38bdf8>[CDL Air Brake] Ignition switched via <I> key to: {next}</color>");
            }

            if (keyY && airSystem != null)
            {
                airSystem.ToggleYellowValve();
            }

            if (keyR && airSystem != null)
            {
                airSystem.ToggleRedValve();
            }

            if (keyF)
            {
                if (dmvManager != null && dmvManager.CurrentStep == DMVStep.AppliedLeakageTest)
                {
                    dmvManager.FastForwardAppliedLeakage();
                }
                else if (airSystem != null)
                {
                    bool nextRev = !airSystem.IsEngineRevved;
                    airSystem.SetEngineRevved(nextRev);
                }
            }

            if (keyB)
            {
                if (brakePedal != null) brakePedal.FanOnce();
                else if (airSystem != null) airSystem.FanPedal();
            }

            if (keyT && themeManager != null)
            {
                themeManager.NextTheme();
            }

            if (keyN)
            {
                StartNewGame();
            }
        }

        #region Event Callbacks

        private void HandleIgnitionChanged(IgnitionState state)
        {
            UpdateIgnitionVisuals(state);
        }

        private void UpdateIgnitionVisuals(IgnitionState state)
        {
            if (imgIgnitionOff != null) imgIgnitionOff.color = (state == IgnitionState.OFF) ? activeControlColor : inactiveControlColor;
            if (imgIgnitionAcc != null) imgIgnitionAcc.color = (state == IgnitionState.ACC_ON) ? activeControlColor : inactiveControlColor;
            if (imgIgnitionRun != null) imgIgnitionRun.color = (state == IgnitionState.RUNNING) ? activeControlColor : inactiveControlColor;

            if (btnRevEngine != null)
            {
                btnRevEngine.interactable = (state == IgnitionState.RUNNING);
            }
            if (txtRevEngine != null)
            {
                txtRevEngine.text = (state == IgnitionState.RUNNING && airSystem.IsEngineRevved) ? "RPM: 1400 (REV)" : "RPM: 600 (IDLE)";
            }
        }

        private void HandleValveStateChanged(ValveType valve, bool pushedIn)
        {
            UpdateValveVisual(valve, pushedIn);
        }

        private void UpdateValveVisual(ValveType valve, bool pushedIn)
        {
            if (valve == ValveType.TractorYellow)
            {
                if (yellowKnobTransform != null)
                {
                    // Pushed in: slightly scale down and shift in (pressed state)
                    yellowKnobTransform.localScale = pushedIn ? new Vector3(0.90f, 0.90f, 1f) : Vector3.one;
                }
                if (txtYellowStatus != null)
                {
                    txtYellowStatus.text = pushedIn ? "PUSHED IN (CHARGED)" : "POPPED OUT (APPLIED)";
                    txtYellowStatus.color = pushedIn ? valvePushedColor : valvePoppedColor;
                }
            }
            else if (valve == ValveType.TrailerRed)
            {
                if (redKnobTransform != null)
                {
                    redKnobTransform.localScale = pushedIn ? new Vector3(0.90f, 0.90f, 1f) : Vector3.one;
                }
                if (txtRedStatus != null)
                {
                    txtRedStatus.text = pushedIn ? "PUSHED IN (SUPPLIED)" : "POPPED OUT (EMERGENCY)";
                    txtRedStatus.color = pushedIn ? valvePushedColor : valvePoppedColor;
                }
            }
        }

        private void HandleLowAirWarningChanged(bool isActive)
        {
            if (warningPanel != null)
            {
                warningPanel.SetActive(isActive);
            }
        }

        private void UpdateWarningAnnunciatorFlash()
        {
            if (airSystem != null && airSystem.IsLowAirWarningActive && warningPanel != null && warningPanel.activeSelf)
            {
                warningFlashTimer += Time.deltaTime * 4f;
                float flash = Mathf.PingPong(warningFlashTimer, 1f);

                if (warningIcon != null)
                {
                    warningIcon.color = Color.Lerp(Color.red, Color.yellow, flash);
                }
                if (warningText != null)
                {
                    warningText.color = Color.Lerp(Color.white, Color.red, flash);
                }
            }
        }

        private void HandleStepChanged(DMVStep step)
        {
            if (txtStepHeader != null)
            {
                txtStepHeader.text = GetStepHeaderString(step);
            }

            if (btnFastForward != null)
            {
                btnFastForward.gameObject.SetActive(step == DMVStep.AppliedLeakageTest);
            }
        }

        private string GetStepHeaderString(DMVStep step)
        {
            switch (step)
            {
                case DMVStep.EnginePrep: return "STEP 1/5: CUT-OUT";
                case DMVStep.AppliedLeakageTest: return "STEP 2/5: APPLIED LEAK (L)";
                case DMVStep.LowAirAlarmTest: return "STEP 3/5: LOW ALARM (A)";
                case DMVStep.EmergencyPopOutTest: return "STEP 4/5: POP-OUT (B)";
                case DMVStep.CompressorBuildUpTest: return "STEP 5/5: BUILD-UP (L)";
                case DMVStep.TestCompleted: return "TEST PASSED!";
                case DMVStep.TestFailed: return "TEST FAILED";
                default: return "CDL TEST";
            }
        }

        private void HandleInstructionUpdated(string instruction, string script)
        {
            if (txtInstructions != null)
            {
                string goal = instruction;
                if (goal.Contains("\n"))
                {
                    var lines = goal.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        string tl = line.Trim();
                        if (tl.StartsWith("Goal:", System.StringComparison.OrdinalIgnoreCase) || tl.StartsWith("▶") || tl.StartsWith("HOLD PEDAL!"))
                        {
                            goal = tl.Replace("▶ ACTION REQUIRED:", "Goal:").Replace("▶ READY:", "Goal:");
                            break;
                        }
                        else if (!tl.StartsWith("Step ", System.StringComparison.OrdinalIgnoreCase))
                        {
                            goal = tl;
                        }
                    }
                }
                if (!goal.StartsWith("Goal:", System.StringComparison.OrdinalIgnoreCase) &&
                    !goal.StartsWith("HOLD", System.StringComparison.OrdinalIgnoreCase) &&
                    !goal.StartsWith("Applied Leakage", System.StringComparison.OrdinalIgnoreCase) &&
                    !goal.StartsWith("Brakes applied", System.StringComparison.OrdinalIgnoreCase))
                {
                    goal = "Goal: " + goal;
                }

                if (txtInstructions != null && txtInstructions != txtNextAction)
                {
                    txtInstructions.text = goal;
                }
                else if (dmvManager != null && dmvManager.CurrentGameMode == GameMode.Exam && txtNextAction != null)
                {
                    txtNextAction.text = goal;
                }
            }
            if (txtVerbalScript != null) txtVerbalScript.text = script;
        }

        private void HandleTimerUpdated(float current, float total)
        {
            if (txtTimer != null)
            {
                if (total > 0f)
                {
                    txtTimer.text = $"TIMER: {current:F1}s / {total:F0}s";
                }
                else
                {
                    txtTimer.text = "";
                }
            }
        }

        private void HandleTestFailed(string reason, string regulation)
        {
            if (failureModal != null)
            {
                failureModal.SetActive(true);
            }

            if (txtFailureReason != null) txtFailureReason.text = reason;
            if (txtFailureRegulation != null) txtFailureRegulation.text = $"Regulation: {regulation}";
        }

        private void HandleTestPassed(DMVTestManager.TestScorecard score)
        {
            if (successModal != null)
            {
                successModal.SetActive(true);
            }

            if (txtScorecard != null)
            {
                txtScorecard.text =
                    $"OFFICIAL CDL IN-CAB AIR BRAKE SCORECARD\n" +
                    $"----------------------------------------\n" +
                    $"1. Governor Cut-Out:      {score.CutOutPSI:F0} PSI (Spec: 120-140 PSI)  [PASS]\n" +
                    $"2. Applied Leakage Loss:   {score.LeakageDropPSI:F1} PSI (Spec: <= 4.0 PSI)   [PASS]\n" +
                    $"3. Low Air Warning Alarm: {score.LowAirAlarmPSI:F0} PSI (Spec: >= 60 PSI)   [PASS]\n" +
                    $"4. Emergency Pop-Out:\n" +
                    $"   - Trailer Red Knob:    {score.RedPopOutPSI:F0} PSI (Spec: 20-45 PSI)  [PASS]\n" +
                    $"   - Tractor Yellow Knob: {score.YellowPopOutPSI:F0} PSI (Spec: 20-45 PSI)  [PASS]\n" +
                    $"5. Compressor Build-Up:   {score.BuildUpSeconds:F1} sec (Spec: <= 45s)    [PASS]\n" +
                    $"----------------------------------------\n" +
                    $"FINAL RESULT: CLASS A CDL AIR BRAKE TEST PASSED!";
            }
        }

        private void HandleGameModeChanged(GameMode mode)
        {
            UpdateGameModeVisuals(mode);
        }

        private void HandleTargetHighlightChanged(HighlightTarget target)
        {
            UpdateHighlighters(target);
        }

        private void HandleTrainingGuidanceUpdated(string nextAction, string whyWeDoThis)
        {
            if (dmvManager != null && dmvManager.CurrentGameMode != GameMode.Training)
            {
                if (txtNextAction != null)
                {
                    txtNextAction.text = "🎯 DMV EXAM MODE: Official testing in progress.";
                }
                return;
            }

            if (txtNextAction != null && !string.IsNullOrEmpty(nextAction))
            {
                txtNextAction.text = $"🎯 NEXT ACTION: {nextAction}";
            }

            if (txtWhyWeDoThis != null && !string.IsNullOrEmpty(whyWeDoThis))
            {
                txtWhyWeDoThis.text = $"💡 WHY WE DO THIS: {whyWeDoThis}";
            }
        }

        private void HandleSoftErrorHint(HighlightTarget wrongTarget, string hint)
        {
            var wrongHl = GetHighlighterForTarget(wrongTarget);
            if (wrongHl != null)
            {
                wrongHl.TriggerWrongActionShake();
            }

            PulseCurrentHighlight();

            if (txtWhyWeDoThis != null && !string.IsNullOrEmpty(hint))
            {
                txtWhyWeDoThis.text = $"💡 TIP: <color=#fef08a>{hint}</color>";
            }
        }

        public void UpdateHighlighters(HighlightTarget target)
        {
            SetHighlighterState(hlIgnitionOff, false);
            SetHighlighterState(hlIgnitionAcc, false);
            SetHighlighterState(hlIgnitionRun, false);
            SetHighlighterState(hlRevEngine, false);
            SetHighlighterState(hlYellowKnob, false);
            SetHighlighterState(hlRedKnob, false);
            SetHighlighterState(hlHoldPedal, false);
            SetHighlighterState(hlFanPedal, false);
            SetHighlighterState(hlFastForward, false);

            if (dmvManager != null && dmvManager.CurrentGameMode != GameMode.Training)
            {
                return;
            }

            switch (target)
            {
                case HighlightTarget.IgnitionOff:
                    SetHighlighterState(hlIgnitionOff, true);
                    break;
                case HighlightTarget.IgnitionAcc:
                    SetHighlighterState(hlIgnitionAcc, true);
                    break;
                case HighlightTarget.IgnitionRun:
                    SetHighlighterState(hlIgnitionRun, true);
                    break;
                case HighlightTarget.RevEngine:
                    SetHighlighterState(hlRevEngine, true);
                    break;
                case HighlightTarget.YellowValve:
                    SetHighlighterState(hlYellowKnob, true);
                    break;
                case HighlightTarget.RedValve:
                    SetHighlighterState(hlRedKnob, true);
                    break;
                case HighlightTarget.BothValves:
                    SetHighlighterState(hlYellowKnob, true);
                    SetHighlighterState(hlRedKnob, true);
                    break;
                case HighlightTarget.HoldPedal:
                    SetHighlighterState(hlHoldPedal, true);
                    break;
                case HighlightTarget.FanPedal:
                    SetHighlighterState(hlFanPedal, true);
                    break;
                case HighlightTarget.FastForward:
                    SetHighlighterState(hlFastForward, true);
                    break;
            }
        }

        private void SetHighlighterState(UIElementHighlighter hl, bool active)
        {
            if (hl != null) hl.SetHighlighted(active);
        }

        private UIElementHighlighter GetHighlighterForTarget(HighlightTarget target)
        {
            switch (target)
            {
                case HighlightTarget.IgnitionOff: return hlIgnitionOff;
                case HighlightTarget.IgnitionAcc: return hlIgnitionAcc;
                case HighlightTarget.IgnitionRun: return hlIgnitionRun;
                case HighlightTarget.RevEngine: return hlRevEngine;
                case HighlightTarget.YellowValve: return hlYellowKnob;
                case HighlightTarget.RedValve: return hlRedKnob;
                case HighlightTarget.BothValves: return hlYellowKnob != null ? hlYellowKnob : hlRedKnob;
                case HighlightTarget.HoldPedal: return hlHoldPedal;
                case HighlightTarget.FanPedal: return hlFanPedal;
                case HighlightTarget.FastForward: return hlFastForward;
                default: return null;
            }
        }

        private void PulseCurrentHighlight()
        {
            if (dmvManager == null) return;
            HighlightTarget target = dmvManager.CurrentHighlightedTarget;
            var hl = GetHighlighterForTarget(target);
            if (hl != null) hl.PulseBrightHint();
            if (target == HighlightTarget.BothValves && hlRedKnob != null)
            {
                hlRedKnob.PulseBrightHint();
            }
        }

        public void UpdateGameModeVisuals(GameMode mode)
        {
            bool isTraining = (mode == GameMode.Training);

            if (imgModeTrainingBg != null)
            {
                imgModeTrainingBg.color = isTraining ? new Color(0.05f, 0.58f, 0.38f, 1f) : new Color(0.16f, 0.20f, 0.26f, 0.95f);
            }
            if (txtModeTraining != null)
            {
                txtModeTraining.text = isTraining ? "🎓 TRAINING MODE (ACTIVE)" : "🎓 START TRAINING";
                txtModeTraining.color = isTraining ? Color.white : new Color(0.72f, 0.78f, 0.88f);
                txtModeTraining.fontStyle = FontStyle.Bold;
            }

            if (imgModeExamBg != null)
            {
                imgModeExamBg.color = !isTraining ? new Color(0.85f, 0.20f, 0.20f, 1f) : new Color(0.16f, 0.20f, 0.26f, 0.95f);
            }
            if (txtModeExam != null)
            {
                txtModeExam.text = !isTraining ? "⏱️ DMV EXAM (ACTIVE)" : "⏱️ START DMV EXAM";
                txtModeExam.color = !isTraining ? Color.white : new Color(0.72f, 0.78f, 0.88f);
                txtModeExam.fontStyle = FontStyle.Bold;
            }

            if (whyWeDoThisBoxGO != null)
            {
                whyWeDoThisBoxGO.SetActive(isTraining);
            }

            if (!isTraining)
            {
                UpdateHighlighters(HighlightTarget.None);
                if (txtNextAction != null)
                {
                    txtNextAction.text = "🎯 DMV EXAM MODE: Perform steps independently without visual cues.";
                }
            }
            else
            {
                if (dmvManager != null)
                {
                    UpdateHighlighters(dmvManager.CurrentHighlightedTarget);
                    HandleTrainingGuidanceUpdated(dmvManager.CurrentNextAction, dmvManager.CurrentWhyWeDoThis);
                }
            }
        }

        public void BindModeSwitcher(Button trainingBtn, Image trainingBg, Text trainingTxt, Button examBtn, Image examBg, Text examTxt)
        {
            btnModeTraining = trainingBtn;
            imgModeTrainingBg = trainingBg;
            txtModeTraining = trainingTxt;

            btnModeExam = examBtn;
            imgModeExamBg = examBg;
            txtModeExam = examTxt;

            WireButtonListeners();
            UpdateGameModeVisuals(dmvManager != null ? dmvManager.CurrentGameMode : GameMode.Training);
        }

        public void BindTrainingCards(GameObject nextActionBox, Text nextActionTxt, GameObject whyWeDoThisBox, Text whyWeDoThisTxt)
        {
            nextActionBoxGO = nextActionBox;
            txtNextAction = nextActionTxt;
            whyWeDoThisBoxGO = whyWeDoThisBox;
            txtWhyWeDoThis = whyWeDoThisTxt;
        }

        public void BindHighlighters(
            UIElementHighlighter offHl,
            UIElementHighlighter accHl,
            UIElementHighlighter runHl,
            UIElementHighlighter revHl,
            UIElementHighlighter yelHl,
            UIElementHighlighter redHl,
            UIElementHighlighter holdHl,
            UIElementHighlighter fanHl,
            UIElementHighlighter fastHl)
        {
            hlIgnitionOff = offHl;
            hlIgnitionAcc = accHl;
            hlIgnitionRun = runHl;
            hlRevEngine = revHl;
            hlYellowKnob = yelHl;
            hlRedKnob = redHl;
            hlHoldPedal = holdHl;
            hlFanPedal = fanHl;
            hlFastForward = fastHl;
        }

        #endregion

        #region Public Binding Setters (for SceneSetupHelper)

        public void BindComponents(
            AirBrakeSystem system,
            DMVTestManager dmv,
            InteractivePedal pedal,
            Button btnOff, Button btnAcc, Button btnRun,
            Image imgOff, Image imgAcc, Image imgRun,
            Button btnRev, Text txtRev,
            Button btnYellow, RectTransform yellowRect, Text txtYellow,
            Button btnRed, RectTransform redRect, Text txtRed,
            Button btnFan,
            GameObject warnPanel, Image warnIcon, Text warnTxt,
            Text headerTxt, Text instructTxt, Text scriptTxt, Text timerTxt,
            Button fastFwdBtn, Toggle faultToggle,
            GameObject failModal, Text failReasonTxt, Text failRegTxt, Button retryBtn, Button restartBtn,
            GameObject passModal, Text scoreTxt, Button againBtn,
            Button newGameBtn = null,
            Text rpmCounter = null)
        {
            airSystem = system;
            dmvManager = dmv;
            brakePedal = pedal;
            txtRpmCounter = rpmCounter;

            btnStartNewGame = newGameBtn;
            if (btnStartNewGame != null)
            {
                btnStartNewGame.onClick.RemoveAllListeners();
                btnStartNewGame.onClick.AddListener(StartNewGame);
            }

            btnIgnitionOff = btnOff;
            btnIgnitionAcc = btnAcc;
            btnIgnitionRun = btnRun;
            imgIgnitionOff = imgOff;
            imgIgnitionAcc = imgAcc;
            imgIgnitionRun = imgRun;
            btnRevEngine = btnRev;
            txtRevEngine = txtRev;

            btnYellowKnob = btnYellow;
            yellowKnobTransform = yellowRect;
            txtYellowStatus = txtYellow;

            btnRedKnob = btnRed;
            redKnobTransform = redRect;
            txtRedStatus = txtRed;

            btnFanPedal = btnFan;

            warningPanel = warnPanel;
            warningIcon = warnIcon;
            warningText = warnTxt;

            txtStepHeader = headerTxt;
            txtInstructions = instructTxt;
            txtVerbalScript = scriptTxt;
            txtTimer = timerTxt;
            btnFastForward = fastFwdBtn;
            toggleFaultLeak = faultToggle;

            failureModal = failModal;
            if (failureModal != null) failureModal.SetActive(false);
            txtFailureReason = failReasonTxt;
            txtFailureRegulation = failRegTxt;
            btnRetryStep = retryBtn;
            btnRestartFullTest = restartBtn;

            successModal = passModal;
            if (successModal != null) successModal.SetActive(false);
            txtScorecard = scoreTxt;
            btnInspectAgain = againBtn;

            // Wire all listeners and subscribe to events immediately
            WireButtonListeners();
            SubscribeEvents();
            UpdateIgnitionVisuals(airSystem != null ? airSystem.CurrentIgnition : IgnitionState.OFF);
            UpdateValveVisual(ValveType.TractorYellow, airSystem != null && airSystem.IsYellowBrakePushedIn);
            UpdateValveVisual(ValveType.TrailerRed, airSystem != null && airSystem.IsRedTrailerPushedIn);
        }

        public void SetTutorialGuide(TutorialGuideController guide) => tutorialGuide = guide;

        #endregion
    }
}
