using UnityEngine;
using UnityEngine.UI;

namespace CDL.AirBrakeSimulator
{
    /// <summary>
    /// Interactive CDL Air Brake Training & Tutorial Mode.
    /// Provides step-by-step guidance for student drivers:
    /// - Pulsating highlight outline & arrow badge over the exact next interactive control.
    /// - Comprehensive pedagogical guidance card:
    ///     * WHAT TO DO (exact button, valve, or pedal to activate, including keyboard shortcut).
    ///     * WHY / PURPOSE (FMCSA regulations, pneumatic rationale, and mechanical purpose).
    ///     * EXPECTED RESULT (exact pressure thresholds, audio cues, and pass criteria).
    /// </summary>
    public class TutorialGuideController : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private AirBrakeSystem airSystem;
        [SerializeField] private DMVTestManager dmvManager;
        [SerializeField] private AirBrakeUIController uiController;

        [Header("Interactive Target Controls (for highlighting)")]
        [SerializeField] private RectTransform targetIgnitionOff;
        [SerializeField] private RectTransform targetIgnitionAcc;
        [SerializeField] private RectTransform targetIgnitionRun;
        [SerializeField] private RectTransform targetRevEngine;
        [SerializeField] private RectTransform targetYellowValve;
        [SerializeField] private RectTransform targetRedValve;
        [SerializeField] private RectTransform targetHoldPedal;
        [SerializeField] private RectTransform targetFanPedal;
        [SerializeField] private RectTransform targetFastForward;
        [SerializeField] private RectTransform targetRetryBtn;
        [SerializeField] private RectTransform targetStartNewGameBtn;

        [Header("Training Mode Toggle Button")]
        [SerializeField] private Button btnToggleTraining;
        [SerializeField] private Text txtToggleTraining;

        [Header("Tutorial Guidance Card UI")]
        [SerializeField] private GameObject tutorialCardGO;
        [SerializeField] private Text txtTutorialStepTitle;
        [SerializeField] private Text txtTutorialHowAction;
        [SerializeField] private Text txtTutorialWhyPurpose;
        [SerializeField] private Text txtTutorialCriteriaExpected;
        [SerializeField] private Button btnCloseTutorial;

        [Header("Animated Highlight Frame")]
        [SerializeField] private GameObject highlightBoxGO;
        [SerializeField] private RectTransform highlightRect;
        [SerializeField] private Image highlightImg;
        [SerializeField] private Text highlightBadgeText;
        [SerializeField] private Outline highlightOutline;

        private bool isTrainingModeActive = false;
        private RectTransform currentTargetControl = null;
        private Color btnActiveCol = new Color(0.92f, 0.68f, 0.12f, 1f);
        private Color btnInactiveCol = new Color(0.22f, 0.28f, 0.38f, 1f);

        public bool IsTrainingModeActive => isTrainingModeActive;

        private void Awake()
        {
            if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
            if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();
            if (uiController == null) uiController = FindFirstObjectByType<AirBrakeUIController>();

            if (dmvManager != null)
            {
                dmvManager.OnGameModeChanged += (mode) => SetTrainingMode(mode == GameMode.Training);
            }

            if (btnCloseTutorial != null)
            {
                btnCloseTutorial.onClick.RemoveAllListeners();
                btnCloseTutorial.onClick.AddListener(() => SetTrainingMode(false));
            }

            if (highlightBoxGO != null)
            {
                CanvasGroup cg = highlightBoxGO.GetComponent<CanvasGroup>();
                if (cg == null) cg = highlightBoxGO.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable = false;
                if (highlightImg != null) highlightImg.raycastTarget = false;
                if (highlightBadgeText != null) highlightBadgeText.raycastTarget = false;
                highlightBoxGO.SetActive(false);
            }
        }

        private void Start()
        {
            if (highlightBoxGO != null)
            {
                CanvasGroup cg = highlightBoxGO.GetComponent<CanvasGroup>();
                if (cg == null) cg = highlightBoxGO.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable = false;
                if (highlightImg != null) highlightImg.raycastTarget = false;
                if (highlightBadgeText != null) highlightBadgeText.raycastTarget = false;
                highlightBoxGO.SetActive(false);
            }

            if (dmvManager != null)
            {
                SetTrainingMode(dmvManager.CurrentGameMode == GameMode.Training);
            }
            else
            {
                ApplyTrainingModeVisuals(false);
            }
        }

        private void Update()
        {
            if (airSystem == null) airSystem = FindFirstObjectByType<AirBrakeSystem>();
            if (dmvManager == null) dmvManager = FindFirstObjectByType<DMVTestManager>();

            if (!isTrainingModeActive) return;

            UpdateTutorialState();
            AnimateHighlightBox();
        }

        /// <summary>
        /// Toggles the interactive training mode ON / OFF.
        /// </summary>
        public void ToggleTrainingMode()
        {
            SetTrainingMode(!isTrainingModeActive);
        }

        public void SetTrainingMode(bool active)
        {
            isTrainingModeActive = active;
            if (dmvManager != null && dmvManager.CurrentGameMode != (active ? GameMode.Training : GameMode.Exam))
            {
                dmvManager.SetGameMode(active ? GameMode.Training : GameMode.Exam);
            }
            ApplyTrainingModeVisuals(active);
        }

        private void ApplyTrainingModeVisuals(bool active)
        {
            if (txtToggleTraining != null)
            {
                txtToggleTraining.text = active ? "🎓 TRAINING MODE (ACTIVE)" : "🎓 START TRAINING";
            }

            if (tutorialCardGO != null)
            {
                tutorialCardGO.SetActive(false); // In unified UI, Next Action and Why We Do This are built into Examiner Card
            }

            if (highlightBoxGO != null)
            {
                highlightBoxGO.SetActive(false); // UIElementHighlighter provides in-place control glow/arrows
            }

            if (active)
            {
                UpdateTutorialState();
            }
        }

        private void UpdateTutorialState()
        {
            if (dmvManager == null || airSystem == null) return;

            string stepTitle = "";
            string howAction = "";
            string whyPurpose = "";
            string criteriaExpected = "";
            RectTransform target = null;
            string badgeLabel = "👉 НАЖМИТЕ ЗДЕСЬ (CLICK)";

            DMVStep step = dmvManager.CurrentStep;

            switch (step)
            {
                case DMVStep.EnginePrep:
                    stepTitle = "ШАГ 1/5: ПРОВЕРКА ОТСЕЧКИ КОМПРЕССОРА (GOVERNOR CUT-OUT)";
                    if (airSystem.CurrentIgnition != IgnitionState.RUNNING)
                    {
                        target = targetIgnitionRun;
                        howAction = "▶ ДЕЙСТВИЕ: Нажмите кнопку [RUNNING] или клавишу <I> на клавиатуре.";
                        whyPurpose = "💡 ЗАЧЕМ: Чтобы компрессор начал нагнетать воздух в систему, двигатель должен быть запущен.";
                        criteriaExpected = "✔ РЕЗУЛЬТАТ: Двигатель заведется со стартера, приборы оживут.";
                        badgeLabel = "👇 ЗАВЕДИТЕ ДВИГАТЕЛЬ (RUNNING)";
                    }
                    else if (airSystem.PrimaryPSI < 120f)
                    {
                        target = targetRevEngine;
                        howAction = "▶ ДЕЙСТВИЕ: Нажмите [⚡ REV ENGINE] или зажмите клавишу <F>.";
                        whyPurpose = "💡 ЗАЧЕМ: Повышенные обороты (~1500 RPM) ускоряют накачку воздуха компрессором до рабочей зоны (120–140 PSI).";
                        criteriaExpected = "✔ РЕЗУЛЬТАТ: Давление быстро поднимется до зоны срабатывания регулятора.";
                        badgeLabel = "⚡ ПОГАЗУЙТЕ (1500 RPM)";
                    }
                    else
                    {
                        target = null;
                        howAction = "▶ ДЕЙСТВИЕ: Ожидайте отсечки компрессора (Governor Cut-Out)...";
                        whyPurpose = "💡 ЗАЧЕМ: Компрессор накачивает последние PSI до отключения регулятором давления.";
                        criteriaExpected = "✔ НОРМАТИВ: Отсечка произойдет строго между 120 и 140 PSI со звуком сброса воздуха (пшик).";
                        badgeLabel = "⏳ ОЖИДАЙТЕ ОТСЕЧКИ...";
                    }
                    break;

                case DMVStep.AppliedLeakageTest:
                    stepTitle = "ШАГ 2/5: ПРОВЕРКА УТЕЧКИ ПОД НАГРУЗКОЙ (APPLIED LEAKAGE TEST)";
                    if (airSystem.CurrentIgnition == IgnitionState.RUNNING)
                    {
                        target = targetIgnitionOff;
                        howAction = "▶ ДЕЙСТВИЕ: Заглушите двигатель: нажмите кнопку [OFF] или клавишу <I>.";
                        whyPurpose = "💡 ЗАЧЕМ: По стандарту FMCSA тест на утечку проводится СТРОГО при заглушенном двигателе, чтобы компрессор не подкачивал воздух.";
                        criteriaExpected = "✔ РЕЗУЛЬТАТ: Двигатель заглохнет, подача воздуха компрессором прекратится.";
                        badgeLabel = "🛑 ЗАГЛУШИТЕ ДВИГАТЕЛЬ (OFF)";
                    }
                    else if (airSystem.CurrentIgnition == IgnitionState.OFF)
                    {
                        target = targetIgnitionAcc;
                        howAction = "▶ ДЕЙСТВИЕ: Включите зажигание: нажмите [ACC / ON] или клавишу <I>.";
                        whyPurpose = "💡 ЗАЧЕМ: Электропитание должно быть включено, чтобы видеть показания манометров и работу аварийных сигналов.";
                        criteriaExpected = "✔ РЕЗУЛЬТАТ: Приборы включатся и покажут текущее давление в ресиверах.";
                        badgeLabel = "🔑 ВКЛЮЧИТЕ ЗАЖИГАНИЕ (ACC/ON)";
                    }
                    else if (!airSystem.IsYellowBrakePushedIn)
                    {
                        target = targetYellowValve;
                        howAction = "▶ ДЕЙСТВИЕ: Нажмите на желтый клапан [TRACTOR PARKING] (клавиша <Y>), чтобы утопить его (PUSH IN).";
                        whyPurpose = "💡 ЗАЧЕМ: Снятие тягача со стояночного тормоза для подачи давления в тормозные камеры.";
                        criteriaExpected = "✔ РЕЗУЛЬТАТ: Клапан утопится (PUSHED IN), загорится зеленый статус RELEASED.";
                        badgeLabel = "👉 УТОПИТЕ ЖЕЛТЫЙ КЛАПАН";
                    }
                    else if (!airSystem.IsRedTrailerPushedIn)
                    {
                        target = targetRedValve;
                        howAction = "▶ ДЕЙСТВИЕ: Нажмите на красный клапан [TRAILER AIR SUPPLY] (клавиша <R>), чтобы утопить его (PUSH IN).";
                        whyPurpose = "💡 ЗАЧЕМ: Подача воздуха в тормозную магистраль полуприцепа и снятие с аварийного тормоза.";
                        criteriaExpected = "✔ РЕЗУЛЬТАТ: Клапан утопится (PUSHED IN), загорится зеленый статус SUPPLIED.";
                        badgeLabel = "👉 УТОПИТЕ КРАСНЫЙ КЛАПАН";
                    }
                    else
                    {
                        // Both valves pushed in, ready to test
                        target = targetFastForward != null ? targetFastForward : targetHoldPedal;
                        howAction = "▶ ДЕЙСТВИЕ: Нажмите золотую кнопку [⏩ FAST FORWARD 60s >>] (или удерживайте [HOLD PEDAL] 60 сек).";
                        whyPurpose = "💡 ЗАЧЕМ: Замер падения давления в рабочих камерах под полной нагрузкой за 1 минуту.";
                        criteriaExpected = "✔ НОРМАТИВ: Потеря давления за 60 секунд не должна превысить 4.0 PSI для автопоезда (Class A).";
                        badgeLabel = "⏩ ПЕРЕМОТКА 60с (ИЛИ УДЕРЖИВАТЬ ТОРМОЗ)";
                    }
                    break;

                case DMVStep.LowAirAlarmTest:
                    stepTitle = "ШАГ 3/5: СИГНАЛИЗАЦИЯ НИЗКОГО ДАВЛЕНИЯ (LOW AIR WARNING)";
                    if (airSystem.CurrentIgnition != IgnitionState.ACC_ON)
                    {
                        target = targetIgnitionAcc;
                        howAction = "▶ ДЕЙСТВИЕ: Включите зажигание: нажмите [ACC / ON] (клавиша <I>).";
                        whyPurpose = "💡 ЗАЧЕМ: Аварийная сигнализация (зуммер и табло) требует питания бортовой сети.";
                        criteriaExpected = "✔ РЕЗУЛЬТАТ: Питание включено (ACC/ON).";
                        badgeLabel = "🔑 ВКЛЮЧИТЕ ЗАЖИГАНИЕ";
                    }
                    else
                    {
                        target = targetFanPedal;
                        howAction = "▶ ДЕЙСТВИЕ: Нажимайте кнопку [FAN PEDAL] (или клавишу <B>), чтобы стравливать воздух порциями.";
                        whyPurpose = "💡 ЗАЧЕМ: Имитация частых рабочих торможений для проверки порога включения аварийного зуммера.";
                        criteriaExpected = "✔ НОРМАТИВ: Зуммер и красная лампа LOW AIR обязаны включиться при давлении НЕ МЕНЕЕ 60 PSI.";
                        badgeLabel = "🦶 НАЖИМАЙТЕ FAN PEDAL (СТРАВЛИВАЙТЕ)";
                    }
                    break;

                case DMVStep.EmergencyPopOutTest:
                    stepTitle = "ШАГ 4/5: ОТЩЕЛКИВАНИЕ КЛАПАНОВ (EMERGENCY POP-OUT TEST)";
                    target = targetFanPedal;
                    howAction = "▶ ДЕЙСТВИЕ: Продолжайте нажимать кнопку [FAN PEDAL] (клавиша <B>), стравливая давление ниже 60 PSI.";
                    whyPurpose = "💡 ЗАЧЕМ: Проверка автоматического срабатывания аварийных пружинных тормозов (Spring Brakes) при аварийной утечке.";
                    criteriaExpected = "✔ НОРМАТИВ: В диапазоне 20–45 PSI красный клапан прицепа, а затем желтый клапан тягача обязаны со звуком щелчка выскочить наружу (POPPED OUT).";
                    badgeLabel = "🦶 СТРАВЛИВАЙТЕ ДО ОТЩЕЛКИВАНИЯ (20-45 PSI)";
                    break;

                case DMVStep.CompressorBuildUpTest:
                    stepTitle = "ШАГ 5/5: СКОРОСТЬ НАКАЧКИ КОМПРЕССОРА (COMPRESSOR BUILD-UP)";
                    if (airSystem.CurrentIgnition != IgnitionState.RUNNING)
                    {
                        target = targetIgnitionRun;
                        howAction = "▶ ДЕЙСТВИЕ: Запустите двигатель: нажмите кнопку [RUNNING] (клавиша <I>).";
                        whyPurpose = "💡 ЗАЧЕМ: Для замера времени накачки двигатель должен работать и вращать компрессор.";
                        criteriaExpected = "✔ РЕЗУЛЬТАТ: Двигатель заведется и начнет нагнетать давление.";
                        badgeLabel = "👇 ЗАВЕДИТЕ ДВИГАТЕЛЬ (RUNNING)";
                    }
                    else
                    {
                        target = targetRevEngine;
                        howAction = "▶ ДЕЙСТВИЕ: Нажмите [⚡ REV ENGINE] (клавиша <F>), удерживая рабочие обороты компрессора (~1500 RPM).";
                        whyPurpose = "💡 ЗАЧЕМ: Проверка производительности компрессора при накачке давления с 85 до 100 PSI.";
                        criteriaExpected = "✔ НОРМАТИВ: Время накачки с 85 до 100 PSI должно быть строго МЕНЕЕ 45 СЕКУНД.";
                        badgeLabel = "⚡ ДЕРЖИТЕ ОБОРОТЫ (REV ENGINE)";
                    }
                    break;

                case DMVStep.TestCompleted:
                    stepTitle = "ПОЗДРАВЛЯЕМ! ЭКЗАМЕН CDL УСПЕШНО СДАН! 🎉";
                    target = targetStartNewGameBtn;
                    howAction = "▶ ДЕЙСТВИЕ: Все 5 нормативов проверки тормозной системы пройдены без нарушений!";
                    whyPurpose = "💡 ВЫ УСПЕШНО ОСВОИЛИ: 1) Cut-Out 120-140 PSI, 2) Applied Leak <=4 PSI/мин, 3) Warning >=60 PSI, 4) Pop-Out 20-45 PSI, 5) Build-Up <=45с.";
                    criteriaExpected = "✔ Для повторной тренировки нажмите [↻ START NEW GAME].";
                    badgeLabel = "🏆 ЭКЗАМЕН СДАН! НАЖМИТЕ START NEW GAME";
                    break;

                case DMVStep.TestFailed:
                    stepTitle = "ОШИБКА / НАРУШЕНИЕ РЕГЛАМЕНТА CDL ⚠";
                    target = targetRetryBtn;
                    howAction = "▶ ДЕЙСТВИЕ: Нажмите зеленую кнопку [RETRY THIS STEP] в окне ошибки.";
                    whyPurpose = $"💡 ПРИЧИНА: {dmvManager.FailureReason}";
                    criteriaExpected = "✔ Вы вернетесь к началу этого шага с правильными начальными условиями.";
                    badgeLabel = "🔄 ПОВТОРИТЕ ШАГ (RETRY)";
                    break;
            }

            currentTargetControl = target;

            if (txtTutorialStepTitle != null) txtTutorialStepTitle.text = stepTitle;
            if (txtTutorialHowAction != null) txtTutorialHowAction.text = howAction;
            if (txtTutorialWhyPurpose != null) txtTutorialWhyPurpose.text = whyPurpose;
            if (txtTutorialCriteriaExpected != null) txtTutorialCriteriaExpected.text = criteriaExpected;
            if (highlightBadgeText != null) highlightBadgeText.text = badgeLabel;
        }

        private void AnimateHighlightBox()
        {
            // UIElementHighlighter attached directly to the controls now handles in-place glow halo and arrow badges.
            // Keeping highlightBoxGO disabled prevents duplicate badges and eliminates any risk of raycast blocking.
            if (highlightBoxGO != null && highlightBoxGO.activeSelf)
            {
                highlightBoxGO.SetActive(false);
            }
        }

        public void BindTargetReferences(
            RectTransform ignOff,
            RectTransform ignAcc,
            RectTransform ignRun,
            RectTransform revEng,
            RectTransform yellowVal,
            RectTransform redVal,
            RectTransform holdPed,
            RectTransform fanPed,
            RectTransform fastFwd,
            RectTransform retryBtn,
            RectTransform startNewBtn)
        {
            targetIgnitionOff = ignOff;
            targetIgnitionAcc = ignAcc;
            targetIgnitionRun = ignRun;
            targetRevEngine = revEng;
            targetYellowValve = yellowVal;
            targetRedValve = redVal;
            targetHoldPedal = holdPed;
            targetFanPedal = fanPed;
            targetFastForward = fastFwd;
            targetRetryBtn = retryBtn;
            targetStartNewGameBtn = startNewBtn;
        }

        public void BindUIElements(
            Button toggleBtn,
            Text toggleTxt,
            GameObject cardGO,
            Text stepTitleTxt,
            Text howTxt,
            Text whyTxt,
            Text criteriaTxt,
            Button closeBtn,
            GameObject highlightGO,
            RectTransform highlightRt,
            Image highlightImage,
            Text badgeTxt,
            Outline outline)
        {
            btnToggleTraining = toggleBtn;
            txtToggleTraining = toggleTxt;
            tutorialCardGO = cardGO;
            txtTutorialStepTitle = stepTitleTxt;
            txtTutorialHowAction = howTxt;
            txtTutorialWhyPurpose = whyTxt;
            txtTutorialCriteriaExpected = criteriaTxt;
            btnCloseTutorial = closeBtn;

            highlightBoxGO = highlightGO;
            highlightRect = highlightRt;
            highlightImg = highlightImage;
            highlightBadgeText = badgeTxt;
            highlightOutline = outline;

            if (highlightBoxGO != null)
            {
                CanvasGroup cg = highlightBoxGO.GetComponent<CanvasGroup>();
                if (cg == null) cg = highlightBoxGO.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;
                cg.interactable = false;
                if (highlightImg != null) highlightImg.raycastTarget = false;
                if (highlightBadgeText != null) highlightBadgeText.raycastTarget = false;
                highlightBoxGO.SetActive(false);
            }

            if (btnCloseTutorial != null)
            {
                btnCloseTutorial.onClick.RemoveAllListeners();
                btnCloseTutorial.onClick.AddListener(() => SetTrainingMode(false));
            }
        }
    }
}
